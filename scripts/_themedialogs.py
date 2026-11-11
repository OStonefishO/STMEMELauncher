import io

targets = [
    r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.xaml.cs",
    r"D:\综合创作\MCLauncher\src\Aurora.App\MainWindow.Java.cs",
]

for path in targets:
    s = io.open(path, encoding="utf-8", newline="").read()
    out = []
    i = 0
    inserted = 0
    while True:
        idx = s.find("var window = new Window", i)
        if idx < 0:
            out.append(s[i:])
            break
        # find end of object initializer
        brace = s.find("{", idx)
        depth = 0
        j = brace
        while j < len(s):
            if s[j] == "{":
                depth += 1
            elif s[j] == "}":
                depth -= 1
                if depth == 0:
                    break
            j += 1
        end = s.find(";", j) + 1
        block = s[i:end]
        out.append(block)
        out.append("\n\n        StyleDialogWindow(window);")
        inserted += 1
        i = end
    new = "".join(out)
    if new != s:
        io.open(path, "w", encoding="utf-8", newline="").write(new)
    print(path.split("\\")[-1], "inserted", inserted)
