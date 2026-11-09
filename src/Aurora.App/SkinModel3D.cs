using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Aurora.App;

/// <summary>
/// WPF 3D 皮肤小人：Steve / Alex 两种体型，支持拖拽 360 度旋转。
/// </summary>
internal sealed class SkinModel3D
{
    private const double HeadSize = 8;
    private const double BodyWidth = 8;
    private const double BodyHeight = 12;
    private const double LimbDepth = 4;

    public static Model3DGroup Build(BitmapSource? skin, bool slim)
    {
        var group = new Model3DGroup();
        var material = MaterialFor(skin);

        // 关键：整体下移，让模型以 (0,0,0) 为中心
        group.Children.Add(Box(new Rect3(0, 4, 0, HeadSize, HeadSize, HeadSize), material, HeadFaces));
        group.Children.Add(Box(new Rect3(0, -6, 0, BodyWidth, BodyHeight, LimbDepth), material, BodyFaces));

        var armWidth = slim ? 3.0 : 4.0;
        group.Children.Add(Box(new Rect3(-(BodyWidth / 2 + armWidth / 2), -6, 0, armWidth, BodyHeight, LimbDepth),
            material, ArmFaces));
        group.Children.Add(Box(new Rect3(BodyWidth / 2 + armWidth / 2, -6, 0, armWidth, BodyHeight, LimbDepth),
            material, ArmFaces));

        group.Children.Add(Box(new Rect3(-2, -18, 0, 4, BodyHeight, LimbDepth), material, RightLegFaces));
        group.Children.Add(Box(new Rect3(2, -18, 0, 4, BodyHeight, LimbDepth), material, LeftLegFaces));

        return group;
    }

    public static PerspectiveCamera Camera() => new()
    {
        Position = new Point3D(0, -8, 104),
        LookDirection = new Vector3D(0, 0, -104),
        UpDirection = new Vector3D(0, 1, 0),
        FieldOfView = 40,
    };

