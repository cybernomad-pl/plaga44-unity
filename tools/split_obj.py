#!/usr/bin/env python3
"""
Split an OBJ file into connected components (independent geometric parts).

Connected components are groups of triangles/faces sharing vertices.
Each component is exported as a separate OBJ file, sorted by face count (largest first).

Modes:
  Single file:
    python3 split_obj.py input.obj [output_dir/]

  Batch (old flat structure):
    python3 split_obj.py --batch input_dir/ [output_dir/]

  Batch per-model folders (new structure):
    python3 split_obj.py --batch-models obj_dir/
    Scans obj_dir/ for ModelName/ModelName_full.obj, splits into ModelName/parts/,
    and updates manifest.json.
"""

import sys
import os
import time
import json
import numpy as np


class OBJData:
    """Minimal OBJ parser that preserves original data."""

    def __init__(self):
        self.vertices = []       # list of (x, y, z)
        self.normals = []        # list of (nx, ny, nz)
        self.texcoords = []      # list of (u, v, ...)
        self.faces = []          # list of face-vertex tuples: [(vi, vti, vni), ...]

    def load(self, filepath):
        print(f"  Parsing OBJ: {os.path.basename(filepath)} ...")
        t0 = time.time()
        with open(filepath, 'r') as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith('#'):
                    continue
                parts = line.split()
                key = parts[0]

                if key == 'v' and len(parts) >= 4:
                    self.vertices.append((float(parts[1]), float(parts[2]), float(parts[3])))
                elif key == 'vn' and len(parts) >= 4:
                    self.normals.append((float(parts[1]), float(parts[2]), float(parts[3])))
                elif key == 'vt' and len(parts) >= 3:
                    self.texcoords.append(tuple(float(x) for x in parts[1:]))
                elif key == 'f':
                    face_verts = []
                    for fv in parts[1:]:
                        indices = fv.split('/')
                        vi = int(indices[0])
                        vti = int(indices[1]) if len(indices) > 1 and indices[1] else 0
                        vni = int(indices[2]) if len(indices) > 2 and indices[2] else 0
                        face_verts.append((vi, vti, vni))
                    self.faces.append(face_verts)

        dt = time.time() - t0
        print(f"    Vertices: {len(self.vertices)}, Normals: {len(self.normals)}, "
              f"TexCoords: {len(self.texcoords)}, Faces: {len(self.faces)}  ({dt:.1f}s)")
        return self


def find_connected_components(obj_data):
    """Find connected components using Union-Find on vertex indices."""
    print(f"  Finding connected components ...")
    t0 = time.time()

    n_verts = len(obj_data.vertices)
    if n_verts == 0:
        return []

    # Union-Find
    parent = list(range(n_verts + 1))  # 1-indexed
    rank = [0] * (n_verts + 1)

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra == rb:
            return
        if rank[ra] < rank[rb]:
            ra, rb = rb, ra
        parent[rb] = ra
        if rank[ra] == rank[rb]:
            rank[ra] += 1

    # Union all vertices in each face
    for face in obj_data.faces:
        first_vi = face[0][0]
        for fv in face[1:]:
            union(first_vi, fv[0])

    # Group faces by component root
    component_faces = {}
    for fi, face in enumerate(obj_data.faces):
        root = find(face[0][0])
        if root not in component_faces:
            component_faces[root] = []
        component_faces[root].append(fi)

    # Sort by face count descending
    components = sorted(component_faces.values(), key=len, reverse=True)

    dt = time.time() - t0
    print(f"    Found {len(components)} components in {dt:.1f}s")
    return components


