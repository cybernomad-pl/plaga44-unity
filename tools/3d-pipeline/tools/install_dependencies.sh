#!/bin/bash
# Install dependencies for cbrnmd.3D photogrammetry pipeline

set -e

echo "================================"
echo "cbrnmd.3D - Dependencies Setup"
echo "================================"
echo ""

# Detect OS
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    OS="linux"
elif [[ "$OSTYPE" == "darwin"* ]]; then
    OS="macos"
else
    echo "Unsupported OS: $OSTYPE"
    echo "Please install dependencies manually. See docs/SETUP.md"
    exit 1
fi

echo "Detected OS: $OS"
echo ""

# Check if running as root (for apt/dnf)
if [ "$OS" = "linux" ] && [ "$EUID" -ne 0 ]; then
    echo "Note: This script may require sudo for system packages."
    echo "You may be prompted for your password."
    echo ""
fi

# Install FFmpeg
echo "Installing FFmpeg..."
if [ "$OS" = "linux" ]; then
    if command -v apt &> /dev/null; then
        sudo apt update
        sudo apt install -y ffmpeg
    elif command -v dnf &> /dev/null; then
        sudo dnf install -y ffmpeg
    elif command -v pacman &> /dev/null; then
        sudo pacman -S --noconfirm ffmpeg
    else
        echo "Error: Package manager not found. Install FFmpeg manually."
        exit 1
    fi
elif [ "$OS" = "macos" ]; then
    if ! command -v brew &> /dev/null; then
        echo "Error: Homebrew not found. Install from https://brew.sh"
        exit 1
    fi
    brew install ffmpeg
fi

# Install COLMAP
echo ""
echo "Installing COLMAP..."
if [ "$OS" = "linux" ]; then
    if command -v apt &> /dev/null; then
        sudo apt install -y colmap
    else
        echo "COLMAP not available in package manager."
        echo "Please build from source: https://colmap.github.io/install.html"
    fi
elif [ "$OS" = "macos" ]; then
    brew install colmap
fi

# Install Python3 and pip if needed
echo ""
echo "Checking Python..."
if ! command -v python3 &> /dev/null; then
    echo "Installing Python 3..."
    if [ "$OS" = "linux" ]; then
        sudo apt install -y python3 python3-pip python3-venv
    elif [ "$OS" = "macos" ]; then
        brew install python@3.11
    fi
else
    echo "Python 3 already installed: $(python3 --version)"
fi

# Create virtual environment
echo ""
echo "Creating Python virtual environment..."
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_DIR"

if [ -d "venv" ]; then
    echo "Virtual environment already exists."
else
    python3 -m venv venv
    echo "✓ Virtual environment created"
fi

# Activate venv and install Python packages
echo ""
echo "Installing Python packages..."
source venv/bin/activate

pip install --upgrade pip
pip install -r tools/requirements.txt

echo ""
echo "✓ Python packages installed"

# Verify installations
echo ""
echo "================================"
echo "Verifying installations..."
echo "================================"
echo ""

check_command() {
    if command -v "$1" &> /dev/null; then
        echo "✓ $1: $(command -v $1)"
        return 0
    else
        echo "✗ $1: NOT FOUND"
        return 1
    fi
}

ALL_GOOD=true

check_command ffmpeg || ALL_GOOD=false
check_command colmap || ALL_GOOD=false
check_command python3 || ALL_GOOD=false

echo ""
echo "Python packages:"
python3 -c "
import sys
packages = ['numpy', 'cv2', 'PIL', 'yaml', 'trimesh']
for pkg in packages:
    try:
        __import__(pkg)
        print(f'✓ {pkg}')
    except ImportError:
        print(f'✗ {pkg}: NOT FOUND')
        sys.exit(1)
"

if [ $? -eq 0 ]; then
    echo ""
    echo "================================"
    echo "✓ All dependencies installed!"
    echo "================================"
    echo ""
    echo "Next steps:"
    echo "1. Activate virtual environment:"
    echo "   source venv/bin/activate"
    echo ""
    echo "2. Run example pipeline:"
    echo "   python pipeline/turntable_pipeline.py --input video.mp4 --output output/"
    echo ""
    echo "3. See docs/TURNTABLE-GUIDE.md for more info"
else
    echo ""
    echo "✗ Some packages failed to install"
    echo "Check errors above and see docs/SETUP.md for manual installation"
    exit 1
fi
