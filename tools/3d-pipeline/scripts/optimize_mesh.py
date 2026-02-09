#!/usr/bin/env python3
"""
Optimize 3D mesh for game engines (Unity).

Operations:
- Decimate/reduce polygon count
- Generate LOD levels
- Simplify topology
- Export to various formats

Usage:
    python optimize_mesh.py --input model.obj
    python optimize_mesh.py --input model.ply --target-tris 10000 --generate-lods
"""

import argparse
import sys
from pathlib import Path

try:
    import trimesh
    import pymeshlab
    MESHLAB_AVAILABLE = True
except ImportError:
    print("Warning: pymeshlab not available. Some features will be limited.")
    MESHLAB_AVAILABLE = False
    import trimesh


def load_mesh(input_path):
    """Load mesh from file."""
    print(f"Loading mesh from {input_path}...")
    mesh = trimesh.load(str(input_path))
    print(f"  Vertices: {len(mesh.vertices)}")
    print(f"  Faces: {len(mesh.faces)}")
    return mesh


def decimate_mesh(mesh, target_faces):
    """Reduce mesh complexity to target face count."""
    current_faces = len(mesh.faces)

    if current_faces <= target_faces:
        print(f"Mesh already has {current_faces} faces (target: {target_faces})")
        return mesh

    print(f"Decimating from {current_faces} to {target_faces} faces...")

    # Simple decimation
    mesh_decimated = mesh.simplify_quadric_decimation(target_faces)

    print(f"  Result: {len(mesh_decimated.faces)} faces")
    return mesh_decimated


def generate_lods(mesh, levels=3, ratios=None):
    """
    Generate LOD (Level of Detail) versions of mesh.

    Args:
        mesh: Input mesh
        levels: Number of LOD levels
        ratios: List of reduction ratios (e.g., [0.5, 0.25, 0.1])

    Returns:
        List of meshes [LOD0, LOD1, LOD2, ...]
    """
    if ratios is None:
        ratios = [0.5, 0.25, 0.1]

    ratios = ratios[:levels]

    lods = [mesh]  # LOD0 = original
    current_faces = len(mesh.faces)

    print(f"Generating {levels} LOD levels...")

    for i, ratio in enumerate(ratios):
        target = int(current_faces * ratio)
        lod = mesh.simplify_quadric_decimation(target)
        lods.append(lod)
        print(f"  LOD{i+1}: {len(lod.faces)} faces ({ratio*100:.0f}%)")

    return lods


def export_mesh(mesh, output_path, format=None):
    """Export mesh to file."""
    output_path = Path(output_path)

    if format is None:
        format = output_path.suffix[1:].upper()

    print(f"Exporting to {output_path}...")

    try:
        mesh.export(str(output_path))
        print(f"  ✓ Exported as {format}")
        return True
    except Exception as e:
        print(f"  Error exporting: {e}")
        return False


def optimize_mesh_pymeshlab(input_path, output_path, target_tris=10000):
    """Optimize mesh using PyMeshLab (advanced features)."""
    if not MESHLAB_AVAILABLE:
        print("PyMeshLab not available. Using trimesh instead.")
        return False

    print("Using PyMeshLab for advanced optimization...")

    ms = pymeshlab.MeshSet()
    ms.load_new_mesh(str(input_path))

    mesh = ms.current_mesh()
    print(f"Input: {mesh.vertex_number()} vertices, {mesh.face_number()} faces")

    # Remove duplicate vertices
    ms.meshing_remove_duplicate_vertices()

    # Remove unreferenced vertices
    ms.meshing_remove_unreferenced_vertices()

    # Remove duplicate faces
    ms.meshing_remove_duplicate_faces()

    # Simplify
    if mesh.face_number() > target_tris:
        target_faces = target_tris
        ms.meshing_decimation_quadric_edge_collapse(targetfacenum=target_faces)

    # Smooth
    ms.apply_coord_laplacian_smoothing()

    # Export
    ms.save_current_mesh(str(output_path))

    mesh = ms.current_mesh()
    print(f"Output: {mesh.vertex_number()} vertices, {mesh.face_number()} faces")
    print(f"✓ Optimized mesh saved to {output_path}")

    return True


def main():
    parser = argparse.ArgumentParser(description='Optimize 3D mesh')
    parser.add_argument('--input', '-i', required=True, help='Input mesh file')
    parser.add_argument('--output', '-o', help='Output mesh file')
    parser.add_argument('--target-tris', '-t', type=int, default=10000,
                       help='Target triangle count (default: 10000)')
    parser.add_argument('--generate-lods', action='store_true',
                       help='Generate LOD levels')
    parser.add_argument('--lod-levels', type=int, default=3,
                       help='Number of LOD levels (default: 3)')
    parser.add_argument('--format', '-f', choices=['OBJ', 'PLY', 'GLB', 'STL'],
                       help='Output format')
    parser.add_argument('--use-meshlab', action='store_true',
                       help='Use PyMeshLab for advanced optimization')

    args = parser.parse_args()

    input_path = Path(args.input)
    if not input_path.exists():
        print(f"Error: Input file not found: {args.input}", file=sys.stderr)
        sys.exit(1)

    # Determine output path
    if args.output:
        output_path = Path(args.output)
    else:
        output_path = input_path.parent / f"{input_path.stem}_optimized{input_path.suffix}"

    # Use PyMeshLab if requested and available
    if args.use_meshlab and MESHLAB_AVAILABLE:
        success = optimize_mesh_pymeshlab(input_path, output_path, args.target_tris)
        if success:
            sys.exit(0)
        else:
            print("Falling back to trimesh...")

    # Load mesh
    try:
        mesh = load_mesh(input_path)
    except Exception as e:
        print(f"Error loading mesh: {e}", file=sys.stderr)
        sys.exit(1)

    # Optimize
    target_faces = args.target_tris
    optimized = decimate_mesh(mesh, target_faces)

    # Export main optimized mesh
    export_mesh(optimized, output_path, args.format)

    # Generate LODs if requested
    if args.generate_lods:
        lods = generate_lods(optimized, args.lod_levels)

        # Export LODs
        for i, lod_mesh in enumerate(lods[1:], 1):  # Skip LOD0 (already exported)
            lod_path = output_path.parent / f"{output_path.stem}_LOD{i}{output_path.suffix}"
            export_mesh(lod_mesh, lod_path, args.format)

    print("\n✓ Optimization complete!")

    sys.exit(0)


if __name__ == '__main__':
    main()
