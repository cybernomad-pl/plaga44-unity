#!/usr/bin/env python3
"""
FBX -> OBJ conversion pipeline.
Uses FBX2glTF (FBX->GLB) then trimesh (GLB->OBJ).
Exports per-model folders with MTL, textures, and manifest.json.

Output structure:
    obj/
      ModelName/
        ModelName_full.obj       # merged full mesh (all sub-meshes)
        ModelName_full.mtl       # material with texture refs
        submeshes/               # only if multiple geometries in FBX
          ModelName_mesh_00_xxx.obj
          ...
        textures/
          diffuse.png
          normal.png
          ...
        manifest.json

Usage:
    python3 convert_fbx_to_obj.py [input_dir] [output_dir]

Defaults:
    input_dir  = directory where this script lives
    output_dir = input_dir/obj/
"""

import sys
import os
import subprocess
import shutil
import tempfile
import time
import json
import glob as globmod
import re

# ---- config ----
FBX2GLTF = "/tmp/fbx2gltf_bin/FBX2glTF-linux-x86_64/FBX2glTF-linux-x86_64"
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
DEFAULT_INPUT = SCRIPT_DIR
DEFAULT_OUTPUT = os.path.join(SCRIPT_DIR, "obj")


def find_textures_for_fbx(fbx_path):
    """Find texture files associated with an FBX file (.fbm folder)."""
    fbm_dir = os.path.splitext(fbx_path)[0] + ".fbm"
    textures = {}  # type -> filepath
    if os.path.isdir(fbm_dir):
        for f in os.listdir(fbm_dir):
            fl = f.lower()
            if not (fl.endswith('.png') or fl.endswith('.jpg') or fl.endswith('.jpeg') or fl.endswith('.tga') or fl.endswith('.bmp')):
                continue
            fpath = os.path.join(fbm_dir, f)
            if 'diffuse' in fl or 'albedo' in fl or 'color' in fl or 'basecolor' in fl:
                textures['diffuse'] = fpath
            elif 'normal' in fl or 'nrm' in fl:
                textures['normal'] = fpath
            elif 'specular' in fl or 'spec' in fl:
                textures['specular'] = fpath
            elif 'glossiness' in fl or 'gloss' in fl or 'roughness' in fl:
                textures['glossiness'] = fpath
            else:
                textures.setdefault('other', [])
                if isinstance(textures.get('other'), list):
                    textures['other'].append(fpath)
    return textures


def copy_textures_to_model_dir(textures, model_dir):
    """Copy texture files to model_dir/textures/. Returns list of relative paths and tex_map."""
    tex_dir = os.path.join(model_dir, "textures")
    tex_rel_paths = []
    tex_map = {}  # type -> relative path for MTL
    files_to_copy = []
    for tex_type, path in textures.items():
        if tex_type == 'other' and isinstance(path, list):
            for p in path:
                files_to_copy.append(('other', p))
        else:
            files_to_copy.append((tex_type, path))

    if not files_to_copy:
        return tex_rel_paths, tex_map

    os.makedirs(tex_dir, exist_ok=True)
    for tex_type, src_path in files_to_copy:
        fname = os.path.basename(src_path)
        dst_path = os.path.join(tex_dir, fname)
        shutil.copy2(src_path, dst_path)
        rel_path = f"textures/{fname}"
        tex_rel_paths.append(rel_path)
        if tex_type != 'other':
            tex_map[tex_type] = rel_path
        print(f"    Texture [{tex_type}]: {fname}")
    return sorted(tex_rel_paths), tex_map


def write_mtl_with_textures(mtl_path, material_names, tex_map):
    """Write MTL file with texture map references (relative to model dir)."""
    with open(mtl_path, 'w') as f:
        f.write("# MTL with texture maps\n\n")
        for mat_name in material_names:
            f.write(f"newmtl {mat_name}\n")
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
    print(f"    MTL written: {os.path.basename(mtl_path)} ({len(material_names)} materials)")


