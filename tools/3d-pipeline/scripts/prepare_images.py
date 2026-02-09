#!/usr/bin/env python3
"""
Prepare images for photogrammetry processing.

Operations:
- Resize images
- Auto-crop to object
- Enhance sharpness
- Denoise
- White balance

Usage:
    python prepare_images.py --input frames/ --output prepared/
    python prepare_images.py --input frames/ --output prepared/ --resize 2048
"""

import argparse
import sys
from pathlib import Path
from PIL import Image, ImageEnhance, ImageFilter
import cv2
import numpy as np
from tqdm import tqdm


def resize_image(img, max_size):
    """Resize image maintaining aspect ratio."""
    width, height = img.size

    if max(width, height) <= max_size:
        return img

    if width > height:
        new_width = max_size
        new_height = int(height * (max_size / width))
    else:
        new_height = max_size
        new_width = int(width * (max_size / height))

    return img.resize((new_width, new_height), Image.Resampling.LANCZOS)


def auto_crop(img, threshold=10):
    """Auto-crop image to object (remove uniform background)."""
    # Convert to numpy array
    img_array = np.array(img)

    # Convert to grayscale
    if len(img_array.shape) == 3:
        gray = cv2.cvtColor(img_array, cv2.COLOR_RGB2GRAY)
    else:
        gray = img_array

    # Find edges
    edges = cv2.Canny(gray, 50, 150)

    # Find contours
    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    if not contours:
        return img

    # Get bounding rectangle of all contours
    x_min, y_min = img.size
    x_max, y_max = 0, 0

    for contour in contours:
        x, y, w, h = cv2.boundingRect(contour)
        x_min = min(x_min, x)
        y_min = min(y_min, y)
        x_max = max(x_max, x + w)
        y_max = max(y_max, y + h)

    # Add margin
    margin = threshold
    x_min = max(0, x_min - margin)
    y_min = max(0, y_min - margin)
    x_max = min(img.size[0], x_max + margin)
    y_max = min(img.size[1], y_max + margin)

    # Crop
    return img.crop((x_min, y_min, x_max, y_max))


def enhance_sharpness(img, factor=1.5):
    """Enhance image sharpness."""
    enhancer = ImageEnhance.Sharpness(img)
    return enhancer.enhance(factor)


def denoise(img):
    """Denoise image."""
    img_array = np.array(img)
    denoised = cv2.fastNlMeansDenoisingColored(img_array, None, 10, 10, 7, 21)
    return Image.fromarray(denoised)


def auto_white_balance(img):
    """Auto white balance."""
    img_array = np.array(img)
    result = cv2.cvtColor(img_array, cv2.COLOR_RGB2LAB)
    avg_a = np.average(result[:, :, 1])
    avg_b = np.average(result[:, :, 2])
    result[:, :, 1] = result[:, :, 1] - ((avg_a - 128) * (result[:, :, 0] / 255.0) * 1.1)
    result[:, :, 2] = result[:, :, 2] - ((avg_b - 128) * (result[:, :, 0] / 255.0) * 1.1)
    result = cv2.cvtColor(result, cv2.COLOR_LAB2RGB)
    return Image.fromarray(result)


def prepare_images(input_dir, output_dir, resize=None, crop=False,
                   sharpen=False, denoise_img=False, white_balance=False):
    """
    Prepare images for photogrammetry.

    Args:
        input_dir: Input directory with images
        output_dir: Output directory for processed images
        resize: Max dimension (maintains aspect ratio)
        crop: Auto-crop to object
        sharpen: Enhance sharpness
        denoise_img: Denoise images
        white_balance: Auto white balance
    """
    input_dir = Path(input_dir)
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Find all images
    image_files = []
    for ext in ['*.jpg', '*.jpeg', '*.png', '*.JPG', '*.JPEG', '*.PNG']:
        image_files.extend(input_dir.glob(ext))

    if not image_files:
        print(f"No images found in {input_dir}")
        return False

    print(f"Found {len(image_files)} images")
    print("Processing:")
    if resize:
        print(f"  - Resize to {resize}px")
    if crop:
        print("  - Auto-crop")
    if sharpen:
        print("  - Sharpen")
    if denoise_img:
        print("  - Denoise")
    if white_balance:
        print("  - White balance")

    # Process each image
    for img_file in tqdm(image_files, desc="Processing"):
        try:
            # Load image
            img = Image.open(img_file)

            # Convert to RGB if needed
            if img.mode != 'RGB':
                img = img.convert('RGB')

            # Apply operations
            if crop:
                img = auto_crop(img)

            if resize:
                img = resize_image(img, resize)

            if white_balance:
                img = auto_white_balance(img)

            if sharpen:
                img = enhance_sharpness(img)

            if denoise_img:
                img = denoise(img)

            # Save
            output_file = output_dir / img_file.name
            img.save(output_file, quality=95)

        except Exception as e:
            print(f"\nError processing {img_file}: {e}")
            continue

    print(f"\n✓ Processed images saved to {output_dir}")
    return True


def main():
    parser = argparse.ArgumentParser(description='Prepare images for photogrammetry')
    parser.add_argument('--input', '-i', required=True, help='Input directory')
    parser.add_argument('--output', '-o', required=True, help='Output directory')
    parser.add_argument('--resize', '-r', type=int, help='Max dimension (px)')
    parser.add_argument('--crop', action='store_true', help='Auto-crop to object')
    parser.add_argument('--sharpen', action='store_true', help='Enhance sharpness')
    parser.add_argument('--denoise', action='store_true', help='Denoise images')
    parser.add_argument('--white-balance', action='store_true', help='Auto white balance')

    args = parser.parse_args()

    if not Path(args.input).exists():
        print(f"Error: Input directory not found: {args.input}", file=sys.stderr)
        sys.exit(1)

    success = prepare_images(
        args.input,
        args.output,
        resize=args.resize,
        crop=args.crop,
        sharpen=args.sharpen,
        denoise_img=args.denoise,
        white_balance=args.white_balance
    )

    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
