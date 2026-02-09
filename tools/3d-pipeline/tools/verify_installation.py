#!/usr/bin/env python3
"""
Verify cbrnmd.3D installation and dependencies.

Usage:
    python verify_installation.py
"""

import shutil
import subprocess
import sys


def check_command(name, command=None):
    """Check if command is available."""
    if command is None:
        command = name

    print(f"Checking {name}...", end=" ")

    if shutil.which(command):
        # Try to get version
        try:
            result = subprocess.run(
                [command, '--version'],
                capture_output=True,
                text=True,
                timeout=5
            )
            version = result.stdout.split('\n')[0] if result.stdout else "installed"
            print(f"✓ ({version})")
            return True
        except:
            print("✓ (installed)")
            return True
    else:
        print("✗ NOT FOUND")
        return False


def check_python_package(package_name, import_name=None):
    """Check if Python package is available."""
    if import_name is None:
        import_name = package_name

    print(f"Checking {package_name}...", end=" ")

    try:
        module = __import__(import_name)
        version = getattr(module, '__version__', 'unknown')
        print(f"✓ (v{version})")
        return True
    except ImportError:
        print("✗ NOT FOUND")
        return False


def main():
    print("=" * 60)
    print("cbrnmd.3D - Installation Verification")
    print("=" * 60)
    print()

    all_good = True

    # Check system commands
    print("System Commands:")
    print("-" * 40)

    commands = [
        ('Python 3', 'python3'),
        ('FFmpeg', 'ffmpeg'),
        ('COLMAP', 'colmap'),
    ]

    for name, cmd in commands:
        if not check_command(name, cmd):
            all_good = False

    print()

    # Check optional commands
    print("Optional Tools:")
    print("-" * 40)

    optional = [
        ('Meshroom', 'meshroom'),
        ('Blender', 'blender'),
        ('MeshLab', 'meshlab'),
    ]

    for name, cmd in optional:
        check_command(name, cmd)

    print()

    # Check Python packages
    print("Python Packages:")
    print("-" * 40)

    packages = [
        ('numpy', 'numpy'),
        ('OpenCV', 'cv2'),
        ('Pillow', 'PIL'),
        ('PyYAML', 'yaml'),
        ('trimesh', 'trimesh'),
        ('tqdm', 'tqdm'),
    ]

    for name, import_name in packages:
        if not check_python_package(name, import_name):
            all_good = False

    print()

    # Check optional Python packages
    print("Optional Python Packages:")
    print("-" * 40)

    optional_packages = [
        ('pymeshlab', 'pymeshlab'),
        ('open3d', 'open3d'),
        ('plyfile', 'plyfile'),
    ]

    for name, import_name in optional_packages:
        check_python_package(name, import_name)

    print()
    print("=" * 60)

    if all_good:
        print("✓ All required dependencies installed!")
        print()
        print("You're ready to use cbrnmd.3D pipeline.")
        print()
        print("Next steps:")
        print("1. See docs/TURNTABLE-GUIDE.md for recording tips")
        print("2. Run pipeline:")
        print("   python pipeline/turntable_pipeline.py --input video.mp4 --output out/")
        return 0
    else:
        print("✗ Some required dependencies are missing")
        print()
        print("Please install missing dependencies:")
        print("- Run: ./tools/install_dependencies.sh")
        print("- Or see: docs/SETUP.md for manual installation")
        return 1


if __name__ == '__main__':
    sys.exit(main())
