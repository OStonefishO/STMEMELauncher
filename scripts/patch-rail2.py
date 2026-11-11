import io
p = r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Ui.cs"
s = io.open(p, encoding="utf-8", newline="").read()

# 1) 彻底关掉侧栏悬停展开（经典/纸质都会压到内容）
s = s.replace('''        // 只有经典布局的左侧图标栏才做「悬停展开」；其它布局展开会压到内容上，引发来回闪
        if (_style != "classic" || RailColumn == null || RailPanel == null || _railExpanded == expanded)
        {
            return;
        }''','''        // 侧栏「悬停展开」会压到内容上（经典/纸质都中招），直接停用，保持固定 74px
        if (RailColumn == null || RailPanel == null || _railExpanded == expanded)
        {
            return;
        }

        return;''', 1)

# 2) 左内边距只给纸质（经典页面本来就在 grid 的第二列，不需要额外留白）
s = s.replace("        SetPagesLeftInset(useLeft ? 84 : 0);", "        SetPagesLeftInset(_paper ? 84 : 0);", 1)
io.open(p, "w", encoding="utf-8", newline="").write(s)
print("rail expansion disabled, inset only for paper")
