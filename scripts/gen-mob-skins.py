"""生成四张 64x64 的"类原版"生物皮肤（自绘，用于 WPF 3D 贴图盒）。
布局遵循 Minecraft 皮肤的标准 box-UV：
  头 (0,0) 8x8x8 / 身 (16,16) 8x12x4 / 臂 (40,16) 4x12x4 / 腿 (0,16) 4x12x4
苦力怕、蜘蛛用同一套 box-UV，只是尺寸不同。
"""
import os, random
from PIL import Image

OUT = r"D:\综合创作\MCLauncher\src\Aurora.App\Assets\mobs"
os.makedirs(OUT, exist_ok=True)


def new():
    return Image.new("RGBA", (64, 64), (0, 0, 0, 0))


def box(im, x, y, w, h, d, top, side, front, bottom=None, back=None, left=None, right=None):
    """按标准 box-UV 画一个长方体区域。"""
    bottom = bottom or top
    back = back or side
    left = left or side
    right = right or side

    def fill(ox, oy, fw, fh, color, noise=0):
        for yy in range(oy, oy + fh):
            for xx in range(ox, ox + fw):
                c = list(color)
                if noise:
                    k = random.randint(-noise, noise)
                    c[0] = max(0, min(255, c[0] + k))
                    c[1] = max(0, min(255, c[1] + k))
                    c[2] = max(0, min(255, c[2] + k))
                im.putpixel((xx % 64, yy % 64), tuple(c) + (255,))

    fill(x + d, y, w, d, top, 4)                 # 上面
    fill(x + d + w, y, w, d, bottom, 4)          # 下面
    fill(x, y + d, d, h, right, 5)               # 右
    fill(x + d, y + d, w, h, front, 6)           # 前
    fill(x + d + w, y + d, d, h, left, 5)        # 左
    fill(x + d + w + d, y + d, w, h, back, 6)    # 后


def px(im, x, y, c):
    im.putpixel((x, y), tuple(c) + (255,))


def rect(im, x, y, w, h, c):
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            px(im, xx, yy, c)


# ---------------- 僵尸 ----------------
def zombie():
    im = new()
    skin = (86, 122, 62)
    skin_d = (68, 100, 48)
    shirt = (0, 168, 168)
    shirt_d = (0, 140, 140)
    pants = (59, 59, 176)
    pants_d = (44, 44, 140)

    # 头（含脸）：前脸区域是 (8,8) 起 8x8
    box(im, 0, 0, 8, 8, 8, skin, skin_d, skin, skin_d)
    rect(im, 0, 0, 32, 16, skin)          # 整个头部区先铺底色
    for yy in range(0, 16):
        for xx in range(32):
            if (xx + yy) % 7 == 0:
                px(im, xx, yy, skin_d)
    # 脸（front = 头部区 (8,8) 8x8）
    rect(im, 8, 8, 8, 8, skin)
    # 眼睛（两只 2x1 的深色，向上偏移一点）
    rect(im, 9, 10, 2, 1, (24, 40, 32))
    rect(im, 13, 10, 2, 1, (24, 40, 32))
    # 嘴
    rect(im, 10, 13, 4, 1, (40, 62, 44))
    px(im, 11, 12, (40, 62, 44))
    px(im, 12, 12, (40, 62, 44))

    # 身体 (16,16) 8x12x4
    rect(im, 16, 16, 40, 28, shirt)
    for yy in range(16, 32):
        for xx in range(16, 40):
            if (xx * 3 + yy) % 11 == 0:
                px(im, xx, yy, shirt_d)

    # 手臂 (40,16) 4x12x4 ×2
    rect(im, 40, 16, 12, 16, skin)
    rect(im, 32, 48, 12, 16, skin)
    # 袖子
    rect(im, 40, 16, 12, 4, shirt)
    rect(im, 32, 48, 12, 4, shirt)

    # 腿 (0,16) 4x12x4 ×2
    rect(im, 0, 16, 12, 16, pants)
    rect(im, 16, 48, 12, 16, pants)
    for yy in range(16, 32):
        for xx in range(0, 12):
            if (xx + yy) % 9 == 0:
                px(im, xx, yy, pants_d)
    return im


