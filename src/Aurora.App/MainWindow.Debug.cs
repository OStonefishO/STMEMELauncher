using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aurora.App;

public partial class MainWindow
{
    /// <summary>调试：造一条真实的崩溃日志，走一遍真正的分析器。</summary>
    private static Aurora.Core.Launch.CrashInsight BuildDemoCrash()
    {
        var lines = new List<string>
        {
            "[main/INFO]: Loading Minecraft 1.20.1 with Fabric Loader 0.15.11",
            "[main/INFO]: Loading 87 mods:",
            "\t- create 0.5.1.j",
            "[main/WARN]: Mod resolution failed",
            "[main/ERROR]: Missing or unsupported mandatory dependencies:",
            "\tMod ID: 'create_fabric', Requested by: 'create', Expected range: '[0.5.1,)', Actual version: '[MISSING]'",
            "\tMod ID: 'flywheel', Requested by: 'create', Expected range: '[0.6.10,)', Actual version: '[MISSING]'",
            "java.lang.NoClassDefFoundError: com/simibubi/create/foundation/utility/RecipeUtils",
            "\tat com.simibubi.create.Create.onInitialize(Create.java:142)",
            "[main/ERROR]: Minecraft has crashed!"
        };

        return Aurora.Core.Launch.CrashInspector.Analyze(
            Path.Combine(Path.GetTempPath(), "meme-demo"),
            1,
            lines,
            DateTimeOffset.Now.AddMinutes(-3));
    }

    /// <summary>调试：内容页自动打开第一个项目的详情（截图用）。</summary>
    private async Task OpenFirstContentDetailAsync()
    {
        try
        {
            await Task.Delay(2500);
            if (ContentList?.Items.Count > 0 && ContentList.Items[0] is ContentCard card)
            {
                await ShowContentDetailAsync(card.Item);

                // 调试：--demo-detail 之后自动点一次「翻译为中文」
                if (_demoTranslate)
                {
                    await Task.Delay(800);
                    OnDetailTranslate(this, new RoutedEventArgs());
                }
            }
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>调试：把设置页滚到底，方便截图看水印那块。</summary>
    private void ScrollSettingsToBottom()
    {
        try
        {
            var viewer = FindFirstScrollViewer(SettingsPage);
            viewer?.ScrollToEnd();
        }
        catch
        {
            // 忽略
        }
    }

    private static ScrollViewer? FindFirstScrollViewer(DependencyObject? root)
    {
        if (root == null)
        {
            return null;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }

            var nested = FindFirstScrollViewer(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
