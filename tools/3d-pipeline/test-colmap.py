#!/usr/bin/env python3
"""
Test COLMAP configuration
Checks if COLMAP is properly configured and can run
"""

import os
import sys
import subprocess
import platform

def test_colmap():
    """Test if COLMAP works with current environment"""

    print("=" * 60)
    print("COLMAP CONFIGURATION TEST")
    print("=" * 60)

    # Check platform
    os_name = platform.system()
    print(f"\n✓ Platform: {os_name}")

    # Check environment variables
    colmap_path = os.environ.get('COLMAP_PATH')
    qt_platform = os.environ.get('QT_QPA_PLATFORM')

    print(f"\n✓ COLMAP_PATH: {colmap_path or 'NOT SET'}")
    print(f"✓ QT_QPA_PLATFORM: {qt_platform or 'NOT SET'}")

    if os_name == 'Windows':
        if not colmap_path:
            print("\n❌ ERROR: COLMAP_PATH not set on Windows!")
            print("Set it with: $env:COLMAP_PATH = 'C:\\COLMAP\\...\\bin\\colmap.exe'")
            return False

        if qt_platform != 'windows':
            print("\n⚠️  WARNING: QT_QPA_PLATFORM should be 'windows' for this COLMAP build")
            print("Set it with: $env:QT_QPA_PLATFORM = 'windows'")
            print("Continuing anyway...")

        colmap_executable = colmap_path
    else:
        colmap_executable = 'colmap'

    # Test if COLMAP is accessible
    print(f"\n✓ Testing COLMAP executable: {colmap_executable}")

    try:
        result = subprocess.run(
            [colmap_executable, '--help'],
            capture_output=True,
            text=True,
            timeout=10
        )

        if result.returncode == 0:
            print("✓ COLMAP is accessible and responding")
            print(f"✓ COLMAP help output (first 200 chars):")
            print(result.stdout[:200])
            return True
        else:
            print(f"❌ COLMAP returned error code: {result.returncode}")
            print(f"STDERR: {result.stderr[:500]}")
            return False

    except FileNotFoundError:
        print(f"❌ COLMAP executable not found: {colmap_executable}")
        print("\nOn Windows, make sure:")
        print("1. COLMAP is installed")
        print("2. COLMAP_PATH points to colmap.exe")
        print("3. QT_QPA_PLATFORM is set to 'offscreen'")
        return False
    except Exception as e:
        print(f"❌ Error testing COLMAP: {e}")
        return False

if __name__ == '__main__':
    print()
    success = test_colmap()
    print()
    print("=" * 60)
    if success:
        print("✓ COLMAP TEST PASSED - Ready to run tutorial!")
    else:
        print("❌ COLMAP TEST FAILED - Fix configuration before running tutorial")
    print("=" * 60)
    print()

    sys.exit(0 if success else 1)