    public static Model3DGroup Lights()
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(0xAA, 0xAA, 0xAA)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0xFF, 0xFF, 0xFF), new Vector3D(-0.4, -0.6, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x55, 0x60, 0x70), new Vector3D(0.8, 0.3, -0.6)));
        return group;
    }

    // ---- 皮肤 UV 区域（64x64 皮肤）----
    private static readonly Rect[] HeadFaces =
    {
        new(8, 8, 8, 8),    // front
        new(16, 8, 8, 8),   // left
        new(24, 8, 8, 8),   // back
        new(0, 8, 8, 8),    // right
        new(8, 0, 8, 8),    // top
        new(16, 0, 8, 8),   // bottom
    };

    private static readonly Rect[] BodyFaces =
    {
        new(20, 20, 8, 12),
        new(28, 20, 4, 12),
        new(32, 20, 8, 12),
        new(16, 20, 4, 12),
        new(20, 16, 8, 4),
        new(28, 16, 8, 4),
    };

    private static readonly Rect[] ArmFaces =
    {
        new(44, 20, 4, 12),
        new(48, 20, 4, 12),
        new(56, 20, 4, 12),
        new(40, 20, 4, 12),
        new(44, 16, 4, 4),
        new(48, 16, 4, 4),
    };

    private static readonly Rect[] RightLegFaces =
    {
        new(4, 20, 4, 12),
        new(8, 20, 4, 12),
        new(12, 20, 4, 12),
        new(0, 20, 4, 12),
        new(4, 16, 4, 4),
        new(8, 16, 4, 4),
    };

    private static readonly Rect[] LeftLegFaces =
    {
        new(20, 52, 4, 12),
        new(24, 52, 4, 12),
        new(28, 52, 4, 12),
        new(16, 52, 4, 12),
        new(20, 48, 4, 4),
        new(24, 48, 4, 4),
    };

    private static Material MaterialFor(BitmapSource? skin)
    {
        if (skin == null)
        {
            return new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xF2)));
        }

        // 用绝对像素坐标取皮肤区域
        var brush = new ImageBrush(skin)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, skin.PixelWidth, skin.PixelHeight),
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
        };
        brush.Freeze();
        return new DiffuseMaterial(brush);
    }

    private readonly record struct Rect3(double X, double Y, double Z, double W, double H, double D);

    /// <summary>建一个长方体；faces 顺序为 front/left/back/right/top/bottom。</summary>
    private static GeometryModel3D Box(Rect3 box, Material material, Rect[] faces)
    {
        var mesh = new MeshGeometry3D();
        var hw = box.W / 2;
        var hh = box.H / 2;
        var hd = box.D / 2;

        // 局部坐标，最后整体平移
        AddQuad(mesh, 0, faces[0],
            new Point3D(-hw, hh, hd), new Point3D(hw, hh, hd), new Point3D(hw, -hh, hd), new Point3D(-hw, -hh, hd),
            new Vector3D(0, 0, 1));
        AddQuad(mesh, 0, faces[1],
            new Point3D(-hw, hh, -hd), new Point3D(-hw, hh, hd), new Point3D(-hw, -hh, hd), new Point3D(-hw, -hh, -hd),
            new Vector3D(-1, 0, 0));
        AddQuad(mesh, 0, faces[2],
            new Point3D(hw, hh, -hd), new Point3D(-hw, hh, -hd), new Point3D(-hw, -hh, -hd), new Point3D(hw, -hh, -hd),
            new Vector3D(0, 0, -1));
        AddQuad(mesh, 0, faces[3],
            new Point3D(hw, hh, hd), new Point3D(hw, hh, -hd), new Point3D(hw, -hh, -hd), new Point3D(hw, -hh, hd),
            new Vector3D(1, 0, 0));
        AddQuad(mesh, 0, faces[4],
            new Point3D(-hw, hh, -hd), new Point3D(hw, hh, -hd), new Point3D(hw, hh, hd), new Point3D(-hw, hh, hd),
            new Vector3D(0, 1, 0));
        AddQuad(mesh, 0, faces[5],
            new Point3D(-hw, -hh, hd), new Point3D(hw, -hh, hd), new Point3D(hw, -hh, -hd), new Point3D(-hw, -hh, -hd),
            new Vector3D(0, -1, 0));

        var model = new GeometryModel3D(mesh, material)
        {
            Transform = new TranslateTransform3D(box.X, box.Y, box.Z),
        };

        return model;
    }

    private static void AddQuad(MeshGeometry3D mesh, int _ , Rect uv, Point3D a, Point3D b, Point3D c, Point3D d,
        Vector3D normal)
    {
        var start = mesh.Positions.Count;

        mesh.Positions.Add(a);
        mesh.Positions.Add(b);
        mesh.Positions.Add(c);
        mesh.Positions.Add(d);

        mesh.Normals.Add(normal);
        mesh.Normals.Add(normal);
        mesh.Normals.Add(normal);
        mesh.Normals.Add(normal);

        // 纹理坐标按 uv 归一化到 64x64 皮肤
        var u0 = uv.Left / 64.0;
        var v0 = uv.Top / 64.0;
        var u1 = uv.Right / 64.0;
        var v1 = uv.Bottom / 64.0;

        mesh.TextureCoordinates.Add(new Point(u0, v0));
        mesh.TextureCoordinates.Add(new Point(u1, v0));
        mesh.TextureCoordinates.Add(new Point(u1, v1));
        mesh.TextureCoordinates.Add(new Point(u0, v1));

        // 逆时针（从外面看）
        mesh.TriangleIndices.Add(start);
        mesh.TriangleIndices.Add(start + 1);
        mesh.TriangleIndices.Add(start + 2);
        mesh.TriangleIndices.Add(start);
        mesh.TriangleIndices.Add(start + 2);
        mesh.TriangleIndices.Add(start + 3);
    }
}
