using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Aurora.App;

/// <summary>
/// 用 WPF 3D 拼一个方块小人来预览皮肤（Steve 粗胳膊 / Alex 细胳膊）。
/// 没有皮肤时就是一个白色模型。
/// </summary>
internal static class SkinModel3D
{
    public static Model3DGroup Build(BitmapSource? skin, bool slim)
    {
        var group = new Model3DGroup();
        var material = skin == null ? WhiteMaterial() : SkinMaterial(skin);

        // 头 8x8x8（中心在 y=28..36）
        group.Children.Add(Box(8, 8, 8, 0, 0, material, new[]
        {
            Rect(8, 8, 8, 8),   // front
            Rect(16, 8, 8, 8),  // left
            Rect(24, 8, 8, 8),  // back
            Rect(0, 8, 8, 8),   // right
            Rect(8, 0, 8, 8),   // top
            Rect(16, 0, 8, 8),  // bottom
        }));

        // 身体 8x12x4
        group.Children.Add(Box(8, 12, 4, 0, -10, material, new[]
        {
            Rect(20, 20, 8, 12),
            Rect(28, 20, 8, 12),
            Rect(32, 20, 8, 12),
            Rect(16, 20, 4, 12),
            Rect(20, 16, 8, 4),
            Rect(28, 16, 8, 4),
        }));

        var armWidth = slim ? 3.0 : 4.0;
        var armU = slim ? 44.0 : 44.0;

        // 右臂
        group.Children.Add(Box(armWidth, 12, 4, -4 - armWidth / 2, -10, material, new[]
        {
            Rect(armU, 20, armWidth, 12),
            Rect(armU + armWidth, 20, 4, 12),
            Rect(56, 20, armWidth, 12),
            Rect(40, 20, 4, 12),
            Rect(armU, 16, armWidth, 4),
            Rect(armU + armWidth, 16, armWidth, 4),
        }));

        // 左臂（镜像，复用右臂贴图）
        group.Children.Add(Box(armWidth, 12, 4, 4 + armWidth / 2, -10, material, new[]
        {
            Rect(armU, 20, armWidth, 12),
            Rect(armU + armWidth, 20, 4, 12),
            Rect(56, 20, armWidth, 12),
            Rect(40, 20, 4, 12),
            Rect(armU, 16, armWidth, 4),
            Rect(armU + armWidth, 16, armWidth, 4),
        }));

        // 右腿
        group.Children.Add(Box(4, 12, 4, -2, -22, material, new[]
        {
            Rect(4, 20, 4, 12),
            Rect(8, 20, 4, 12),
            Rect(12, 20, 4, 12),
            Rect(0, 20, 4, 12),
            Rect(4, 16, 4, 4),
            Rect(8, 16, 4, 4),
        }));

        // 左腿
        group.Children.Add(Box(4, 12, 4, 2, -22, material, new[]
        {
            Rect(20, 52, 4, 12),
            Rect(24, 52, 4, 12),
            Rect(28, 52, 4, 12),
            Rect(16, 52, 4, 12),
            Rect(20, 48, 4, 4),
            Rect(24, 48, 4, 4),
        }));

        return group;
    }

    public static PerspectiveCamera Camera() => new()
    {
        Position = new Point3D(0, -12, 78),
        LookDirection = new Vector3D(0, 0, -78),
        UpDirection = new Vector3D(0, 1, 0),
        FieldOfView = 30,
    };

    public static Model3DGroup Lights()
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(0x8A, 0x8A, 0x8A)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0xFF, 0xFF, 0xFF), new Vector3D(-0.5, -0.6, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x60, 0x60, 0x70), new Vector3D(0.7, 0.2, -0.4)));
        return group;
    }

    private static Material WhiteMaterial()
    {
        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xEE))));
        return group;
    }

    private static MaterialGroup SkinMaterial(BitmapSource skin)
    {
        var brush = new ImageBrush(skin)
        {
            Stretch = Stretch.Fill,
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();

        var group = new MaterialGroup();
        group.Children.Add(new DiffuseMaterial(brush));
        return group;
    }

    private static Rect Rect(double x, double y, double w, double h) => new(x, y, w, h);

    /// <summary>一个长方体，faces 依次是 front/left/back/right/top/bottom 的皮肤区域（像素坐标）。</summary>
    private static GeometryModel3D Box(double width, double height, double depth, double centerX, double centerY,
        Material material, Rect[] faces)
    {
        var mesh = new MeshGeometry3D();
        var hw = width / 2;
        var hh = height / 2;
        var hd = depth / 2;

        // 顶点顺序：0-3 前面，4-7 左面，8-11 后面，12-15 右面，16-19 顶面，20-23 底面
        AddFace(mesh, new Point3D(-hw, hh, hd), new Point3D(hw, hh, hd), new Point3D(hw, -hh, hd), new Point3D(-hw, -hh, hd), faces[0]);
        AddFace(mesh, new Point3D(-hw, hh, -hd), new Point3D(-hw, hh, hd), new Point3D(-hw, -hh, hd), new Point3D(-hw, -hh, -hd), faces[1]);
        AddFace(mesh, new Point3D(hw, hh, -hd), new Point3D(-hw, hh, -hd), new Point3D(-hw, -hh, -hd), new Point3D(hw, -hh, -hd), faces[2]);
        AddFace(mesh, new Point3D(hw, hh, hd), new Point3D(hw, hh, -hd), new Point3D(hw, -hh, -hd), new Point3D(hw, -hh, hd), faces[3]);
        AddFace(mesh, new Point3D(-hw, hh, -hd), new Point3D(hw, hh, -hd), new Point3D(hw, hh, hd), new Point3D(-hw, hh, hd), faces[4]);
        AddFace(mesh, new Point3D(-hw, -hh, hd), new Point3D(hw, -hh, hd), new Point3D(hw, -hh, -hd), new Point3D(-hw, -hh, -hd), faces[5]);

        mesh.Freeze();

        var model = new GeometryModel3D(mesh, material);
        var transform = new Transform3DGroup();
        transform.Children.Add(new TranslateTransform3D(centerX, centerY, 0));
        model.Transform = transform;

        return model;
    }

    private static void AddFace(MeshGeometry3D mesh, Point3D a, Point3D b, Point3D c, Point3D d, Rect uv)
    {
        var index = mesh.Positions.Count;
        mesh.Positions.Add(a);
        mesh.Positions.Add(b);
        mesh.Positions.Add(c);
        mesh.Positions.Add(d);

        mesh.TextureCoordinates.Add(new Point(uv.Left, uv.Top));
        mesh.TextureCoordinates.Add(new Point(uv.Right, uv.Top));
        mesh.TextureCoordinates.Add(new Point(uv.Right, uv.Bottom));
        mesh.TextureCoordinates.Add(new Point(uv.Left, uv.Bottom));

        // WPF 默认剔除背面，顶点顺序必须是逆时针
        mesh.TriangleIndices.Add(index);
        mesh.TriangleIndices.Add(index + 2);
        mesh.TriangleIndices.Add(index + 1);
        mesh.TriangleIndices.Add(index);
        mesh.TriangleIndices.Add(index + 3);
        mesh.TriangleIndices.Add(index + 2);
    }
}
