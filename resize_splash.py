from PIL import Image

try:
    img = Image.open("logolumi_cropped.png")
    # Resize for splash screen to a reasonable size (e.g., 400x400)
    # Using LANCZOS for high quality downsampling
    img.thumbnail((400, 400), Image.Resampling.LANCZOS)
    img.save("LumiContact/LumiContact.Android/Resources/drawable/logolumi_splash.png")
    print(f"Saved splash screen image with size {img.size}")
except Exception as e:
    print(f"Error: {e}")
