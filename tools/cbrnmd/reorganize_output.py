#!/usr/bin/env python3
"""
Reorganize flat OBJ output into per-model folder structure.

Reads existing obj/ directory and parts/ subdirectory,
groups sub-meshes by source FBX name, and creates:

  obj/
    ModelName/
      ModelName_full.obj       # merged from all sub-meshes (or single mesh)
      ModelName_full.mtl
      parts/
        ModelName_partNN.obj   # from split_obj connected components
      textures/
        diffuse.png
        normal.png
        ...
      manifest.json

"""

import os
import sys
import json
import shutil
import re
import glob as globmod

# Force unbuffered output
sys.stdout = os.fdopen(sys.stdout.fileno(), 'w', buffering=1)
sys.stderr = os.fdopen(sys.stderr.fileno(), 'w', buffering=1)

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
OBJ_DIR = os.path.join(SCRIPT_DIR, "obj")
PARTS_DIR = os.path.join(OBJ_DIR, "parts")
FBX_DIR = SCRIPT_DIR


def get_fbx_base_names():
    """Get base names of all FBX files."""
    fbx_files = globmod.glob(os.path.join(FBX_DIR, "*.fbx"))
    return [os.path.splitext(os.path.basename(f))[0] for f in fbx_files]


def group_obj_files_by_model(obj_dir):
    """
    Group OBJ files by their source FBX model.
    e.g. Ch11_nonPBR_mesh_00_Ch11.obj -> model 'Ch11_nonPBR'
         Ch20_nonPBR.obj -> model 'Ch20_nonPBR'
    """
    fbx_bases = sorted(get_fbx_base_names(), key=lambda x: -len(x))  # longest first for matching

    obj_files = [f for f in os.listdir(obj_dir)
                 if f.endswith('.obj') and os.path.isfile(os.path.join(obj_dir, f))]

    groups = {}  # model_name -> [obj filenames]

    for obj_name in obj_files:
        matched = False
        for fbx_base in fbx_bases:
            # Match: exact name, or name_mesh_NN_xxx
            if obj_name == fbx_base + '.obj' or obj_name.startswith(fbx_base + '_mesh_'):
                if fbx_base not in groups:
                    groups[fbx_base] = []
                groups[fbx_base].append(obj_name)
                matched = True
                break
        if not matched:
            print(f"  WARNING: Could not match {obj_name} to any FBX model")

    return groups


def find_textures_for_model(model_name):
    """Find .fbm texture directory for a model."""
    fbm_dir = os.path.join(FBX_DIR, model_name + ".fbm")
    textures = []
    if os.path.isdir(fbm_dir):
        for f in os.listdir(fbm_dir):
            fl = f.lower()
            if fl.endswith(('.png', '.jpg', '.jpeg', '.tga', '.bmp')):
                textures.append(os.path.join(fbm_dir, f))
    return textures


def find_parts_for_submesh(submesh_name):
    """Find split parts for a given sub-mesh name."""
    parts_subdir = os.path.join(PARTS_DIR, submesh_name)
    if not os.path.isdir(parts_subdir):
        return []
    parts = []
    for f in sorted(os.listdir(parts_subdir)):
        if f.endswith('.obj'):
            parts.append(os.path.join(parts_subdir, f))
    return parts


def count_obj_stats(filepath):
    """Count vertices and faces in an OBJ file."""
    verts = 0
    faces = 0
    with open(filepath, 'r') as f:
        for line in f:
            if line.startswith('v '):
                verts += 1
            elif line.startswith('f '):
                faces += 1
    return verts, faces


