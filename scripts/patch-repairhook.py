import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 启动前体检：assets + libraries 一起补
s = s.replace('''               // 先把缺的库补齐（Forge/NeoForge 最容易缺 bootstraplauncher 这类核心库）
               SetStatus("检查版本文件…");''','''               // 先体检：assets 对象 / libraries 缺失或大小不对就补回来
               SetStatus("检查游戏资源…");
               try
               {
                   var installer = _service.CreateInstaller();
                   var repaired = await installer.RepairAsync(
                       versionInfo.Id,
                       new Progress<Aurora.Core.Models.InstallProgress>(p =>
                       {
                           if (!string.IsNullOrEmpty(p.Message))
                           {
                               SetStatus(p.Message);
                           }
                       }));
                   if (repaired > 0)
                   {
                       SetStatus($"已补回 {repaired} 个资源文件");
                   }
               }
               catch (Exception ex)
               {
                   SetStatus("资源体检跳过：" + ex.Message);
               }

               // 再把缺的库补齐（Forge/NeoForge 最容易缺 bootstraplauncher 这类核心库）
               SetStatus("检查版本文件…");''', 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("repair hooked into launch")