def export_component(obj_data, face_indices, output_path):
    """Export a subset of faces as a new OBJ file with remapped indices."""
    used_v = set()
    used_vt = set()
    used_vn = set()

    for fi in face_indices:
        for (vi, vti, vni) in obj_data.faces[fi]:
            used_v.add(vi)
            if vti: used_vt.add(vti)
            if vni: used_vn.add(vni)

    sorted_v = sorted(used_v)
    sorted_vt = sorted(used_vt)
    sorted_vn = sorted(used_vn)

    v_map = {old: new for new, old in enumerate(sorted_v, 1)}
    vt_map = {old: new for new, old in enumerate(sorted_vt, 1)}
    vn_map = {old: new for new, old in enumerate(sorted_vn, 1)}

    with open(output_path, 'w') as f:
        f.write(f"# Split component: {len(face_indices)} faces, {len(sorted_v)} vertices\n")

        for vi in sorted_v:
            x, y, z = obj_data.vertices[vi - 1]
            f.write(f"v {x} {y} {z}\n")

        for vti in sorted_vt:
            coords = obj_data.texcoords[vti - 1]
            f.write(f"vt {' '.join(str(c) for c in coords)}\n")

        for vni in sorted_vn:
            nx, ny, nz = obj_data.normals[vni - 1]
            f.write(f"vn {nx} {ny} {nz}\n")

        for fi in face_indices:
            face = obj_data.faces[fi]
            parts = []
            for (vi, vti, vni) in face:
                new_vi = v_map[vi]
                if vti and vni:
                    parts.append(f"{new_vi}/{vt_map[vti]}/{vn_map[vni]}")
                elif vti:
                    parts.append(f"{new_vi}/{vt_map[vti]}")
                elif vni:
                    parts.append(f"{new_vi}//{vn_map[vni]}")
                else:
                    parts.append(f"{new_vi}")
            f.write(f"f {' '.join(parts)}\n")


def split_obj(input_path, output_dir, min_faces=0):
    """Split an OBJ file into connected components. Returns list of created part filenames (relative)."""
    base = os.path.splitext(os.path.basename(input_path))[0]
    # Strip _full suffix for part naming
    part_base = base
    if part_base.endswith('_full'):
        part_base = part_base[:-5]

    os.makedirs(output_dir, exist_ok=True)

    obj = OBJData().load(input_path)

    if not obj.faces:
        print(f"  WARNING: No faces found in {input_path}")
        return []

    components = find_connected_components(obj)

    if len(components) <= 1:
        print(f"  Model is a single connected component - no split needed")
        out_name = f"{part_base}_part01.obj"
        out_path = os.path.join(output_dir, out_name)
        export_component(obj, components[0] if components else list(range(len(obj.faces))), out_path)
        return [out_name]

    print(f"\n  Exporting {len(components)} parts ...")
    created = []
    report_lines = []

    for i, comp_faces in enumerate(components):
        n_faces = len(comp_faces)
        if n_faces < min_faces:
            continue

        used_v = set()
        for fi in comp_faces:
            for (vi, vti, vni) in obj.faces[fi]:
                used_v.add(vi)
        n_verts = len(used_v)

        out_name = f"{part_base}_part{i+1:02d}.obj"
        out_path = os.path.join(output_dir, out_name)
        export_component(obj, comp_faces, out_path)

        sz = os.path.getsize(out_path) / 1024
        report_lines.append(f"    part{i+1:02d}: {n_faces:>8} faces, {n_verts:>8} verts  ({sz:.1f} KB)")
        created.append(out_name)

    print(f"\n  REPORT for {part_base}:")
    print(f"  Total parts: {len(created)}")
    for line in report_lines[:20]:  # show first 20
        print(line)
    if len(report_lines) > 20:
        print(f"    ... and {len(report_lines) - 20} more")

    return created


def update_manifest(model_dir, parts_list):
    """Update manifest.json in model_dir with new parts list."""
    manifest_path = os.path.join(model_dir, "manifest.json")
    if not os.path.isfile(manifest_path):
        print(f"  WARNING: No manifest.json in {model_dir}")
        return

    with open(manifest_path) as f:
        manifest = json.load(f)

    manifest["parts"] = sorted([f"parts/{p}" for p in parts_list])
    manifest["stats"]["num_parts"] = len(parts_list)

    with open(manifest_path, 'w') as f:
        json.dump(manifest, f, indent=2)

    print(f"  Updated manifest: {len(parts_list)} parts")