def extract_material_names_from_obj(obj_path):
    """Read an OBJ file and extract usemtl names."""
    names = []
    with open(obj_path, 'r') as f:
        for line in f:
            if line.startswith('usemtl '):
                name = line.strip().split(None, 1)[1]
                if name not in names:
                    names.append(name)
    return names


def patch_obj_mtllib(obj_path, mtl_filename):
    """Ensure OBJ file references the correct MTL file."""
    with open(obj_path, 'r') as f:
        content = f.read()
    if 'mtllib ' in content:
        content = re.sub(r'mtllib\s+\S+', f'mtllib {mtl_filename}', content)
    else:
        content = f"mtllib {mtl_filename}\n" + content
    with open(obj_path, 'w') as f:
        f.write(content)


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
    """Merge multiple OBJ files into one, adjusting vertex indices."""
    v_offset = 0
    vt_offset = 0
    vn_offset = 0

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
                        continue
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
                        parts = stripped.split()
                        new_parts = ['f']
                        for fv in parts[1:]:
                            indices = fv.split('/')
                            vi = int(indices[0]) + v_offset
                            vti = (int(indices[1]) + vt_offset) if (len(indices) > 1 and indices[1]) else None
                            vni = (int(indices[2]) + vn_offset) if (len(indices) > 2 and indices[2]) else None
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


def write_manifest(model_dir, model_name, full_obj, full_mtl, submeshes, textures, total_verts, total_faces):
    """Write manifest.json for a model directory."""
    # Check for existing parts
    parts_dir = os.path.join(model_dir, "parts")
    parts = []
    if os.path.isdir(parts_dir):
        parts = sorted([f"parts/{f}" for f in os.listdir(parts_dir) if f.endswith('.obj')])

    manifest = {
        "name": model_name,
        "full": full_obj,
        "mtl": full_mtl,
        "submeshes": submeshes,
        "parts": parts,
        "textures": textures,
        "stats": {
            "total_verts": total_verts,
            "total_faces": total_faces,
            "num_submeshes": max(1, len(submeshes)),
            "num_parts": len(parts)
        }
    }

    manifest_path = os.path.join(model_dir, "manifest.json")
    with open(manifest_path, 'w') as f:
        json.dump(manifest, f, indent=2)

    print(f"    manifest.json: V={total_verts}, F={total_faces}, parts={len(parts)}")
    return manifest


