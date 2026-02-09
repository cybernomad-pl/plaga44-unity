#!/usr/bin/env python3
"""
Photo-based Photogrammetry Pipeline
cbrnmd.3D - CYBERNOMAD Project

Converts series of photos to 3D model using COLMAP.

Usage:
    python photo_pipeline.py --input photos/ --output output_dir
"""

import argparse
import sys
from pathlib import Path

# Import turntable pipeline and reuse most functionality
from turntable_pipeline import TurntablePipeline


class PhotoPipeline(TurntablePipeline):
    """Pipeline for processing photo series (no video extraction needed)."""

    def __init__(self, input_photos, output_dir, config_path=None):
        # Initialize parent but override input handling
        self.input_photos = Path(input_photos)

        if not self.input_photos.exists():
            raise ValueError(f"Input directory not found: {input_photos}")

        # Count photos
        photo_files = []
        for ext in ['*.jpg', '*.jpeg', '*.png', '*.JPG', '*.JPEG', '*.PNG']:
            photo_files.extend(self.input_photos.glob(ext))

        if not photo_files:
            raise ValueError(f"No photos found in {input_photos}")

        print(f"Found {len(photo_files)} photos")

        # Call parent init but with dummy video path
        # We'll override the frame extraction step
        super().__init__(
            input_video=input_photos / 'dummy.mp4',  # Not used
            output_dir=output_dir,
            config_path=config_path
        )

        # Override frames directory to point to input photos
        self.frames_dir = self.input_photos
        self.photos_mode = True

    def extract_frames(self):
        """Override: Skip frame extraction for photo mode."""
        self.log("=" * 60)
        self.log("STEP 1: Using existing photos (skipping frame extraction)")
        self.log("=" * 60)

        # Count photos
        photos = list(self.frames_dir.glob('*.jpg'))
        photos.extend(self.frames_dir.glob('*.jpeg'))
        photos.extend(self.frames_dir.glob('*.png'))
        photos.extend(self.frames_dir.glob('*.JPG'))
        photos.extend(self.frames_dir.glob('*.JPEG'))
        photos.extend(self.frames_dir.glob('*.PNG'))

        self.log(f"Found {len(photos)} photos in {self.frames_dir}")

        min_photos = self.config['frame_extraction']['min_frames']
        if len(photos) < min_photos:
            self.log(f"Warning: Only {len(photos)} photos (minimum: {min_photos})",
                    level='WARNING')
            self.log("Consider taking more photos for better reconstruction", level='WARNING')

        return True


def main():
    parser = argparse.ArgumentParser(
        description='Photo-based Photogrammetry Pipeline'
    )
    parser.add_argument(
        '--input', '-i',
        required=True,
        help='Input directory with photos'
    )
    parser.add_argument(
        '--output', '-o',
        required=True,
        help='Output directory for results'
    )
    parser.add_argument(
        '--config', '-c',
        default=None,
        help='Configuration file (YAML). Default: config.yaml'
    )

    args = parser.parse_args()

    # Validate input
    if not Path(args.input).exists():
        print(f"Error: Input directory not found: {args.input}")
        sys.exit(1)

    # Run pipeline
    try:
        pipeline = PhotoPipeline(args.input, args.output, args.config)
        success = pipeline.run()
        sys.exit(0 if success else 1)
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)


if __name__ == '__main__':
    main()
