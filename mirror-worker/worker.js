/**
 * MEME Mirror · Cloudflare Worker 版
 *
 * 免费、常驻、不用开电脑：部署到 Cloudflare Workers 后就是一个永久在线的镜像站。
 * 支持的源和本地版一致，路径规则也一样：/m/{源}/{原始路径}
 */

const SOURCES = {
  neoforge: 'https://bmclapi2.bangbang93.com/maven',
  forge: 'https://maven.minecraftforge.net',
  libraries: 'https://bmclapi2.bangbang93.com/libraries',
  piston: 'https://bmclapi2.bangbang93.com',
  fabric: 'https://bmclapi2.bangbang93.com/fabric-meta',
  assets: 'https://bmclapi2.bangbang93.com/assets',
};

const TITLES = {
  neoforge: 'NeoForge Maven',
  forge: 'Forge Maven',
  libraries: 'Mojang Libraries',
  piston: 'Mojang 元数据',
  fabric: 'Fabric Meta',
  assets: '游戏资源',
};

const CORS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET,HEAD,OPTIONS',
};

export default {
  async fetch(request) {
    const url = new URL(request.url);
    const path = decodeURIComponent(url.pathname);

    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: CORS });
    }

    if (path === '/' || path === '/index.html') {
      return new Response(dashboard(url.origin), {
        headers: { 'Content-Type': 'text/html; charset=utf-8' },
      });
    }

    if (path === '/api/status') {
      return new Response(
        JSON.stringify({
          base: url.origin,
          sources: Object.keys(SOURCES).map((key) => ({
            key,
            title: TITLES[key],
            upstream: SOURCES[key],
            path: `${url.origin}/m/${key}/`,
          })),
        }, null, 2),
        { headers: { 'Content-Type': 'application/json; charset=utf-8', ...CORS } },
      );
    }

    if (!path.startsWith('/m/')) {
      return new Response('404 — 用法: /m/{源}/{路径}', { status: 404, headers: CORS });
    }

    const parts = path.slice(3).split('/');
    const key = parts.shift();
    const rest = parts.join('/');
    const upstreamBase = SOURCES[key];

    if (!upstreamBase || !rest) {
      return new Response('未知源或路径为空', { status: 404, headers: CORS });
    }

    const target = `${upstreamBase}/${rest}${url.search}`;
    const cache = caches.default;

    if (request.method === 'GET') {
      const hit = await cache.match(target);
      if (hit) {
        const response = new Response(hit.body, hit);
        response.headers.set('X-Meme-Cache', 'HIT');
        for (const [k, v] of Object.entries(CORS)) response.headers.set(k, v);
        return response;
      }
    }

    const upstream = await fetch(target, {
      method: request.method,
      headers: { 'User-Agent': 'MemeMirror/1.0 (cloudflare worker)' },
      redirect: 'follow',
    });

    const response = new Response(upstream.body, upstream);
    response.headers.set('X-Meme-Cache', 'MISS');
    for (const [k, v] of Object.entries(CORS)) response.headers.set(k, v);

    if (upstream.ok && request.method === 'GET') {
      const toCache = new Response(upstream.clone().body, upstream);
      toCache.headers.set('Cache-Control', 'public, max-age=86400');
      // waitUntil 在部分运行时不可用，直接 await 更稳
      try {
        await cache.put(target, toCache);
      } catch (_) {
        // 缓存失败不影响本次返回
      }
    }

    return response;
  },
};

function dashboard(origin) {
  const rows = Object.keys(SOURCES)
    .map(
      (key) => `<tr>
        <td><b>${TITLES[key]}</b><div class="sub">/m/${key}/</div></td>
        <td class="mono">${SOURCES[key]}</td>
        <td><a class="btn" href="/m/${key}/">打开</a></td>
      </tr>`,
    )
    .join('');

  return `<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>MEME Mirror · 我的世界镜像站</title>
<style>
*{box-sizing:border-box}
body{margin:0;background:#0f1116;color:#e8ebf1;font-family:system-ui,"Microsoft YaHei",sans-serif}
.wrap{max-width:1000px;margin:0 auto;padding:40px 24px 60px}
h1{font-size:28px;margin:0 0 8px}
.lead{color:#8b93a3;font-size:14px;margin:0 0 28px}
table{width:100%;border-collapse:collapse;background:#1a1e26;border-radius:12px;overflow:hidden}
th,td{padding:14px 16px;text-align:left;font-size:13.5px;border-bottom:1px solid #242a34}
th{background:#151922;color:#8b93a3;font-weight:500;font-size:12px}
tr:last-child td{border-bottom:0}
.sub{color:#6f7787;font-size:11.5px;margin-top:3px;font-family:Consolas,monospace}
.mono{font-family:Consolas,monospace;font-size:11.5px;color:#9aa3b2;word-break:break-all}
.btn{background:#1f2937;color:#8ab4ff;padding:5px 12px;border-radius:6px;text-decoration:none;font-size:12px}
.btn:hover{background:#243043}
code{background:#0b0e13;padding:2px 6px;border-radius:5px;font-family:Consolas,monospace;font-size:12px;color:#8ab4ff}
pre{background:#0b0e13;padding:16px;border-radius:10px;overflow:auto;font-size:12px;color:#a9b4c6;line-height:1.7}
.card{margin-top:28px;background:#151922;border:1px solid #242a34;border-radius:12px;padding:20px}
.card h2{margin:0 0 12px;font-size:15px}
.foot{margin-top:28px;color:#5f6675;font-size:12px}
</style></head><body><div class="wrap">
<h1>MEME Mirror</h1>
<p class="lead">常驻云端镜像 · 给 MEME Launcher 和手动下载用</p>
<table>
  <tr><th>源</th><th>上游</th><th></th></tr>
  ${rows}
</table>

<div class="card">
  <h2>怎么用</h2>
  <p style="color:#8b93a3;font-size:13px;margin:0 0 12px">把原域名换成 <code>${origin}/m/{源}/</code>，路径不变：</p>
  <pre>NeoForge
https://maven.neoforged.net/releases/net/neoforged/bus/8.0.5/bus-8.0.5.jar
→ ${origin}/m/neoforge/net/neoforged/bus/8.0.5/bus-8.0.5.jar

Fabric
https://meta.fabricmc.net/v2/versions/loader
→ ${origin}/m/fabric/v2/versions/loader

Mojang 元数据
https://piston-meta.mojang.com/mc/game/version_manifest_v2.json
→ ${origin}/m/piston/mc/game/version_manifest_v2.json</pre>
  <p style="color:#8b93a3;font-size:13px;margin:14px 0 0">
    启动器里：设置 → 自建镜像站地址，填 <code>${origin}</code>
  </p>
</div>

<p class="foot">MEME Mirror · Cloudflare Worker · 首次请求回源，之后走 Cloudflare 边缘缓存</p>
</div></body></html>`;
}
