using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Aurora.App;

/// <summary>
/// 主界面那个缓慢自转的方块岛：全部用代码生成 MeshGeometry3D，
/// 不依赖任何资源文件，也没有外部模型。
/// </summary>
public static class HeroScene
{
    private const double Size = 1.0;

    public static ModelVisual3D Build()
    {
        var group = new Model3DGroup();

        // ---- 地形：7x7 草地，边缘往下掉 ----
        for (var x = -3; x <= 3; x++)
        {
            for (var z = -3; z <= 3; z++)
            {
                if (Math.Abs(x) == 3 && Math.Abs(z) == 3)
                {
                    continue; // 切掉四个角，轮廓更圆
                }

                var edge = Math.Max(Math.Abs(x), Math.Abs(z));
                var depth = edge switch { 3 => 3, 2 => 2, _ => 1 };

                group.Children.Add(Cube(new Point3D(x, -0.5, z), Grass()));
                for (var d = 1; d < depth; d++)
                {
                    group.Children.Add(Cube(new Point3D(x, -0.5 - d, z), d == depth - 1 ? Stone() : Dirt()));
                }
            }
        }

        // ---- 两棵小树 ----
        Tree(group, new Point3D(-1, 0, -1));
        Tree(group, new Point3D(2, 0, 1), scale: 0.9);

        // ---- 漂浮的小方块，增加层次 ----
        group.Children.Add(Cube(new Point3D(4.6, 2.4, -1.2), Accent("#FF6FA8FF"), Size * 0.55));
        group.Children.Add(Cube(new Point3D(-4.2, 3.2, 1.6), Accent("#FF9B7CFF"), Size * 0.45));
        group.Children.Add(Cube(new Point3D(3.1, 3.9, 2.6), Accent("#FF57C1A8"), Size * 0.32));

        var visual = new ModelVisual3D { Content = group };

        // 绕 Y 轴慢转 + 上下轻微浮动，静止的模型看起来像"悬浮着"
        var axis = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        var rotate = new RotateTransform3D(axis) { CenterX = 0, CenterY = -1, CenterZ = 0 };
        var bob = new TranslateTransform3D(0, 0, 0);

        var group3D = new Transform3DGroup();
        group3D.Children.Add(rotate);
        group3D.Children.Add(bob);
        visual.Transform = group3D;

        var spin = new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromSeconds(96))
        {
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
        };
        axis.BeginAnimation(AxisAngleRotation3D.AngleProperty, spin);

        var floatUp = new System.Windows.Media.Animation.DoubleAnimation(0.32, -0.32, TimeSpan.FromSeconds(4.5))
        {
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new System.Windows.Media.Animation.SineEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut,
            },
        };
        bob.BeginAnimation(TranslateTransform3D.OffsetYProperty, floatUp);

        return visual;
    }

    public static OrthographicCamera Camera()
    {
        return new OrthographicCamera
        {
            Position = new Point3D(14, 13, 14),
            LookDirection = new Vector3D(-14, -13, -14),
            UpDirection = new Vector3D(0, 1, 0),
            Width = 17.2,
        };
    }

    public static Model3DGroup Lights()
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(0x5A, 0x64, 0x78)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0xFF, 0xF3, 0xD6), new Vector3D(-1, -2, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x66, 0x8C, 0xFF), new Vector3D(1, -0.6, 1)));
        return group;
    }

    // ---------------- 方块 ----------------

    private static void Tree(Model3DGroup group, Point3D basePoint, double scale = 1.0)
    {
        for (var i = 0; i < 3; i++)
        {
            group.Children.Add(Cube(new Point3D(basePoint.X, basePoint.Y + i + 0.5, basePoint.Z), Wood(), scale));
        }

        var leafY = basePoint.Y + 3.5;
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                if (Math.Abs(dx) == 1 && Math.Abs(dz) == 1)
                {
                    continue;
                }

                group.Children.Add(Cube(new Point3D(basePoint.X + dx, leafY, basePoint.Z + dz), Leaves(), scale));
            }
        }

        group.Children.Add(Cube(new Point3D(basePoint.X, leafY + 1, basePoint.Z), Leaves(), scale * 0.95));
    }

    private static GeometryModel3D Cube(Point3D center, Material material, double scale = Size)
    {
        var h = scale / 2.0;
        var x0 = center.X - h;
        var x1 = center.X + h;
        var y0 = center.Y - h;
        var y1 = center.Y + h;
        var z0 = center.Z - h;
        var z1 = center.Z + h;

        var mesh = new MeshGeometry3D();

        AddFace(mesh, new Point3D(x0, y1, z0), new Point3D(x0, y1, z1), new Point3D(x1, y1, z1), new Point3D(x1, y1, z0), new Vector3D(0, 1, 0));
        AddFace(mesh, new Point3D(x0, y0, z0), new Point3D(x1, y0, z0), new Point3D(x1, y0, z1), new Point3D(x0, y0, z1), new Vector3D(0, -1, 0));
        AddFace(mesh, new Point3D(x0, y0, z1), new Point3D(x1, y0, z1), new Point3D(x1, y1, z1), new Point3D(x0, y1, z1), new Vector3D(0, 0, 1));
        AddFace(mesh, new Point3D(x1, y0, z0), new Point3D(x0, y0, z0), new Point3D(x0, y1, z0), new Point3D(x1, y1, z0), new Vector3D(0, 0, -1));
        AddFace(mesh, new Point3D(x1, y0, z1), new Point3D(x1, y0, z0), new Point3D(x1, y1, z0), new Point3D(x1, y1, z1), new Vector3D(1, 0, 0));
        AddFace(mesh, new Point3D(x0, y0, z0), new Point3D(x0, y0, z1), new Point3D(x0, y1, z1), new Point3D(x0, y1, z0), new Vector3D(-1, 0, 0));

        mesh.Freeze();
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static void AddFace(MeshGeometry3D mesh, Point3D a, Point3D b, Point3D c, Point3D d, Vector3D normal)
    {
        var start = mesh.Positions.Count;
        mesh.Positions.Add(a);
        mesh.Positions.Add(b);
        mesh.Positions.Add(c);
        mesh.Positions.Add(d);

        for (var i = 0; i < 4; i++)
        {
            mesh.Normals.Add(normal);
        }

        mesh.TriangleIndices.Add(start);
        mesh.TriangleIndices.Add(start + 1);
        mesh.TriangleIndices.Add(start + 2);
        mesh.TriangleIndices.Add(start);
        mesh.TriangleIndices.Add(start + 2);
        mesh.TriangleIndices.Add(start + 3);
    }

    // ---------------- 材质 ----------------

    private static Material Grass() => Gradient(
        "#FF7FC24A", "#FF6FA83F", "#FF8A5B34");

    private static Material Dirt() => Solid("#FF8A5B34");

    private static Material Stone() => Solid("#FF6E7278");

    private static Material Wood() => Solid("#FF6B4A2A");

    private static Material Leaves() => Solid("#FF3E8E41");

    private static Material Accent(string hex) => Solid(hex);

    private static Material Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return new DiffuseMaterial(brush);
    }

    /// <summary>顶面偏亮、侧面略暗的渐变，让方块有“受光”的感觉。</summary>
    private static Material Gradient(string top, string side, string dark)
    {
        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(top),
            (Color)ColorConverter.ConvertFromString(side),
            new Point(0.5, 0),
            new Point(0.5, 1));
        brush.Freeze();
        return new DiffuseMaterial(brush);
    }
}
