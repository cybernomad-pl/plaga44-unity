#!/usr/bin/env python3
"""
Turntable Video Photogrammetry Pipeline
cbrnmd.3D - CYBERNOMAD Project

Converts turntable video to 3D model using COLMAP.

Usage:
    python turntable_pipeline.py --input video.mp4 --output output_dir
    python turntable_pipeline.py --input video.mp4 --config custom_config.yaml
"""

import os
import sys
import argparse
import subprocess
import yaml
import shutil
from pathlib import Path
from datetime import datetime
import json

# Add scripts directory to path
sys.path.append(str(Path(__file__).parent.parent / 'scripts'))

try:
    from extract_frames import extract_frames_from_video
    from prepare_images import prepare_images
    from optimize_mesh import optimize_mesh
except ImportError:
    print("Warning: Some helper modules not found. Using simplified workflow.")


class TurntablePipeline:
    """Main pipeline for processing turntable video to 3D model."""

    def __init__(self, input_video, output_dir, config_path=None):
        self.input_video = Path(input_video)
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)

        # Load configuration
        if config_path:
            self.config = self.load_config(config_path)
        else:
            default_config = Path(__file__).parent / 'config.yaml'
            self.config = self.load_config(default_config)

        # Setup paths
        self.frames_dir = self.output_dir / 'frames'
        self.database_path = self.output_dir / 'database.db'
        self.sparse_dir = self.output_dir / 'sparse'
        self.dense_dir = self.output_dir / 'dense'
        self.mesh_dir = self.output_dir / 'mesh'
        self.final_dir = self.output_dir / 'final'

        # Create directories
        for d in [self.frames_dir, self.sparse_dir, self.dense_dir,
                  self.mesh_dir, self.final_dir]:
            d.mkdir(exist_ok=True)

        # Setup logging
        self.log_file = self.output_dir / 'pipeline.log'
        self.start_time = datetime.now()

        self.log(f"Pipeline initialized at {self.start_time}")
        self.log(f"Input: {self.input_video}")
        self.log(f"Output: {self.output_dir}")

    def load_config(self, config_path):
        """Load YAML configuration file."""
        with open(config_path, 'r') as f:
            return yaml.safe_load(f)

    def log(self, message, level='INFO'):
        """Log message to console and file."""
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        log_line = f"[{timestamp}] {level}: {message}"

        print(log_line)

        if self.config['output']['log_file']:
            with open(self.log_file, 'a') as f:
                f.write(log_line + '\n')

    def run_command(self, cmd, description=""):
        """Run shell command with logging."""
        if description:
            self.log(f"Running: {description}")

        self.log(f"Command: {' '.join(cmd)}", level='DEBUG')

        try:
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                check=True
            )

            if result.stdout and self.config['output']['verbose']:
                self.log(result.stdout, level='DEBUG')

            return True

        except subprocess.CalledProcessError as e:
            self.log(f"Error: {e}", level='ERROR')
            self.log(f"Stderr: {e.stderr}", level='ERROR')
            return False

    def check_dependencies(self):
        """Check if required tools are installed."""
        self.log("Checking dependencies...")

        required = ['ffmpeg', 'colmap']
        missing = []

        for tool in required:
            if not shutil.which(tool):
                missing.append(tool)

        if missing:
            self.log(f"Missing dependencies: {', '.join(missing)}", level='ERROR')
            self.log("Please install missing tools. See docs/SETUP.md", level='ERROR')
            return False

        self.log("All dependencies found ✓")
        return True

    def extract_frames(self):
        """Extract frames from video."""
        self.log("=" * 60)
        self.log("STEP 1: Extracting frames from video")
        self.log("=" * 60)

        cfg = self.config['frame_extraction']

        # Calculate optimal frame rate
        if cfg['fps'] is None:
            # Auto-detect video duration and calculate optimal fps
            result = subprocess.run(
                ['ffprobe', '-v', 'error', '-show_entries',
                 'format=duration', '-of',
                 'default=noprint_wrappers=1:nokey=1',
                 str(self.input_video)],
                capture_output=True,
                text=True
            )
            duration = float(result.stdout.strip())
            fps = cfg['target_frames'] / duration
            self.log(f"Auto-detected video duration: {duration:.2f}s")
            self.log(f"Calculated optimal FPS: {fps:.2f}")
        else:
            fps = cfg['fps']

        # Extract frames using ffmpeg
        cmd = [
            'ffmpeg',
            '-i', str(self.input_video),
            '-vf', f'fps={fps}',
            '-qscale:v', str(100 - cfg['frame_quality']),
            str(self.frames_dir / 'frame_%04d.jpg')
        ]

        if not self.run_command(cmd, "Extracting frames with FFmpeg"):
            return False

        # Count extracted frames
        frames = list(self.frames_dir.glob('*.jpg'))
        self.log(f"Extracted {len(frames)} frames")

        if len(frames) < cfg['min_frames']:
            self.log(f"Warning: Only {len(frames)} frames (minimum: {cfg['min_frames']})",
                    level='WARNING')

        return True

    def preprocess_images(self):
        """Preprocess extracted frames."""
        self.log("=" * 60)
        self.log("STEP 2: Preprocessing images")
        self.log("=" * 60)

        # TODO: Implement preprocessing if needed
        # For now, we skip preprocessing
        self.log("Preprocessing skipped (not configured)")
        return True

    def run_colmap_feature_extraction(self):
        """COLMAP: Extract features from images."""
        self.log("=" * 60)
        self.log("STEP 3: Feature extraction (COLMAP)")
        self.log("=" * 60)

        cfg = self.config['colmap']['feature_extractor']

        cmd = [
            'colmap', 'feature_extractor',
            '--database_path', str(self.database_path),
            '--image_path', str(self.frames_dir),
            '--ImageReader.camera_model', cfg['camera_model'],
            '--ImageReader.single_camera', '1' if cfg['single_camera'] else '0',
            '--SiftExtraction.use_gpu', '1' if cfg['gpu'] else '0'
        ]

        return self.run_command(cmd, "Extracting features")

    def run_colmap_matching(self):
        """COLMAP: Match features between images."""
        self.log("=" * 60)
        self.log("STEP 4: Feature matching (COLMAP)")
        self.log("=" * 60)

        cfg = self.config['colmap']['matcher']

        cmd = [
            'colmap', f'{cfg["type"]}_matcher',
            '--database_path', str(self.database_path),
            '--SiftMatching.use_gpu', '1' if cfg['gpu'] else '0'
        ]

        return self.run_command(cmd, "Matching features")

    def run_colmap_mapper(self):
        """COLMAP: Sparse reconstruction."""
        self.log("=" * 60)
        self.log("STEP 5: Sparse reconstruction (COLMAP)")
        self.log("=" * 60)

        cfg = self.config['colmap']['mapper']

        cmd = [
            'colmap', 'mapper',
            '--database_path', str(self.database_path),
            '--image_path', str(self.frames_dir),
            '--output_path', str(self.sparse_dir),
            '--Mapper.ba_refine_focal_length', '1' if cfg['ba_refine_focal_length'] else '0',
            '--Mapper.ba_refine_principal_point', '1' if cfg['ba_refine_principal_point'] else '0',
            '--Mapper.ba_refine_extra_params', '1' if cfg['ba_refine_extra_params'] else '0'
        ]

        return self.run_command(cmd, "Sparse reconstruction")

    def run_colmap_undistorter(self):
        """COLMAP: Undistort images."""
        self.log("=" * 60)
        self.log("STEP 6: Image undistortion (COLMAP)")
        self.log("=" * 60)

        sparse_model = self.sparse_dir / '0'
        if not sparse_model.exists():
            self.log("Error: Sparse model not found", level='ERROR')
            return False

        cmd = [
            'colmap', 'image_undistorter',
            '--image_path', str(self.frames_dir),
            '--input_path', str(sparse_model),
            '--output_path', str(self.dense_dir),
            '--output_type', 'COLMAP'
        ]

        return self.run_command(cmd, "Undistorting images")

    def run_colmap_stereo(self):
        """COLMAP: Dense stereo matching."""
        self.log("=" * 60)
        self.log("STEP 7: Dense stereo reconstruction (COLMAP)")
        self.log("=" * 60)

        cfg = self.config['colmap']['dense']

        cmd = [
            'colmap', 'patch_match_stereo',
            '--workspace_path', str(self.dense_dir),
            '--PatchMatchStereo.window_radius', str(cfg['window_radius']),
            '--PatchMatchStereo.num_samples', str(cfg['num_samples']),
            '--PatchMatchStereo.num_iterations', str(cfg['num_iterations']),
            '--PatchMatchStereo.geom_consistency', '1' if cfg['geom_consistency'] else '0'
        ]

        return self.run_command(cmd, "Dense stereo matching")

    def run_colmap_fusion(self):
        """COLMAP: Stereo fusion to point cloud."""
        self.log("=" * 60)
        self.log("STEP 8: Stereo fusion (COLMAP)")
        self.log("=" * 60)

        output_ply = self.dense_dir / 'fused.ply'

        cmd = [
            'colmap', 'stereo_fusion',
            '--workspace_path', str(self.dense_dir),
            '--output_path', str(output_ply)
        ]

        return self.run_command(cmd, "Fusing point cloud")

    def run_colmap_meshing(self):
        """COLMAP: Generate mesh from point cloud."""
        self.log("=" * 60)
        self.log("STEP 9: Mesh generation (COLMAP)")
        self.log("=" * 60)

        cfg = self.config['meshing']
        input_ply = self.dense_dir / 'fused.ply'
        output_ply = self.mesh_dir / 'mesh.ply'

        if cfg['method'] == 'poisson':
            cmd = [
                'colmap', 'poisson_mesher',
                '--input_path', str(input_ply),
                '--output_path', str(output_ply),
                '--PoissonMeshing.depth', str(cfg['poisson_depth']),
                '--PoissonMeshing.trim', str(cfg['poisson_trim'])
            ]
        else:
            cmd = [
                'colmap', 'delaunay_mesher',
                '--input_path', str(input_ply),
                '--output_path', str(output_ply)
            ]

        return self.run_command(cmd, "Generating mesh")

    def optimize_and_export(self):
        """Optimize mesh and export to final formats."""
        self.log("=" * 60)
        self.log("STEP 10: Optimization and export")
        self.log("=" * 60)

        input_mesh = self.mesh_dir / 'mesh.ply'

        if not input_mesh.exists():
            self.log("Error: Mesh not found", level='ERROR')
            return False

        # Try to import trimesh for format conversion
        try:
            import trimesh
            TRIMESH_AVAILABLE = True
            self.log("Trimesh available - will convert to multiple formats")
        except ImportError:
            TRIMESH_AVAILABLE = False
            self.log("Trimesh not available - exporting PLY only", level='WARNING')

        # Always copy raw PLY to final
        shutil.copy(input_mesh, self.final_dir / 'model.ply')
        self.log("✓ Saved model.ply")

        # Export to configured formats
        for fmt in self.config['export']['formats']:
            fmt_lower = fmt.lower()
            output_file = self.final_dir / f'model.{fmt_lower}'

            if fmt_lower == 'ply':
                continue  # Already copied above

            self.log(f"Converting to {fmt}...")

            if not TRIMESH_AVAILABLE:
                self.log(f"  Skipped (trimesh not installed)", level='WARNING')
                self.log(f"  Install: pip install trimesh", level='INFO')
                continue

            try:
                # Load mesh with trimesh
                mesh = trimesh.load(str(input_mesh))

                # Optionally optimize mesh
                cfg_opt = self.config.get('optimization', {})
                if cfg_opt.get('enabled', False):
                    target_tris = cfg_opt.get('target_triangles', 10000)
                    current_tris = len(mesh.faces)

                    if current_tris > target_tris:
                        self.log(f"  Optimizing: {current_tris} → {target_tris} triangles")
                        mesh = mesh.simplify_quadric_decimation(target_tris)
                        self.log(f"  ✓ Optimized to {len(mesh.faces)} triangles")

                # Export to target format
                mesh.export(str(output_file))
                self.log(f"✓ Saved model.{fmt_lower}")

                # Generate LODs if requested
                if cfg_opt.get('generate_lods', False):
                    self.log(f"  Generating LOD levels...")
                    lod_ratios = cfg_opt.get('lod_ratios', [0.5, 0.25, 0.1])

                    for i, ratio in enumerate(lod_ratios, 1):
                        lod_tris = int(len(mesh.faces) * ratio)
                        lod_mesh = mesh.simplify_quadric_decimation(lod_tris)
                        lod_file = self.final_dir / f'model_LOD{i}.{fmt_lower}'
                        lod_mesh.export(str(lod_file))
                        self.log(f"  ✓ LOD{i}: {len(lod_mesh.faces)} triangles ({ratio*100:.0f}%)")

            except Exception as e:
                self.log(f"  Error converting to {fmt}: {e}", level='ERROR')
                self.log(f"  Fallback: Use Blender or MeshLab to convert manually", level='INFO')

        return True

    def cleanup(self):
        """Clean up intermediate files if configured."""
        if not self.config['output']['keep_intermediate']:
            self.log("Cleaning up intermediate files...")

            # Remove frames
            if self.frames_dir.exists():
                shutil.rmtree(self.frames_dir)

            # Remove database
            if self.database_path.exists():
                self.database_path.unlink()

            self.log("Cleanup complete")

    def generate_report(self):
        """Generate processing report."""
        self.log("=" * 60)
        self.log("Generating report")
        self.log("=" * 60)

        end_time = datetime.now()
        duration = end_time - self.start_time

        report = {
            'input_video': str(self.input_video),
            'output_directory': str(self.output_dir),
            'start_time': self.start_time.isoformat(),
            'end_time': end_time.isoformat(),
            'duration_seconds': duration.total_seconds(),
            'configuration': self.config
        }

        report_file = self.output_dir / 'report.json'
        with open(report_file, 'w') as f:
            json.dump(report, f, indent=2)

        self.log(f"Report saved to {report_file}")
        self.log(f"Total processing time: {duration}")

    def run(self):
        """Run complete pipeline."""
        self.log("=" * 60)
        self.log("Starting cbrnmd.3D Turntable Pipeline")
        self.log("=" * 60)

        steps = [
            ('Checking dependencies', self.check_dependencies),
            ('Extracting frames', self.extract_frames),
            ('Preprocessing images', self.preprocess_images),
            ('Feature extraction', self.run_colmap_feature_extraction),
            ('Feature matching', self.run_colmap_matching),
            ('Sparse reconstruction', self.run_colmap_mapper),
            ('Image undistortion', self.run_colmap_undistorter),
            ('Dense stereo', self.run_colmap_stereo),
            ('Stereo fusion', self.run_colmap_fusion),
            ('Mesh generation', self.run_colmap_meshing),
            ('Optimization & export', self.optimize_and_export),
        ]

        for step_name, step_func in steps:
            try:
                if not step_func():
                    self.log(f"Pipeline failed at: {step_name}", level='ERROR')
                    return False
            except Exception as e:
                self.log(f"Exception in {step_name}: {e}", level='ERROR')
                import traceback
                self.log(traceback.format_exc(), level='ERROR')
                return False

        # Cleanup and report
        self.cleanup()
        self.generate_report()

        self.log("=" * 60)
        self.log("Pipeline completed successfully! ✓")
        self.log("=" * 60)
        self.log(f"Output files in: {self.final_dir}")

        return True


def main():
    parser = argparse.ArgumentParser(
        description='Turntable Video Photogrammetry Pipeline'
    )
    parser.add_argument(
        '--input', '-i',
        required=True,
        help='Input video file (MP4, MOV, etc.)'
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
        print(f"Error: Input file not found: {args.input}")
        sys.exit(1)

    # Run pipeline
    pipeline = TurntablePipeline(args.input, args.output, args.config)
    success = pipeline.run()

    sys.exit(0 if success else 1)


if __name__ == '__main__':
    main()