def convert_one(fbx_path, output_dir, fbx2gltf=FBX2GLTF):
    """Convert a single FBX file into a per-model folder. Returns manifest dict or None."""
    base = os.path.splitext(os.path.basename(fbx_path))[0]
    model_dir = os.path.join(output_dir, base)

    print(f"\n{'='*60}")
    print(f"  Converting: {os.path.basename(fbx_path)}")
    print(f"  Size: {os.path.getsize(fbx_path) / 1e6:.1f} MB")
    print(f"  Output: {model_dir}")
    print(f"{'='*60}")

    # Find textures before conversion
    textures = find_textures_for_fbx(fbx_path)
    if textures:
        print(f"  Found textures: {list(textures.keys())}")

    # Copy FBX to /tmp to avoid WSL path issues with FBX2glTF
    tmp_dir = tempfile.mkdtemp(prefix=f"fbx_{base}_")
    tmp_fbx = os.path.join(tmp_dir, os.path.basename(fbx_path))
    print(f"  Copying to {tmp_fbx} ...")
    shutil.copy2(fbx_path, tmp_fbx)

    fbm_dir = os.path.splitext(fbx_path)[0] + ".fbm"
    if os.path.isdir(fbm_dir):
        tmp_fbm = os.path.join(tmp_dir, os.path.basename(fbm_dir))
        shutil.copytree(fbm_dir, tmp_fbm)

    # FBX -> GLB
    glb_output = os.path.join(tmp_dir, base)
    cmd = [fbx2gltf, "-b", "-i", tmp_fbx, "-o", glb_output]
    print(f"  Running FBX2glTF ...")
    t0 = time.time()
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=300)
        dt = time.time() - t0
        print(f"  FBX2glTF done in {dt:.1f}s (exit code {result.returncode})")
        if result.returncode != 0:
            print(f"  STDERR: {result.stderr[:500]}")
            shutil.rmtree(tmp_dir, ignore_errors=True)
            return None
    except subprocess.TimeoutExpired:
        print(f"  TIMEOUT after 300s - skipping")
        shutil.rmtree(tmp_dir, ignore_errors=True)
        return None
    except Exception as e:
        print(f"  ERROR: {e}")
        shutil.rmtree(tmp_dir, ignore_errors=True)
        return None

    # Find GLB file
    glb_path = glb_output + ".glb"
    if not os.path.exists(glb_path):
        candidates = globmod.glob(os.path.join(tmp_dir, "*.glb"))
        if candidates:
            glb_path = candidates[0]
        else:
            print(f"  ERROR: No GLB file produced")
            shutil.rmtree(tmp_dir, ignore_errors=True)
            return None

    print(f"  GLB size: {os.path.getsize(glb_path) / 1e6:.1f} MB")

    # GLB -> OBJ via trimesh
    print(f"  Loading GLB with trimesh ...")
    t0 = time.time()
    try:
        import trimesh
        scene = trimesh.load(glb_path, force=None)
        dt = time.time() - t0
        print(f"  Loaded in {dt:.1f}s")
    except Exception as e:
        print(f"  ERROR loading GLB: {e}")
        shutil.rmtree(tmp_dir, ignore_errors=True)
        return None

    # Create model directory structure
    os.makedirs(model_dir, exist_ok=True)

    # Export sub-meshes to a temp list
    submesh_paths = []  # full paths to exported OBJ files (temp in model_dir)

    if isinstance(scene, trimesh.Scene):
        geometries = list(scene.geometry.items())
        print(f"  Scene has {len(geometries)} geometries")

        if len(geometries) == 1:
            name, mesh = geometries[0]
            out_path = os.path.join(model_dir, f"{base}_full.obj")
            mesh.export(out_path, file_type="obj")
            verts = len(mesh.vertices)
            faces = len(mesh.faces)
            print(f"    -> {os.path.basename(out_path)}  (V={verts}, F={faces})")
            submesh_paths.append(out_path)
        else:
            # Export individual sub-meshes, then merge
            submesh_dir = os.path.join(model_dir, "submeshes")
            os.makedirs(submesh_dir, exist_ok=True)
            for i, (name, mesh) in enumerate(geometries):
                if not hasattr(mesh, 'vertices') or len(mesh.vertices) == 0:
                    continue
                safe_name = "".join(c if c.isalnum() or c in "._-" else "_" for c in name)
                out_path = os.path.join(submesh_dir, f"{base}_mesh_{i:02d}_{safe_name}.obj")
                mesh.export(out_path, file_type="obj")
                verts = len(mesh.vertices)
                faces = len(mesh.faces)
                print(f"    -> {os.path.basename(out_path)}  (V={verts}, F={faces})")
                submesh_paths.append(out_path)
    elif isinstance(scene, trimesh.Trimesh):
        out_path = os.path.join(model_dir, f"{base}_full.obj")
        scene.export(out_path, file_type="obj")
        verts = len(scene.vertices)
        faces = len(scene.faces)
        print(f"    -> {os.path.basename(out_path)}  (V={verts}, F={faces})")
        submesh_paths.append(out_path)
    else:
        print(f"  WARNING: Unknown type: {type(scene)}")
        shutil.rmtree(tmp_dir, ignore_errors=True)
        return None

    if not submesh_paths:
        print(f"  ERROR: No meshes exported")
        shutil.rmtree(tmp_dir, ignore_errors=True)
        return None

    # Create merged full OBJ if multiple sub-meshes
    full_obj_name = f"{base}_full.obj"
    full_obj_path = os.path.join(model_dir, full_obj_name)
    submesh_rel = []

    if len(submesh_paths) > 1:
        merge_obj_files(submesh_paths, full_obj_path)
        print(f"  Merged {len(submesh_paths)} sub-meshes -> {full_obj_name}")
        submesh_rel = [f"submeshes/{os.path.basename(p)}" for p in submesh_paths]
    # else: single mesh was already exported as _full.obj

    # Copy textures
    tex_rel_paths, tex_map = copy_textures_to_model_dir(textures, model_dir)

    # Write MTL
    full_mtl_name = f"{base}_full.mtl"
    full_mtl_path = os.path.join(model_dir, full_mtl_name)

    mat_names = extract_material_names_from_obj(full_obj_path)
    if not mat_names:
        mat_names = [f"{base}_mat"]
    write_mtl_with_textures(full_mtl_path, mat_names, tex_map)

    # Patch full OBJ to reference MTL
    patch_obj_mtllib(full_obj_path, full_mtl_name)

    # Also patch sub-mesh OBJs if they exist
    if len(submesh_paths) > 1:
        for sp in submesh_paths:
            patch_obj_mtllib(sp, f"../{full_mtl_name}")

    # Count stats from full OBJ
    total_verts, total_faces = count_obj_stats(full_obj_path)

    # Write manifest (parts may not exist yet - split_obj adds them later)
    manifest = write_manifest(
        model_dir, base, full_obj_name, full_mtl_name,
        submesh_rel, tex_rel_paths, total_verts, total_faces
    )

    # Cleanup
    shutil.rmtree(tmp_dir, ignore_errors=True)
    return manifest


