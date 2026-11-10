import io, os, sys

p = r"D:\综合创作\MCLauncher\docs\index.html"
html = io.open(p, encoding="utf-8").read()

CSS = """/* sac alert + steps */
.alert{display:flex;align-items:center;gap:14px;margin:0 0 8px;padding:16px 20px;border-radius:16px;
  border:1px solid rgba(255,170,80,.45);background:linear-gradient(135deg,rgba(255,160,60,.16),rgba(255,80,60,.08))}
.alert .ico{width:38px;height:38px;border-radius:11px;display:grid;place-items:center;font-size:19px;
  background:rgba(255,170,80,.2);border:1px solid rgba(255,170,80,.4)}
.alert b{color:#ffd9a8}
.alert p{color:var(--dim);font-size:13.4px;margin-top:2px}
.alert a{margin-left:auto;white-space:nowrap}
.steps{display:grid;grid-template-columns:1.1fr .9fr;gap:22px;align-items:start}
.step{display:flex;gap:14px;padding:16px 18px;border-radius:14px;border:1px solid var(--line);background:rgba(255,255,255,.03);margin-bottom:12px}
.step .n{width:28px;height:28px;border-radius:9px;flex:0 0 auto;display:grid;place-items:center;font-weight:700;font-size:13px;
  background:linear-gradient(135deg,var(--purple),var(--cyan));color:#fff}
.step h4{font-size:14px;margin-bottom:4px}
.step p{color:var(--dim);font-size:13.2px}
.steps img{width:100%;border-radius:14px;border:1px solid var(--line);box-shadow:0 20px 50px rgba(0,0,0,.5)}
@media(max-width:900px){.steps{grid-template-columns:1fr}.alert a{margin-left:0}}

/* support */"""

ALERT = """  <div class="wrap" style="margin-top:-18px">
    <div class="alert rv">
      <div class="ico">⚠️</div>
      <div>
        <b>下载后双击没反应、提示「智能应用控制已阻止」？</b>
        <p>不是病毒，是本作没有代码签名证书。30 秒设置一下就能跑起来。</p>
      </div>
      <a class="btn btn-primary" href="#sac" style="padding:10px 18px">看解决办法</a>
    </div>
  </div>

  <section id="features" class="wrap">"""

SECTION = """  <section id="sac" class="wrap">
    <div class="sec-head rv">
      <div class="eyebrow">BLOCKED?</div>
      <h2>被 Windows 拦住了？</h2>
      <p>Windows 11 的「智能应用控制」只放行有数字签名的程序，本作没有买证书（免费签名申请未通过），所以会被拦。按下面关掉即可。</p>
    </div>
    <div class="steps">
      <div>
        <div class="step rv"><div class="n">1</div><div>
          <h4>打开 Windows 安全中心</h4>
          <p>开始菜单搜「Windows 安全中心」，或按 Win 键直接输入「安全」。</p>
        </div></div>
        <div class="step rv"><div class="n">2</div><div>
          <h4>进入「应用和浏览器控制」</h4>
          <p>在左侧列表点它，再点「智能应用控制设置」。</p>
        </div></div>
        <div class="step rv"><div class="n">3</div><div>
          <h4>把智能应用控制关掉</h4>
          <p>选择「关闭」并确认。<b style="color:#ffcf9a">注意：关掉后要重装系统才能再打开</b>，介意的话换一台没开这个功能的电脑。</p>
        </div></div>
        <p style="color:var(--faint);font-size:12.5px">
          完整图文（含「不关 SAC 的替代做法」）见
          <a href="https://github.com/OStonefishO/STMEMELauncher/blob/main/docs/SMART-APP-CONTROL.md" target="_blank" rel="noopener" style="color:#a99bff">SMART-APP-CONTROL.md</a>。
        </p>
      </div>
      <div class="rv">
        <img src="assets/sac-settings.png" alt="Windows 安全中心">
        <div class="note" style="margin-top:12px">
          💡 关闭后如果还是打不开：右键 exe → 属性 → 底部看有没有「解除锁定」并勾上；或者把 exe 从下载目录挪到 D 盘再试。
        </div>
      </div>
    </div>
  </section>

  <section id="download" class="wrap">"""

assert "/* support */" in html
html = html.replace("/* support */", CSS, 1)

assert '<section id="features" class="wrap">' in html
html = html.replace('  <section id="features" class="wrap">', ALERT, 1)

assert '  <section id="download" class="wrap">' in html
html = html.replace('  <section id="download" class="wrap">', SECTION, 1)

html = html.replace(
    '      <a href="#support" style="color:#b9a3ff">支持</a>',
    '      <a href="#sac" style="color:#ffcf9a">被拦了？</a>\n      <a href="#support" style="color:#b9a3ff">支持</a>', 1)

html = html.replace(
    '      <p>单文件 exe，双击即用。国内建议走 Gitee 镜像。</p>',
    '      <p>单文件 exe，双击即用。国内建议走 Gitee 镜像。<br><span style="color:#ffcf9a">第一次打不开？先看上面的「被 Windows 拦住了？」。</span></p>', 1)

io.open(p, "w", encoding="utf-8", newline="").write(html)
print("ok, len:", len(html))
print("sac section:", 'id="sac"' in html, "alert:", 'class="alert rv"' in html)
