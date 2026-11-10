import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()
s = s.replace("""        _dashSceneReady = true;
        DashViewport.Camera = HeroScene.Camera();""", """        _dashSceneReady = true;
        var cam = HeroScene.Camera();
        cam.Width *= 1.45;   // 小卡片里把视野放宽一点，岛不会被裁掉
        DashViewport.Camera = cam;""")
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("dash camera widened")