# ---------------- 苦力怕 ----------------
def creeper():
    im = new()
    g = (88, 174, 74)
    gd = (66, 143, 55)
    gl = (110, 196, 92)

    box(im, 0, 0, 8, 8, 8, g, gd, g, gd)
    rect(im, 0, 0, 32, 16, g)
    for yy in range(16):
        for xx in range(32):
            r = (xx * 5 + yy * 3) % 13
            if r == 0:
                px(im, xx, yy, gd)
            elif r == 3:
                px(im, xx, yy, gl)

    # 苦力怕的脸（front (8,8) 8x8）
    rect(im, 8, 8, 8, 8, g)
    # 两只方眼 2x2
    rect(im, 9, 9, 2, 2, (20, 26, 20))
    rect(im, 13, 9, 2, 2, (20, 26, 20))
    # 嘴：中间 2x3 竖条 + 下方两撇
    rect(im, 11, 11, 2, 3, (20, 26, 20))
    rect(im, 10, 13, 1, 2, (20, 26, 20))
    rect(im, 13, 13, 1, 2, (20, 26, 20))

    # 身体 (16,16) 8x12x4
    rect(im, 16, 16, 32, 28, g)
    for yy in range(16, 32):
        for xx in range(16, 40):
            r = (xx * 7 + yy * 5) % 17
            if r == 0:
                px(im, xx, yy, gd)
            elif r == 5:
                px(im, xx, yy, gl)

    # 四条腿 (0,16) 每个 4x6x4：贴图里给两段
    rect(im, 0, 16, 12, 12, g)
    rect(im, 16, 48, 12, 12, g)
    return im


# ---------------- 猪灵 ----------------
def piglin():
    im = new()
    skin = (224, 168, 142)
    skin_d = (196, 141, 117)
    body = (110, 70, 56)
    body_d = (86, 54, 43)
    pants = (62, 42, 34)
    gold = (232, 194, 90)

    box(im, 0, 0, 8, 8, 8, skin, skin_d, skin, skin_d)
    rect(im, 0, 0, 32, 16, skin)
    for yy in range(16):
        for xx in range(32):
            if (xx * 3 + yy * 5) % 17 == 0:
                px(im, xx, yy, skin_d)

    # 脸
    rect(im, 8, 8, 8, 8, skin)
    # 金色眼睛
    rect(im, 9, 10, 2, 1, gold)
    rect(im, 13, 10, 2, 1, gold)
    # 鼻子阴影（鼻子是单独的小方块，这里画同色阴影）
    rect(im, 10, 12, 4, 2, skin_d)
    rect(im, 11, 13, 2, 1, (150, 96, 80))

    # 身体
    rect(im, 16, 16, 40, 28, body)
    # 金腰带
    rect(im, 16, 26, 40, 2, gold)

    # 手臂 / 腿
    rect(im, 40, 16, 12, 16, skin)
    rect(im, 32, 48, 12, 16, skin)
    rect(im, 0, 16, 12, 16, pants)
    rect(im, 16, 48, 12, 16, pants)
    return im


# ---------------- 蜘蛛 ----------------
def spider():
    im = new()
    b = (46, 46, 52)
    bd = (32, 32, 38)
    bl = (62, 62, 70)

    # 蜘蛛：头 8x8x8 在 (0,0)，身体 6x6x6 在 (32,0)，腿用纯色
    box(im, 0, 0, 8, 8, 8, b, bd, b, bd)
    rect(im, 0, 0, 32, 16, b)
    for yy in range(16):
        for xx in range(32):
            r = (xx * 3 + yy) % 11
            if r == 0:
                px(im, xx, yy, bd)
            elif r == 4:
                px(im, xx, yy, bl)

    # 头前面：四只红眼（2 大 2 小）
    rect(im, 8, 8, 8, 8, b)
    rect(im, 9, 9, 2, 2, (200, 40, 40))
    rect(im, 13, 9, 2, 2, (200, 40, 40))
    rect(im, 10, 12, 1, 1, (160, 26, 26))
    rect(im, 13, 12, 1, 1, (160, 26, 26))

    # 身体块 (32,0) 放 6x6x6 的贴图（复用 16x16 区域）
    box(im, 32, 0, 6, 6, 6, b, bd, b, bd)
    return im


random.seed(7)
zombie().save(os.path.join(OUT, "zombie.png"))
creeper().save(os.path.join(OUT, "creeper.png"))
piglin().save(os.path.join(OUT, "piglin.png"))
spider().save(os.path.join(OUT, "spider.png"))
print("done:", os.listdir(OUT))
