using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace Aurora.App;

/// <summary>设置页右侧那个小舞台上的角色。</summary>
public enum SettingsMob
{
    Zombie = 0,
    Piglin = 1,
    Spider = 2,
    Creeper = 3,
}

/// <summary>
/// 设置页右侧的 3D 小场景：一个转盘 + 一只方块生物 + 漂浮的小物件。
/// 全部用代码拼方块，不依赖贴图；点一下换生物。
/// </summary>
public static class SettingsScene
{
    public static string Name(SettingsMob mob) => mob switch
    {
        SettingsMob.Zombie => "僵尸",
        SettingsMob.Piglin => "猪灵",
        SettingsMob.Spider => "蜘蛛",
        SettingsMob.Creeper => "苦力怕",
        _ => "僵尸",
    };

    public static string Hint(SettingsMob mob) => mob switch
    {
        SettingsMob.Zombie => "它在看着你设置内存",
        SettingsMob.Piglin => "猪灵：拿金锭来换（不是）",
        SettingsMob.Spider => "八条腿，比你的启动项还多",
        SettingsMob.Creeper => "别靠太近，会炸的",
        _ => string.Empty,
    };

    public static SettingsMob Next(SettingsMob mob) => (SettingsMob)(((int)mob + 1) % 4);

    public static ModelVisual3D Build(SettingsMob mob)
    {
        // 底座
        var baseGroup = new Model3DGroup();
        baseGroup.Children.Add(Box(0, -0.24, 0, 1.6, 0.48, 1.6, "#FF6FA83F"));
        baseGroup.Children.Add(Box(0, -0.54, 0, 1.6, 0.12, 1.6, "#FF8A5B34"));
        baseGroup.Children.Add(Box(0, -0.63, 0, 1.6, 0.06, 1.6, "#FF6E7278"));

        // 生物（放大一点，别显得太小）
        var mobGroup = new Model3DGroup();
        switch (mob)
        {
            case SettingsMob.Zombie: Zombie(mobGroup); break;
            case SettingsMob.Piglin: Piglin(mobGroup); break;
            case SettingsMob.Spider: Spider(mobGroup); break;
            case SettingsMob.Creeper: Creeper(mobGroup); break;
        }

        // 漂浮的小方块
        var floaters = new Model3DGroup();
        floaters.Children.Add(Box(1.55, 1.55, -0.5, 0.24, 0.24, 0.24, "#FF5BE8E0"));
        floaters.Children.Add(Box(-1.55, 1.95, 0.45, 0.2, 0.2, 0.2, "#FFFFD24A"));
        floaters.Children.Add(Box(1.35, 2.5, 0.7, 0.16, 0.16, 0.16, "#FF4BE07A"));

        var visual = new ModelVisual3D();
        visual.Children.Add(new ModelVisual3D { Content = baseGroup });
        visual.Children.Add(new ModelVisual3D { Content = mobGroup });
        visual.Children.Add(new ModelVisual3D { Content = floaters });

        var axis = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        var rotate = new RotateTransform3D(axis) { CenterX = 0, CenterY = 0.6, CenterZ = 0 };
        var bob = new TranslateTransform3D(0, 0, 0);

        var transforms = new Transform3DGroup();
        transforms.Children.Add(rotate);
        transforms.Children.Add(bob);
        visual.Transform = transforms;

        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(46))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        axis.BeginAnimation(AxisAngleRotation3D.AngleProperty, spin);

        var floatUp = new DoubleAnimation(0.06, -0.06, TimeSpan.FromSeconds(3.8))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        bob.BeginAnimation(TranslateTransform3D.OffsetYProperty, floatUp);

