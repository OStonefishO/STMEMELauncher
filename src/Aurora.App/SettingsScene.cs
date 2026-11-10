using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace Aurora.App;

/// <summary>设置页右侧那个小舞台上的角色。</summary>
public enum SettingsMob
{
    Zombie = 0,
    Pig = 1,
    Creeper = 2,
    Slime = 3,
    Wither = 4,
}

/// <summary>
/// 设置页右侧的 3D 小场景：草地底座 + 一只方块生物（纯几何、纯色，和启动器风格一致）。
/// 尺寸按原版比例（16px = 1 格），部件之间留缝不重叠；慢速自转 + 轻微浮动，点一下换角色。
/// </summary>
public static class SettingsScene
{
    public static string Name(SettingsMob mob) => mob switch
    {
        SettingsMob.Zombie => "僵尸",
        SettingsMob.Pig => "猪",
        SettingsMob.Creeper => "苦力怕",
        SettingsMob.Slime => "史莱姆",
        SettingsMob.Wither => "凋灵",
        _ => "僵尸",
    };

    public static string Hint(SettingsMob mob) => mob switch
    {
        SettingsMob.Zombie => "它在看着你设置内存",
        SettingsMob.Pig => "猪：存档里的第一位朋友",
        SettingsMob.Creeper => "别靠太近，会炸的",
        SettingsMob.Slime => "蹦跶、分裂、再蹦跶",
        SettingsMob.Wither => "三个头一起盯着你调内存",
        _ => string.Empty,
    };

    public static SettingsMob Next(SettingsMob mob) => (SettingsMob)(((int)mob + 1) % 5);

    public static ModelVisual3D Build(SettingsMob mob)
    {
        var root = new ModelVisual3D();

        // 底座
        var baseGroup = new Model3DGroup();
        baseGroup.Children.Add(Box(0, -0.26, 0, 1.6, 0.5, 1.6, "#FF6FA83F"));
        baseGroup.Children.Add(Box(0, -0.56, 0, 1.6, 0.1, 1.6, "#FF8A5B34"));
        baseGroup.Children.Add(Box(0, -0.64, 0, 1.6, 0.06, 1.6, "#FF6E7278"));
        root.Children.Add(new ModelVisual3D { Content = baseGroup });

        // 生物
        var mobGroup = new Model3DGroup();
        switch (mob)
        {
            case SettingsMob.Zombie: Zombie(mobGroup); break;
            case SettingsMob.Pig: Pig(mobGroup); break;
            case SettingsMob.Creeper: Creeper(mobGroup); break;
            case SettingsMob.Slime: Slime(mobGroup); break;
            case SettingsMob.Wither: Wither(mobGroup); break;
        }

        root.Children.Add(new ModelVisual3D { Content = mobGroup });

        // 漂浮小方块
        var floaters = new Model3DGroup();
        floaters.Children.Add(Box(1.5, 1.55, -0.55, 0.22, 0.22, 0.22, "#FF5BE8E0"));
        floaters.Children.Add(Box(-1.5, 1.95, 0.5, 0.2, 0.2, 0.2, "#FFFFD24A"));
        floaters.Children.Add(Box(1.3, 2.45, 0.75, 0.16, 0.16, 0.16, "#FF4BE07A"));
        root.Children.Add(new ModelVisual3D { Content = floaters });

        var axis = new AxisAngleRotation3D(new Vector3D(0, 1, 0), 0);
        var rotate = new RotateTransform3D(axis) { CenterX = 0, CenterY = 0, CenterZ = 0 };
        var bob = new TranslateTransform3D(0, 0, 0);

        var transforms = new Transform3DGroup();
        transforms.Children.Add(rotate);
        transforms.Children.Add(bob);
        root.Transform = transforms;

        var spin = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(48))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        axis.BeginAnimation(AxisAngleRotation3D.AngleProperty, spin);

        var floatUp = new DoubleAnimation(0.04, -0.04, TimeSpan.FromSeconds(4))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        bob.BeginAnimation(TranslateTransform3D.OffsetYProperty, floatUp);