def merge_obj_files(obj_paths, output_path):
    """
    Merge multiple OBJ files into one, adjusting vertex indices.
    Returns the MTL filename referenced (if any).
    """
    v_offset = 0
    vt_offset = 0
    vn_offset = 0
    mtl_ref = None

    with open(output_path, 'w') as out:
        out.write("# Merged OBJ file\n")

        for obj_path in obj_paths:
            local_v = 0
            local_vt = 0
            local_vn = 0
            submesh_name = os.path.splitext(os.path.basename(obj_path))[0]
            out.write(f"\n# --- submesh: {submesh_name} ---\n")

            with open(obj_path, 'r') as inp:
                for line in inp:
                    stripped = line.strip()
                    if not stripped or stripped.startswith('#'):
                        continue

                    if stripped.startswith('mtllib '):
                        if mtl_ref is None:
                            mtl_ref = stripped.split(None, 1)[1]
                        continue  # skip, we'll write our own

                    if stripped.startswith('v '):
                        out.write(line)
                        local_v += 1
                    elif stripped.startswith('vt '):
                        out.write(line)
                        local_vt += 1
                    elif stripped.startswith('vn '):
                        out.write(line)
                        local_vn += 1
                    elif stripped.startswith('usemtl '):
                        out.write(line)
                    elif stripped.startswith('f '):
                        # Remap face indices
                        parts = stripped.split()
                        new_parts = ['f']
                        for fv in parts[1:]:
                            indices = fv.split('/')
                            vi = int(indices[0]) + v_offset
                            if len(indices) > 1 and indices[1]:
                                vti = int(indices[1]) + vt_offset
                            else:
                                vti = None
                            if len(indices) > 2 and indices[2]:
                                vni = int(indices[2]) + vn_offset
                            else:
                                vni = None

                            if vti is not None and vni is not None:
                                new_parts.append(f"{vi}/{vti}/{vni}")
                            elif vti is not None:
                                new_parts.append(f"{vi}/{vti}")
                            elif vni is not None:
                                new_parts.append(f"{vi}//{vni}")
                            else:
                                new_parts.append(f"{vi}")
                        out.write(' '.join(new_parts) + '\n')
                    elif stripped.startswith('g ') or stripped.startswith('o '):
                        out.write(line)

            v_offset += local_v
            vt_offset += local_vt
            vn_offset += local_vn

    return mtl_ref


def write_mtl_for_model(mtl_path, model_name, texture_files):
    """Write an MTL file with texture references pointing to textures/ subdir."""
    tex_map = {}  # type -> relative path
    for tex_path in texture_files:
        fname = os.path.basename(tex_path)
        fl = fname.lower()
        rel = f"textures/{fname}"
        if 'diffuse' in fl or 'albedo' in fl or 'color' in fl or 'basecolor' in fl:
            tex_map['diffuse'] = rel
        elif 'normal' in fl or 'nrm' in fl:
            tex_map['normal'] = rel
        elif 'specular' in fl or 'spec' in fl:
            tex_map['specular'] = rel
        elif 'glossiness' in fl or 'gloss' in fl or 'roughness' in fl:
            tex_map['glossiness'] = rel

    with open(mtl_path, 'w') as f:
        f.write(f"# MTL for {model_name}\n\n")
        f.write(f"newmtl {model_name}_mat\n")
        f.write("Ka 0.20000000 0.20000000 0.20000000\n")
        f.write("Kd 1.00000000 1.00000000 1.00000000\n")
        f.write("Ks 0.30000000 0.30000000 0.30000000\n")
        f.write("Ns 20.00000000\n")
        f.write("d 1.0\n")
        if 'diffuse' in tex_map:
            f.write(f"map_Kd {tex_map['diffuse']}\n")
        if 'specular' in tex_map:
            f.write(f"map_Ks {tex_map['specular']}\n")
        if 'normal' in tex_map:
            f.write(f"bump {tex_map['normal']}\n")
        if 'glossiness' in tex_map:
            f.write(f"map_Ns {tex_map['glossiness']}\n")
        f.write("\n")

    return tex_map