        return visual;
    }
    public static Model3DGroup Lights()
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(0x5E, 0x68, 0x7E)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0xFF, 0xF4, 0xDC), new Vector3D(-1, -2, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x74, 0x96, 0xFF), new Vector3D(1, -0.6, 1)));
        return group;
    }

    /// <summary>正交相机：方块生物的比例才不会被透视拉变形（之前 q 版就是因为广角透视）。</summary>
    public static OrthographicCamera Camera() => new()
    {
        Position = new Point3D(7, 5, 7),
        LookDirection = new Vector3D(-7, -4.3, -7),
        UpDirection = new Vector3D(0, 1, 0),
        Width = 4.6,
    };

    // ---------------- 生物 ----------------

    private static void Zombie(Model3DGroup g)
    {
        // 真实比例：腿 0.75 / 身 0.75 / 头 0.5，总高 2.0 格；手臂挂在肩上（0.75~1.5）
        const string skin = "#FF6C9E4A";
        const string shirt = "#FF00A8A8";
        const string pants = "#FF3B3BB0";

        g.Children.Add(Box(-0.125, 0.375, 0, 0.25, 0.75, 0.25, pants));   // 腿
        g.Children.Add(Box(0.125, 0.375, 0, 0.25, 0.75, 0.25, pants));
        g.Children.Add(Box(0, 1.125, 0, 0.5, 0.75, 0.25, shirt));          // 身体
        g.Children.Add(Box(-0.375, 1.125, 0.02, 0.25, 0.75, 0.25, skin));  // 手臂（略前伸）
        g.Children.Add(Box(0.375, 1.125, 0.02, 0.25, 0.75, 0.25, skin));
        g.Children.Add(Box(-0.375, 0.8, 0.02, 0.25, 0.1, 0.25, "#FF3E7A34"));  // 袖口
        g.Children.Add(Box(0.375, 0.8, 0.02, 0.25, 0.1, 0.25, "#FF3E7A34"));
        g.Children.Add(Box(0, 1.75, 0, 0.5, 0.5, 0.5, skin));              // 头
        g.Children.Add(Box(-0.12, 1.82, 0.26, 0.1, 0.1, 0.03, "#FF16241C")); // 眼
        g.Children.Add(Box(0.12, 1.82, 0.26, 0.1, 0.1, 0.03, "#FF16241C"));
        g.Children.Add(Box(0, 1.64, 0.26, 0.16, 0.05, 0.03, "#FF2C4022"));   // 嘴
    }

    private static void Piglin(Model3DGroup g)
    {
        // 猪灵：腿 0.75 / 身 0.75 / 头 0.5，金腰带 + 猪鼻 + 两侧耳朵
        const string skin = "#FFE0A88E";
        const string body = "#FF6E4638";
        const string pants = "#FF3E2A22";
        const string gold = "#FFE8C25A";

        g.Children.Add(Box(-0.125, 0.375, 0, 0.25, 0.75, 0.25, pants));
        g.Children.Add(Box(0.125, 0.375, 0, 0.25, 0.75, 0.25, pants));
        g.Children.Add(Box(0, 1.125, 0, 0.5, 0.75, 0.25, body));
        g.Children.Add(Box(-0.375, 1.125, 0.02, 0.25, 0.75, 0.25, skin));
        g.Children.Add(Box(0.375, 1.125, 0.02, 0.25, 0.75, 0.25, skin));
        g.Children.Add(Box(0, 0.98, 0.14, 0.54, 0.08, 0.06, gold));        // 腰带
        g.Children.Add(Box(0, 1.75, 0, 0.5, 0.5, 0.5, skin));               // 头
        g.Children.Add(Box(0, 1.70, 0.3, 0.22, 0.16, 0.14, "#FFD08E78"));   // 猪鼻
        g.Children.Add(Box(-0.05, 1.72, 0.38, 0.04, 0.06, 0.02, "#FF9A5E4C"));
        g.Children.Add(Box(0.05, 1.72, 0.38, 0.04, 0.06, 0.02, "#FF9A5E4C"));
        g.Children.Add(Box(-0.27, 1.92, 0, 0.1, 0.16, 0.3, skin));          // 耳朵
        g.Children.Add(Box(0.27, 1.92, 0, 0.1, 0.16, 0.3, skin));
        g.Children.Add(Box(-0.12, 1.86, 0.26, 0.1, 0.08, 0.03, gold));      // 金瞳
        g.Children.Add(Box(0.12, 1.86, 0.26, 0.1, 0.08, 0.03, gold));
    }
    private static void Spider(Model3DGroup g)
    {
        var body = "#FF2A2A2E";
        var leg = "#FF1C1C20";

        g.Children.Add(Box(0, 0.45, -0.1, 0.62, 0.42, 0.7, body));   // 身体
        g.Children.Add(Box(0, 0.42, 0.42, 0.42, 0.34, 0.36, body));  // 头
        g.Children.Add(Box(-0.1, 0.5, 0.61, 0.08, 0.07, 0.03, "#FFE24040"));
        g.Children.Add(Box(0.1, 0.5, 0.61, 0.08, 0.07, 0.03, "#FFE24040"));
        g.Children.Add(Box(-0.06, 0.4, 0.61, 0.06, 0.05, 0.03, "#FFB02020"));
        g.Children.Add(Box(0.06, 0.4, 0.61, 0.06, 0.05, 0.03, "#FFB02020"));

        for (var i = 0; i < 4; i++)
        {
            var z = 0.55 - i * 0.28;
            var spread = 0.42 + i * 0.05;
            AddLeg(g, -spread, z, leg);
            AddLeg(g, spread, z, leg);
        }
    }

    private static void AddLeg(Model3DGroup g, double x, double z, string color)
    {
        // 用两段方块模拟弯折的腿
        g.Children.Add(Box(x * 0.62, 0.5, z, 0.3, 0.08, 0.08, color));
        g.Children.Add(Box(x * 0.98, 0.3, z, 0.08, 0.42, 0.08, color));
    }

    private static void Creeper(Model3DGroup g)
    {
        var green = "#FF5DBB4A";
        var dark = "#FF4CA83C";

        // 腿 0.375 高，身体 0.75，头 0.5 → 总高 1.625（接近真实的 1.7）
        g.Children.Add(Box(-0.125, 0.1875, 0.06, 0.25, 0.375, 0.25, dark));
        g.Children.Add(Box(0.125, 0.1875, 0.06, 0.25, 0.375, 0.25, dark));
        g.Children.Add(Box(-0.125, 0.1875, -0.06, 0.25, 0.375, 0.25, dark));
        g.Children.Add(Box(0.125, 0.1875, -0.06, 0.25, 0.375, 0.25, dark));

        g.Children.Add(Box(0, 0.75, 0, 0.5, 0.75, 0.25, green));   // 身体
        g.Children.Add(Box(0, 1.375, 0, 0.5, 0.5, 0.5, green));    // 头

        // 苦力怕的脸：两只方眼 + 下方那张标志性的嘴
        g.Children.Add(Box(-0.12, 1.46, 0.26, 0.12, 0.12, 0.03, "#FF141A14"));
        g.Children.Add(Box(0.12, 1.46, 0.26, 0.12, 0.12, 0.03, "#FF141A14"));
        g.Children.Add(Box(0, 1.32, 0.26, 0.1, 0.14, 0.03, "#FF141A14"));
        g.Children.Add(Box(-0.06, 1.23, 0.26, 0.06, 0.08, 0.03, "#FF141A14"));
        g.Children.Add(Box(0.06, 1.23, 0.26, 0.06, 0.08, 0.03, "#FF141A14"));
    }
    // ---------------- 方块 ----------------

    private static GeometryModel3D Box(double cx, double cy, double cz,
        double w, double h, double d, string color)
    {
        var material = Solid(color);
        var x0 = cx - w / 2;
        var x1 = cx + w / 2;
        var y0 = cy - h / 2;
        var y1 = cy + h / 2;
        var z0 = cz - d / 2;
        var z1 = cz + d / 2;

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

    private static Material Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return new DiffuseMaterial(brush);
    }
}
