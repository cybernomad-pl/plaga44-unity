#!/usr/bin/env python3
"""
Extract frames from turntable video.

Usage:
    python extract_frames.py --input video.mp4 --output frames/ --fps 5
    python extract_frames.py --input video.mp4 --output frames/ --count 120
"""

import argparse
import subprocess
import sys
from pathlib import Path


def get_video_info(video_path):
    """Get video duration and frame rate."""
    cmd = [
        'ffprobe',
        '-v', 'error',
        '-select_streams', 'v:0',
        '-show_entries', 'stream=duration,r_frame_rate',
        '-of', 'default=noprint_wrappers=1',
        str(video_path)
    ]

    result = subprocess.run(cmd, capture_output=True, text=True)
    info = {}

    for line in result.stdout.strip().split('\n'):
        if '=' in line:
            key, value = line.split('=')
            info[key] = value

    # Parse frame rate (format: "30/1")
    if 'r_frame_rate' in info:
        num, den = map(int, info['r_frame_rate'].split('/'))
        info['fps'] = num / den

    # Parse duration
    if 'duration' in info:
        info['duration'] = float(info['duration'])

    return info


def extract_frames_from_video(video_path, output_dir, fps=None, target_count=None,
                              quality=95, start_time=None, end_time=None):
    """
    Extract frames from video.

    Args:
        video_path: Path to input video
        output_dir: Directory to save frames
        fps: Frames per second to extract (if None, uses target_count)
        target_count: Target number of frames (if fps is None)
        quality: JPEG quality (1-100)
        start_time: Start time in seconds
        end_time: End time in seconds
    """
    video_path = Path(video_path)
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Get video info
    info = get_video_info(video_path)
    duration = info.get('duration', 0)
    video_fps = info.get('fps', 30)

    print(f"Video info:")
    print(f"  Duration: {duration:.2f}s")
    print(f"  FPS: {video_fps:.2f}")

    # Calculate extraction FPS
    if fps is None and target_count:
        actual_duration = duration
        if start_time:
            actual_duration -= start_time
        if end_time:
            actual_duration = end_time - (start_time or 0)

        fps = target_count / actual_duration
        print(f"  Target frames: {target_count}")
        print(f"  Calculated extraction FPS: {fps:.2f}")

    # Build ffmpeg command
    cmd = ['ffmpeg', '-i', str(video_path)]

    # Add time range if specified
    if start_time:
        cmd.extend(['-ss', str(start_time)])
    if end_time:
        cmd.extend(['-to', str(end_time)])

    # Add FPS filter
    if fps:
        cmd.extend(['-vf', f'fps={fps}'])

    # Quality setting (qscale:v where lower = higher quality, 2-31)
    qscale = max(2, min(31, int((100 - quality) / 100 * 29 + 2)))
    cmd.extend(['-qscale:v', str(qscale)])

    # Output pattern
    output_pattern = str(output_dir / 'frame_%04d.jpg')
    cmd.append(output_pattern)

    print(f"\nExtracting frames...")
    print(f"Command: {' '.join(cmd)}")

    try:
        subprocess.run(cmd, check=True)

        # Count extracted frames
        frames = list(output_dir.glob('frame_*.jpg'))
        print(f"\n✓ Extracted {len(frames)} frames to {output_dir}")

        return True

    except subprocess.CalledProcessError as e:
        print(f"Error extracting frames: {e}", file=sys.stderr)
        return False


def main():
    parser = argparse.ArgumentParser(description='Extract frames from video')
    parser.add_argument('--input', '-i', required=True, help='Input video file')
    parser.add_argument('--output', '-o', required=True, help='Output directory')
    parser.add_argument('--fps', type=float, help='Frames per second to extract')
    parser.add_argument('--count', '-c', type=int, help='Target number of frames')
    parser.add_argument('--quality', '-q', type=int, default=95,
                       help='JPEG quality (1-100, default: 95)')
    parser.add_argument('--start', type=float, help='Start time in seconds')
    parser.add_argument('--end', type=float, help='End time in seconds')

    args = parser.parse_args()

    if not Path(args.input).exists():
        print(f"Error: Input file not found: {args.input}", file=sys.stderr)
        sys.exit(1)

    if not args.fps and not args.count:
        print("Error: Specify either --fps or --count", file=sys.stderr)
        sys.exit(1)

    success = extract_frames_from_video(
        args.input,
        args.output,
        fps=args.fps,
        target_count=args.count,
        quality=args.quality,
        start_time=args.start,
        end_time=args.end
    )

    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
