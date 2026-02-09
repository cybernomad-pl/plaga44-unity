#!/bin/bash
# Download example datasets for cbrnmd.3D

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "================================"
echo "cbrnmd.3D - Download Datasets"
echo "================================"
echo ""

# Download Sceaux Castle dataset
echo "📥 Downloading Castle Example (Sceaux Castle)..."
mkdir -p "$SCRIPT_DIR/castle_example/input"

if [ -d "/tmp/ImageDataset_SceauxCastle-master" ]; then
    echo "Using cached dataset from /tmp"
else
    wget -q --show-progress --timeout=120 \
        "https://github.com/openMVG/ImageDataset_SceauxCastle/archive/refs/heads/master.zip" \
        -O /tmp/castle.zip

    unzip -q /tmp/castle.zip -d /tmp/
fi

cp /tmp/ImageDataset_SceauxCastle-master/images/*.JPG "$SCRIPT_DIR/castle_example/input/"
echo "✓ Castle example: $(ls $SCRIPT_DIR/castle_example/input/*.JPG | wc -l) images"

# Generate synthetic turntable dataset
echo ""
echo "📥 Generating Turntable Example (Synthetic)..."
mkdir -p "$SCRIPT_DIR/turntable_example/input"

python3 << 'EOF'
from PIL import Image, ImageDraw
import math
import os

output_dir = os.path.join(os.path.dirname(__file__), 'turntable_example', 'input')
os.makedirs(output_dir, exist_ok=True)

for i in range(36):
    angle = i * 10
    img = Image.new('RGB', (800, 600), color=(240, 240, 240))
    draw = ImageDraw.Draw(img)

    center_x, center_y = 400, 300
    size = 100
    rad = math.radians(angle)
    x_offset = int(size * math.sin(rad))

    points = [
        (center_x + x_offset, center_y - size),
        (center_x + size + x_offset, center_y - size),
        (center_x + size + x_offset, center_y + size),
        (center_x + x_offset, center_y + size)
    ]
    draw.polygon(points, fill=(100, 150, 200), outline=(50, 100, 150))
    draw.ellipse([center_x + x_offset - 20, center_y - 20,
                  center_x + x_offset + 20, center_y + 20],
                 fill=(255, 100, 100))
    draw.rectangle([center_x + size//2 + x_offset - 10, center_y - size + 20,
                   center_x + size//2 + x_offset + 10, center_y - size + 60],
                   fill=(100, 255, 100))

    img.save(os.path.join(output_dir, f'img_{i:03d}.jpg'), quality=95)

print(f"Generated 36 synthetic images")
EOF

echo "✓ Turntable example: 36 synthetic images"

echo ""
echo "================================"
echo "✓ Datasets ready!"
echo "================================"
echo ""
echo "Next steps:"
echo "1. Test turntable example:"
echo "   python ../pipeline/photo_pipeline.py \\"
echo "     --input turntable_example/input \\"
echo "     --output turntable_example/output"
echo ""
echo "2. Test castle example:"
echo "   python ../pipeline/photo_pipeline.py \\"
echo "     --input castle_example/input \\"
echo "     --output castle_example/output"
