from PIL import Image, ImageChops


def trim_white(im):
    bg = Image.new(im.mode, im.size, (255, 255, 255, 255))
    diff = ImageChops.difference(im, bg)
    diff = ImageChops.add(diff, diff, 2.0, -100)
    bbox = diff.getbbox()
    if bbox:
        return im.crop(bbox)
    return im


def trim_trans(im):
    bg = Image.new(im.mode, im.size, im.getpixel((0, 0)))
    diff = ImageChops.difference(im, bg)
    diff = ImageChops.add(diff, diff, 2.0, -100)
    bbox = diff.getbbox()
    if bbox:
        return im.crop(bbox)
    return im


try:
    img = Image.open("logolumi.png")
    img = img.convert("RGBA")
    trimmed = trim_trans(img)
    if trimmed.size == img.size:
        trimmed = trim_white(img)

    # After cropping, let's add a small padding so it's not literally on the edge
    padding = int(max(trimmed.size) * 0.1)  # 10% padding
    new_size = (trimmed.width + padding * 2, trimmed.height + padding * 2)
    final_img = Image.new(
        "RGBA", new_size, (255, 255, 255, 0)
    )  # transparent background
    final_img.paste(trimmed, (padding, padding))

    final_img.save("logolumi_cropped.png")
    print(
        f"Cropped from {img.size} to {trimmed.size}, final with padding: {final_img.size}"
    )
except Exception as e:
    print(f"Error: {e}")