def write_index(output_dir, manifests):
    """Write top-level index.json."""
    index = {
        "models": [m["name"] for m in manifests],
        "count": len(manifests),
        "manifests": {m["name"]: m for m in manifests}
    }
    index_path = os.path.join(output_dir, "index.json")
    with open(index_path, 'w') as f:
        json.dump(index, f, indent=2)
    print(f"  Index written: {index_path}")


def main():
    input_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_INPUT
    output_dir = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_OUTPUT

    fbx_files = sorted(globmod.glob(os.path.join(input_dir, "*.fbx")))
    if not fbx_files:
        print(f"No FBX files found in {input_dir}")
        sys.exit(1)

    print(f"Found {len(fbx_files)} FBX files in {input_dir}")
    print(f"Output directory: {output_dir}")
    os.makedirs(output_dir, exist_ok=True)

    all_manifests = []
    skipped = []

    for fbx_path in fbx_files:
        basename = os.path.basename(fbx_path)
        base = os.path.splitext(basename)[0]

        # Check if already converted (model dir exists with manifest)
        model_dir = os.path.join(output_dir, base)
        manifest_path = os.path.join(model_dir, "manifest.json")
        if os.path.isfile(manifest_path):
            print(f"\n  SKIP {basename} - already have {model_dir}")
            with open(manifest_path) as f:
                all_manifests.append(json.load(f))
            continue

        manifest = convert_one(fbx_path, output_dir)
        if manifest:
            all_manifests.append(manifest)
        else:
            skipped.append(basename)

    # Write index
    write_index(output_dir, all_manifests)

    print(f"\n{'='*60}")
    print(f"  SUMMARY")
    print(f"{'='*60}")
    print(f"  Models: {len(all_manifests)}")
    for m in all_manifests:
        s = m["stats"]
        print(f"    {m['name']}  V={s['total_verts']} F={s['total_faces']} parts={s['num_parts']}")
    if skipped:
        print(f"  Skipped/Failed: {len(skipped)}")
        for s in skipped:
            print(f"    {s}")


if __name__ == "__main__":
    main()
