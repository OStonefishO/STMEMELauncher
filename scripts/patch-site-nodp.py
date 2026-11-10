import io, os
p = r"D:\综合创作\MCLauncher\docs\index.html"
h = io.open(p, encoding="utf-8").read()
h = h.replace("""      <div class="card rv"><span class="tag">新</span><span class="em"><img src="assets/icons/book.png" alt=""></span>
        <h3>创作：整合包 / 数据包</h3>
        <p>自选要打包的内容导出整合包（附带 README 与元数据）；数据包按版本自动算 pack_format，函数、配方、进度、tick 模板开箱即用。</p>
      </div>""", """      <div class="card rv"><span class="tag">新</span><span class="em"><img src="assets/icons/chest.png" alt=""></span>
        <h3>创作：一键导出整合包</h3>
        <p>选一个版本，勾上想带走的内容（模组 / 配置 / 存档 / 资源包 / 光影…），改好名字、简介和版本号，直接导出 zip，附 README 与元数据。</p>
      </div>""")
h = h.replace("""      <div class="gitem rv"><img src="assets/create-data.png" alt="数据包制作"><span>数据包制作</span></div>\n""", "")
io.open(p, "w", encoding="utf-8", newline="").write(h)
for f in ("create-data.png",):
    fp = os.path.join(r"D:\综合创作\MCLauncher\docs\assets", f)
    if os.path.exists(fp): os.remove(fp)
print("ok", "数据包" in h)