def reorganize():
    print(f"OBJ dir: {OBJ_DIR}")
    print(f"Parts dir: {PARTS_DIR}")
    print(f"FBX dir: {FBX_DIR}")

    # Group existing OBJ files by model
    groups = group_obj_files_by_model(OBJ_DIR)
    print(f"\nFound {len(groups)} models:")
    for name, objs in sorted(groups.items()):
        print(f"  {name}: {len(objs)} sub-mesh(es)")

    all_manifests = []

    for model_name in sorted(groups.keys()):
        obj_names = sorted(groups[model_name])
        print(f"\n{'='*60}")
        print(f"  Model: {model_name}")
        print(f"  Sub-meshes: {obj_names}")

        # Create model directory
        model_dir = os.path.join(OBJ_DIR, model_name)
        os.makedirs(model_dir, exist_ok=True)
        parts_out_dir = os.path.join(model_dir, "parts")
        os.makedirs(parts_out_dir, exist_ok=True)
        tex_out_dir = os.path.join(model_dir, "textures")

        # --- Full OBJ ---
        full_obj_name = f"{model_name}_full.obj"
        full_obj_path = os.path.join(model_dir, full_obj_name)
        full_mtl_name = f"{model_name}_full.mtl"
        full_mtl_path = os.path.join(model_dir, full_mtl_name)

        src_obj_paths = [os.path.join(OBJ_DIR, n) for n in obj_names]

        if len(src_obj_paths) == 1:
            # Single mesh - just copy
            shutil.copy2(src_obj_paths[0], full_obj_path)
            print(f"  Copied single mesh -> {full_obj_name}")
        else:
            # Merge multiple sub-meshes
            merge_obj_files(src_obj_paths, full_obj_path)
            print(f"  Merged {len(src_obj_paths)} sub-meshes -> {full_obj_name}")

        # Also copy individual sub-meshes as submeshes/ if more than 1
        submesh_names = []
        if len(src_obj_paths) > 1:
            submesh_dir = os.path.join(model_dir, "submeshes")
            os.makedirs(submesh_dir, exist_ok=True)
            for src in src_obj_paths:
                fname = os.path.basename(src)
                shutil.copy2(src, os.path.join(submesh_dir, fname))
                submesh_names.append(f"submeshes/{fname}")

        # --- Textures ---
        texture_files = find_textures_for_model(model_name)
        texture_rel_paths = []
        tex_map = {}
        if texture_files:
            os.makedirs(tex_out_dir, exist_ok=True)
            for tex_path in texture_files:
                fname = os.path.basename(tex_path)
                shutil.copy2(tex_path, os.path.join(tex_out_dir, fname))
                texture_rel_paths.append(f"textures/{fname}")
                print(f"  Texture: {fname}")
            tex_map = write_mtl_for_model(full_mtl_path, model_name, texture_files)
            print(f"  MTL: {full_mtl_name}")

            # Patch the full OBJ to reference the MTL
            with open(full_obj_path, 'r') as f:
                content = f.read()
            # Remove old mtllib lines and add new one at top
            content = re.sub(r'mtllib\s+\S+\n?', '', content)
            content = f"mtllib {full_mtl_name}\n" + content
            with open(full_obj_path, 'w') as f:
                f.write(content)
        else:
            # No textures - write minimal MTL
            write_mtl_for_model(full_mtl_path, model_name, [])
            # Patch OBJ
            with open(full_obj_path, 'r') as f:
                content = f.read()
            content = re.sub(r'mtllib\s+\S+\n?', '', content)
            content = f"mtllib {full_mtl_name}\n" + content
            with open(full_obj_path, 'w') as f:
                f.write(content)

        # --- Parts (connected components) ---
        all_parts = []
        for obj_name in obj_names:
            submesh_base = os.path.splitext(obj_name)[0]
            parts = find_parts_for_submesh(submesh_base)
            for part_path in parts:
                part_fname = os.path.basename(part_path)
                dst = os.path.join(parts_out_dir, part_fname)
                shutil.copy2(part_path, dst)
                all_parts.append(f"parts/{part_fname}")

        if not all_parts:
            # If no split parts exist, that's fine - just note it
            print(f"  No split parts found")
            # Clean up empty parts dir
            if os.path.isdir(parts_out_dir) and not os.listdir(parts_out_dir):
                os.rmdir(parts_out_dir)
        else:
            print(f"  Parts: {len(all_parts)}")

        # --- Stats ---
        total_verts, total_faces = count_obj_stats(full_obj_path)

        # --- Manifest ---
        manifest = {
            "name": model_name,
            "full": full_obj_name,
            "mtl": full_mtl_name,
            "submeshes": submesh_names,
            "parts": sorted(all_parts),
            "textures": sorted(texture_rel_paths),
            "stats": {
                "total_verts": total_verts,
                "total_faces": total_faces,
                "num_submeshes": len(obj_names),
                "num_parts": len(all_parts)
            }
        }

        manifest_path = os.path.join(model_dir, "manifest.json")
        with open(manifest_path, 'w') as f:
            json.dump(manifest, f, indent=2)

        print(f"  manifest.json: V={total_verts}, F={total_faces}, parts={len(all_parts)}")
        all_manifests.append(manifest)

    # --- Write top-level index ---
    index = {
        "models": [m["name"] for m in all_manifests],
        "count": len(all_manifests),
        "manifests": {m["name"]: m for m in all_manifests}
    }
    index_path = os.path.join(OBJ_DIR, "index.json")
    with open(index_path, 'w') as f:
        json.dump(index, f, indent=2)

    print(f"\n{'='*60}")
    print(f"  DONE: {len(all_manifests)} models reorganized")
    print(f"  Index: {index_path}")
    print(f"{'='*60}")


if __name__ == "__main__":
    reorganize()
