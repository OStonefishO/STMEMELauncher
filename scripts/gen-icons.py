"""生成 16x16 像素风的 Minecraft 物品图标（自绘，不直接搬官方贴图）。"""
import os
from PIL import Image

OUT = r"D:\综合创作\MCLauncher\docs\assets\icons"
os.makedirs(OUT, exist_ok=True)


def img():
    return Image.new("RGBA", (16, 16), (0, 0, 0, 0))


def px(im, x, y, c):
    if 0 <= x < 16 and 0 <= y < 16:
        im.putpixel((x, y), c)


def rect(im, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            px(im, x, y, c)


def save(im, name, scale=1):
    im = im.resize((16 * scale, 16 * scale), Image.NEAREST) if scale > 1 else im
    im.save(os.path.join(OUT, name))


# ---------- 草方块 ----------
def grass_block():
    im = img()
    top = [(109, 176, 66), (122, 190, 75), (98, 160, 58), (133, 199, 84)]
    dirt = [(134, 96, 67), (120, 84, 58), (146, 106, 74), (110, 78, 54)]
    for y in range(16):
        for x in range(16):
            if y < 4:
                c = top[(x * 3 + y * 5) % 4]
                if y == 0 or x == 0:
                    c = top[2]
            else:
                c = dirt[(x * 7 + y * 3) % 4]
                if y == 4:
                    c = (86, 128, 52) if (x % 3) else top[3]
            if x == 15 or y == 15:
                c = tuple(int(v * 0.72) for v in c)
            px(im, x, y, c + (255,))
    return im


# ---------- 书 ----------
def book():
    im = img()
    cover = [(140, 84, 50), (120, 70, 41), (161, 101, 62)]
    page = [(238, 231, 208), (223, 214, 188)]
    for y in range(2, 15):
        for x in range(2, 14):
            c = cover[(x + y) % 3] if x < 4 or y < 4 or y > 12 else cover[1]
            px(im, x, y, c + (255,))
    # 书页
    rect(im, 6, 4, 13, 12, page[0] + (255,))
    for y in range(5, 12, 2):
        rect(im, 7, y, 12, y, page[1] + (255,))
    # 书脊
    rect(im, 4, 2, 5, 14, (94, 56, 33, 255))
    im.putpixel((5, 7), (212, 178, 96, 255))
    return im


# ---------- 红石火把 ----------
def torch():
    im = img()
    rect(im, 7, 7, 8, 14, (120, 84, 52, 255))
    rect(im, 8, 7, 8, 14, (96, 66, 40, 255))
    rect(im, 6, 2, 9, 6, (214, 42, 34, 255))
    rect(im, 7, 1, 8, 2, (255, 118, 92, 255))
    im.putpixel((7, 3), (255, 176, 120, 255))
    im.putpixel((9, 4), (170, 24, 24, 255))
    return im


# ---------- 箱子 ----------
def chest():
    im = img()
    wood = [(160, 110, 66), (140, 94, 55), (176, 124, 76)]
    iron = [(86, 86, 96), (110, 110, 122)]
    for y in range(3, 14):
        for x in range(2, 14):
            c = wood[(x + y // 2) % 3]
            if y in (3, 8, 13) or x in (2, 13):
                c = iron[0]
            px(im, x, y, c + (255,))
    rect(im, 7, 7, 8, 10, iron[1] + (255,))
    px(im, 7, 9, (230, 200, 120, 255))
    return im


# ---------- 末影箱 ----------
def ender_chest():
    im = img()
    body = [(22, 40, 44), (30, 52, 56), (44, 70, 74)]
    for y in range(3, 14):
        for x in range(2, 14):
            c = body[(x * y) % 3]
            if y in (3, 8, 13) or x in (2, 13):
                c = body[2]
            px(im, x, y, c + (255,))
    # 眼状锁扣
    rect(im, 7, 8, 8, 9, (108, 220, 208, 255))
    px(im, 7, 8, (190, 255, 246, 255))
    return im


# ---------- 铁砧 ----------
def anvil():
    im = img()
    iron = [(78, 78, 88), (98, 98, 110), (58, 58, 66)]
    rect(im, 3, 3, 12, 5, iron[1] + (255,))
    rect(im, 4, 6, 11, 7, iron[0] + (255,))
    rect(im, 6, 8, 9, 11, iron[0] + (255,))
    rect(im, 4, 12, 11, 13, iron[1] + (255,))
    rect(im, 3, 3, 12, 3, iron[2] + (255,))
    rect(im, 4, 13, 11, 13, iron[2] + (255,))
    return im


# ---------- 地图（问答用） ----------
def map_item():
    im = img()
    paper = [(222, 210, 178), (205, 192, 158), (232, 222, 192)]
    for y in range(2, 14):
        for x in range(2, 14):
            c = paper[(x * 2 + y) % 3]
            if x in (2, 13) or y in (2, 13):
                c = (168, 152, 116)
            px(im, x, y, c + (255,))
    # 路线
    for (x, y) in [(4, 11), (5, 10), (6, 10), (7, 9), (8, 9), (9, 8), (10, 7), (11, 6)]:
        px(im, x, y, (176, 64, 52, 255))
    px(im, 10, 6, (60, 120, 70, 255))
    return im


save(grass_block(), "grass.png")
save(book(), "book.png")
save(torch(), "torch.png")
save(chest(), "chest.png")
save(ender_chest(), "enderchest.png")
save(anvil(), "anvil.png")
save(map_item(), "map.png")

print("done:", ", ".join(sorted(os.listdir(OUT))))