        return root;
    }

    // ---------------- 僵尸（总高 1.95） ----------------

    private static void Zombie(Model3DGroup g)
    {
        const string skin = "#FF6C9E4A";
        const string skinDark = "#FF587F3B";
        const string shirt = "#FF12A6A6";
        const string pants = "#FF3B3BB0";

        // 腿：0 ~ 0.75
        g.Children.Add(Box(-0.135, 0.375, 0, 0.24, 0.75, 0.24, pants));
        g.Children.Add(Box(0.135, 0.375, 0, 0.24, 0.75, 0.24, pants));
        // 身体：0.75 ~ 1.45
        g.Children.Add(Box(0, 1.10, 0, 0.5, 0.7, 0.25, shirt));
        // 手臂：0.75 ~ 1.45，略微前伸
        g.Children.Add(Box(-0.375, 1.10, 0.02, 0.24, 0.7, 0.24, skin));
        g.Children.Add(Box(0.375, 1.10, 0.02, 0.24, 0.7, 0.24, skin));
        // 袖子
        g.Children.Add(Box(-0.375, 1.36, 0.02, 0.25, 0.16, 0.25, shirt));
        g.Children.Add(Box(0.375, 1.36, 0.02, 0.25, 0.16, 0.25, shirt));
        // 头：1.45 ~ 1.95
        g.Children.Add(Box(0, 1.70, 0, 0.5, 0.5, 0.5, skin));
        // 脸（贴在头前面）
        g.Children.Add(Box(-0.115, 1.76, 0.255, 0.1, 0.09, 0.02, "#FF14202A"));
        g.Children.Add(Box(0.115, 1.76, 0.255, 0.1, 0.09, 0.02, "#FF14202A"));
        g.Children.Add(Box(0, 1.60, 0.255, 0.17, 0.05, 0.02, "#FF2C4022"));
        // 额头的破损
        g.Children.Add(Box(0.1, 1.9, 0.252, 0.12, 0.07, 0.02, skinDark));
    }

    // ---------------- 猪（身高 0.9，身长 1.4） ----------------

    private static void Pig(Model3DGroup g)
    {
        const string skin = "#FFF0A5A2";
        const string skinDark = "#FFD98E8B";
        const string snout = "#FFE08E8C";

        // 身体：0.35 ~ 0.85，前后长 0.9
        g.Children.Add(Box(0, 0.60, -0.05, 0.5, 0.5, 0.9, skin));
        // 四条腿：0 ~ 0.35
        foreach (var (x, z) in new[] { (-0.16, 0.3), (0.16, 0.3), (-0.16, -0.38), (0.16, -0.38) })
        {
            g.Children.Add(Box(x, 0.175, z, 0.2, 0.35, 0.2, skinDark));
        }

        // 头：0.45 ~ 0.95，在前面
        g.Children.Add(Box(0, 0.70, 0.62, 0.5, 0.5, 0.45, skin));
        // 鼻子
        g.Children.Add(Box(0, 0.66, 0.87, 0.24, 0.18, 0.06, snout));
        g.Children.Add(Box(-0.05, 0.66, 0.90, 0.05, 0.06, 0.02, "#FFB26A68"));
        g.Children.Add(Box(0.05, 0.66, 0.90, 0.05, 0.06, 0.02, "#FFB26A68"));
        // 眼睛
        g.Children.Add(Box(-0.14, 0.80, 0.85, 0.08, 0.07, 0.02, "#FF2A2A2A"));
        g.Children.Add(Box(0.14, 0.80, 0.85, 0.08, 0.07, 0.02, "#FF2A2A2A"));
        // 耳朵
        g.Children.Add(Box(-0.2, 0.95, 0.60, 0.12, 0.12, 0.1, skinDark));
        g.Children.Add(Box(0.2, 0.95, 0.60, 0.12, 0.12, 0.1, skinDark));
    }

    // ---------------- 苦力怕（总高 1.625） ----------------

    private static void Creeper(Model3DGroup g)
    {
        const string green = "#FF5DBB4A";
        const string greenDark = "#FF4CA83C";

        // 四条腿：0 ~ 0.375
        foreach (var (x, z) in new[] { (-0.13, 0.13), (0.13, 0.13), (-0.13, -0.13), (0.13, -0.13) })
        {
            g.Children.Add(Box(x, 0.1875, z, 0.24, 0.375, 0.24, greenDark));
        }

        // 身体：0.375 ~ 1.125
        g.Children.Add(Box(0, 0.75, 0, 0.5, 0.75, 0.26, green));
        // 头：1.125 ~ 1.625
        g.Children.Add(Box(0, 1.375, 0, 0.5, 0.5, 0.5, green));

        // 那张标志性的脸
        g.Children.Add(Box(-0.12, 1.45, 0.252, 0.12, 0.12, 0.02, "#FF14201A"));
        g.Children.Add(Box(0.12, 1.45, 0.252, 0.12, 0.12, 0.02, "#FF14201A"));
        g.Children.Add(Box(0, 1.31, 0.252, 0.1, 0.15, 0.02, "#FF14201A"));
        g.Children.Add(Box(-0.07, 1.20, 0.252, 0.06, 0.07, 0.02, "#FF14201A"));
        g.Children.Add(Box(0.07, 1.20, 0.252, 0.06, 0.07, 0.02, "#FF14201A"));
    }

    // ---------------- 史莱姆（总高 0.9） ----------------

    private static void Slime(Model3DGroup g)
    {
        const string outer = "#B04BD07A";
        const string core = "#FF2F9A50";

        // 外层（半透明感的浅绿）+ 内芯
        g.Children.Add(Box(0, 0.45, 0, 0.9, 0.9, 0.9, outer));
        g.Children.Add(Box(0, 0.42, 0, 0.5, 0.5, 0.5, core));

        // 眼睛和嘴
        g.Children.Add(Box(-0.15, 0.55, 0.452, 0.12, 0.12, 0.02, "#FF1B3B26"));
        g.Children.Add(Box(0.15, 0.55, 0.452, 0.12, 0.12, 0.02, "#FF1B3B26"));
        g.Children.Add(Box(0, 0.38, 0.452, 0.16, 0.06, 0.02, "#FF1B3B26"));
    }

    // ---------------- 凋灵（总高 3.4） ----------------

    private static void Wither(Model3DGroup g)
    {
        const string bone = "#FF4A4A52";
        const string boneDark = "#FF33333A";
        const string eye = "#FFD83A3A";

        // 下面的三根垂条：0 ~ 0.5（让它站在底座上）
        g.Children.Add(Box(-0.3, 0.25, -0.1, 0.08, 0.5, 0.08, boneDark));
        g.Children.Add(Box(0, 0.25, -0.1, 0.08, 0.5, 0.08, boneDark));
        g.Children.Add(Box(0.3, 0.25, -0.1, 0.08, 0.5, 0.08, boneDark));

        // 身体：0.5 ~ 1.5
        g.Children.Add(Box(0, 1.0, 0, 0.75, 1.0, 0.5, bone));
        // 肋骨
        for (var i = 0; i < 3; i++)
        {
            g.Children.Add(Box(0, 0.75 + i * 0.24, 0.26, 0.7, 0.06, 0.03, boneDark));
        }

        // 三个头：1.5 ~ 1.96
        foreach (var x in new[] { -0.42, 0.0, 0.42 })
        {
            g.Children.Add(Box(x, 1.73, 0.05, 0.46, 0.46, 0.46, bone));
            g.Children.Add(Box(x - 0.1, 1.78, 0.29, 0.08, 0.07, 0.02, eye));
            g.Children.Add(Box(x + 0.1, 1.78, 0.29, 0.08, 0.07, 0.02, eye));
        }
    }
    // ---------------- 灯 & 相机 ----------------

    public static Model3DGroup Lights()
    {
        var group = new Model3DGroup();
        group.Children.Add(new AmbientLight(Color.FromRgb(0x60, 0x6A, 0x80)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0xFF, 0xF6, 0xE2), new Vector3D(-1, -2, -1)));
        group.Children.Add(new DirectionalLight(Color.FromRgb(0x74, 0x94, 0xF0), new Vector3D(1, -0.6, 1)));
        return group;
    }

    public static OrthographicCamera Camera() => new()
    {
        Position = new Point3D(7, 5, 7),
        LookDirection = new Vector3D(-7, -4.6, -7),
        UpDirection = new Vector3D(0, 1, 0),
        Width = 5.0,
    };

    // ---------------- 方块 ----------------

    private static GeometryModel3D Box(double cx, double cy, double cz,
        double w, double h, double d, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        var material = new DiffuseMaterial(brush);

        var x0 = cx - w / 2;
        var x1 = cx + w / 2;
        var y0 = cy - h / 2;
        var y1 = cy + h / 2;
        var z0 = cz - d / 2;
        var z1 = cz + d / 2;

        var mesh = new MeshGeometry3D();
        AddQuad(mesh, new Point3D(x0, y1, z0), new Point3D(x0, y1, z1), new Point3D(x1, y1, z1), new Point3D(x1, y1, z0), new Vector3D(0, 1, 0));
        AddQuad(mesh, new Point3D(x0, y0, z0), new Point3D(x1, y0, z0), new Point3D(x1, y0, z1), new Point3D(x0, y0, z1), new Vector3D(0, -1, 0));
        AddQuad(mesh, new Point3D(x0, y0, z1), new Point3D(x1, y0, z1), new Point3D(x1, y1, z1), new Point3D(x0, y1, z1), new Vector3D(0, 0, 1));
        AddQuad(mesh, new Point3D(x1, y0, z0), new Point3D(x0, y0, z0), new Point3D(x0, y1, z0), new Point3D(x1, y1, z0), new Vector3D(0, 0, -1));
        AddQuad(mesh, new Point3D(x1, y0, z1), new Point3D(x1, y0, z0), new Point3D(x1, y1, z0), new Point3D(x1, y1, z1), new Vector3D(1, 0, 0));
        AddQuad(mesh, new Point3D(x0, y0, z0), new Point3D(x0, y0, z1), new Point3D(x0, y1, z1), new Point3D(x0, y1, z0), new Vector3D(-1, 0, 0));
        mesh.Freeze();

        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private static void AddQuad(MeshGeometry3D mesh, Point3D a, Point3D b, Point3D c, Point3D d, Vector3D normal)
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
}