def batch_models(obj_dir):
    """Process all per-model folders: split ModelName_full.obj into parts/."""
    print(f"Scanning model folders in: {obj_dir}")
    all_created = 0

    for entry in sorted(os.listdir(obj_dir)):
        model_dir = os.path.join(obj_dir, entry)
        if not os.path.isdir(model_dir):
            continue
        manifest_path = os.path.join(model_dir, "manifest.json")
        if not os.path.isfile(manifest_path):
            continue

        with open(manifest_path) as f:
            manifest = json.load(f)

        full_obj = manifest.get("full")
        if not full_obj:
            continue

        full_obj_path = os.path.join(model_dir, full_obj)
        if not os.path.isfile(full_obj_path):
            print(f"\n  SKIP {entry}: {full_obj} not found")
            continue

        # Check if parts already exist
        existing_parts = manifest.get("parts", [])
        if existing_parts:
            print(f"\n  SKIP {entry}: already has {len(existing_parts)} parts")
            all_created += len(existing_parts)
            continue

        print(f"\n{'='*60}")
        print(f"  Splitting: {entry}")
        print(f"{'='*60}")

        parts_dir = os.path.join(model_dir, "parts")
        part_names = split_obj(full_obj_path, parts_dir)
        update_manifest(model_dir, part_names)
        all_created += len(part_names)

    # Regenerate index.json
    manifests = []
    for entry in sorted(os.listdir(obj_dir)):
        manifest_path = os.path.join(obj_dir, entry, "manifest.json")
        if os.path.isfile(manifest_path):
            with open(manifest_path) as f:
                manifests.append(json.load(f))

    if manifests:
        index = {
            "models": [m["name"] for m in manifests],
            "count": len(manifests),
            "manifests": {m["name"]: m for m in manifests}
        }
        index_path = os.path.join(obj_dir, "index.json")
        with open(index_path, 'w') as f:
            json.dump(index, f, indent=2)

    print(f"\n{'='*60}")
    print(f"  BATCH COMPLETE: {all_created} total parts across {len(manifests)} models")
    print(f"{'='*60}")


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    if sys.argv[1] == "--batch-models":
        # New mode: process per-model folder structure
        if len(sys.argv) < 3:
            print("Usage: python3 split_obj.py --batch-models obj_dir/")
            sys.exit(1)
        batch_models(sys.argv[2])

    elif sys.argv[1] == "--batch":
        # Legacy batch mode
        if len(sys.argv) < 3:
            print("Usage: python3 split_obj.py --batch input_dir/ [output_dir/]")
            sys.exit(1)
        input_dir = sys.argv[2]
        output_dir = sys.argv[3] if len(sys.argv) > 3 else os.path.join(input_dir, "parts")

        import glob as globmod
        obj_files = sorted(globmod.glob(os.path.join(input_dir, "*.obj")))
        if not obj_files:
            print(f"No OBJ files found in {input_dir}")
            sys.exit(1)

        print(f"Batch mode: {len(obj_files)} OBJ files")
        all_created = []
        for obj_path in obj_files:
            base = os.path.splitext(os.path.basename(obj_path))[0]
            part_dir = os.path.join(output_dir, base)
            result = split_obj(obj_path, part_dir)
            all_created.extend(result)

        print(f"\n{'='*60}")
        print(f"  BATCH COMPLETE: {len(all_created)} total parts created")
        print(f"{'='*60}")
    else:
        input_path = sys.argv[1]
        if len(sys.argv) > 2:
            output_dir = sys.argv[2]
        else:
            base = os.path.splitext(os.path.basename(input_path))[0]
            output_dir = os.path.join(os.path.dirname(input_path), f"{base}_parts")

        split_obj(input_path, output_dir)


if __name__ == "__main__":
    main()
