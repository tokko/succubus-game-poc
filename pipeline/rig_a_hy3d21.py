"""
Blender headless rigging + animation + FBX export for the succubus character.

Run with:
  blender.exe --background --python pipeline/rig_and_animate.py

Reads:  Assets/Characters/Succubus/succubus_pbr.glb       (preferred — PBR textures)
        Assets/Characters/Succubus/succubus_textured.glb   (fallback — simple texture)
        Assets/Characters/Succubus/succubus_shape.glb      (last fallback — no texture)
Writes: Assets/Characters/Succubus/succubus_rig_pbr.fbx
"""
import bpy
import os
import math

_BASE   = r"D:\claude projects\succubus-game-poc\Assets\Characters\Bakeoff\A_HY3D21"
_TEX_V21 = os.path.join(_BASE, "succubus_textured_v21.glb")  # v2.1 paintpbr output (preferred)
_TEX_V2  = os.path.join(_BASE, "succubus_textured_v2.glb")   # v2.0 paint output
_FIXED   = os.path.join(_BASE, "succubus_pbr_fixed.glb")     # geometry-only, no UVs
_PBR     = os.path.join(_BASE, "succubus_pbr.glb")
_TEX     = os.path.join(_BASE, "succubus_textured.glb")
_SHAPE   = os.path.join(_BASE, "succubus_shape.glb")

# Priority: v2.1 paintpbr > v2.0 paint > geometry-only fallbacks
if os.path.exists(_TEX_V21):
    GLB_IN = _TEX_V21
elif os.path.exists(_TEX_V2):
    GLB_IN = _TEX_V2
elif os.path.exists(_FIXED):
    GLB_IN = _FIXED
elif os.path.exists(_PBR):
    GLB_IN = _PBR
elif os.path.exists(_TEX):
    GLB_IN = _TEX
else:
    GLB_IN = _SHAPE
HAS_TEXTURE = GLB_IN not in (_SHAPE, _FIXED)
FBX_OUT = r"D:\claude projects\succubus-game-poc\Assets\Characters\Bakeoff\A_HY3D21\succubus_a.fbx"

print(f"[rig_and_animate] Input: {GLB_IN}  (has_texture={HAS_TEXTURE})")

# ── helpers ──────────────────────────────────────────────────────────────────

def deselect_all():
    bpy.ops.object.select_all(action='DESELECT')

def set_active(obj):
    deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

# ── 1. Clean scene ────────────────────────────────────────────────────────────
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

# ── 2. Enable Rigify ──────────────────────────────────────────────────────────
bpy.ops.preferences.addon_enable(module='rigify')
print("Rigify enabled")

# ── 3. Import GLB ─────────────────────────────────────────────────────────────
bpy.ops.import_scene.gltf(filepath=GLB_IN)
print(f"Imported: {GLB_IN}")

# Collect all mesh objects from import
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    raise RuntimeError("No mesh objects found after GLB import")

# ── 3a. Remove ground disk / base platform ────────────────────────────────────
# Hunyuan3D-2.1 sometimes generates a thin flat platform underneath the character.
# Detect SEPARATE OBJECTS where Z-extent < 4% of XY-extent AND near the scene bottom.
_all_z = [v[2] for m in meshes for v in m.bound_box]
_scene_z_min = min(_all_z) if _all_z else 0.0
_scene_z_max = max(_all_z) if _all_z else 1.0
_scene_h = max(_scene_z_max - _scene_z_min, 1e-6)
_filtered_meshes = []
for m in meshes:
    bb = m.bound_box
    z_lo = min(v[2] for v in bb); z_hi = max(v[2] for v in bb)
    x_ext = max(v[0] for v in bb) - min(v[0] for v in bb)
    y_ext = max(v[1] for v in bb) - min(v[1] for v in bb)
    z_ext  = z_hi - z_lo
    z_mid  = (z_lo + z_hi) * 0.5
    xy_ext = max(x_ext, y_ext, 1e-6)
    # z_mid relative to scene bottom (fixes centered-mesh case where z_mid is negative)
    z_mid_rel = z_mid - _scene_z_min
    is_disk = z_ext < 0.04 * xy_ext and z_mid_rel < 0.06 * _scene_h
    if is_disk:
        print(f"  Removing ground disk/platform: {m.name}  verts={len(m.data.vertices)}  "
              f"z_ext={z_ext:.4f}  xy_ext={xy_ext:.4f}  z_mid_rel={z_mid_rel:.4f}")
        bpy.data.objects.remove(m, do_unlink=True)
    else:
        _filtered_meshes.append(m)
meshes = _filtered_meshes or meshes  # fall back to all if heuristic removes everything

# Join all mesh pieces into one
deselect_all()
for m in meshes:
    m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.join()
mesh_obj = bpy.context.view_layer.objects.active
mesh_obj.name = "Succubus_Mesh"
print(f"Joined mesh: {mesh_obj.name}  verts={len(mesh_obj.data.vertices):,}")

# ── 3b. Delete flat upward-facing faces at ground level (disk merged into mesh) ─
# Uses bottom-relative Z so this works whether mesh is feet-at-0 or centered.
import bmesh as _bm
_bme = _bm.new()
_bme.from_mesh(mesh_obj.data)
_bme_z_min = min(v.co.z for v in _bme.verts)
_bme_z_max = max(v.co.z for v in _bme.verts)
_bme_h = max(_bme_z_max - _bme_z_min, 1e-6)
# Keep faces that have any vertex above 4% of character height from the bottom
_abs_ground = _bme_z_min + _bme_h * 0.04
_disk_faces = [f for f in _bme.faces
               if f.normal.z > 0.75            # mostly upward
               and all(v.co.z < _abs_ground for v in f.verts)  # all verts in bottom 4%
               and (max(v.co.z for v in f.verts) - min(v.co.z for v in f.verts)) < _bme_h * 0.008]  # very flat
if _disk_faces:
    _bm.ops.delete(_bme, geom=_disk_faces, context='FACES')
    # Also remove orphan vertices left by face deletion
    _orphans = [v for v in _bme.verts if not v.link_faces]
    if _orphans:
        _bm.ops.delete(_bme, geom=_orphans, context='VERTS')
    print(f"  Deleted {len(_disk_faces)} flat ground faces + {len(_orphans)} orphan verts "
          f"(z_min={_bme_z_min:.3f} threshold={_abs_ground:.3f})")
else:
    print(f"  No flat ground faces found (z_min={_bme_z_min:.3f} threshold={_abs_ground:.3f})")
_bme.to_mesh(mesh_obj.data)
_bme.free()

# ── 3c. Recalculate normals for consistent shading ───────────────────────────
set_active(mesh_obj)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
print("  Normals recalculated")

if HAS_TEXTURE and len(mesh_obj.data.materials) > 0:
    # Textured GLB already has materials with image textures — keep them
    print(f"  Keeping {len(mesh_obj.data.materials)} material(s) from textured GLB")
else:
    # Fallback: plain skin-tone Principled BSDF when no texture exists
    mat = bpy.data.materials.new("Succubus_Skin")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.80, 0.62, 0.55, 1.0)  # warm skin tone
    bsdf.inputs["Roughness"].default_value = 0.6
    bsdf.inputs["Specular IOR Level"].default_value = 0.3
    mesh_obj.data.materials.clear()
    mesh_obj.data.materials.append(mat)
    print("  Fallback skin material applied (no texture found)")

# ── 4. Pivot correction — move mesh so feet are at world Z = 0 ────────────────
bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
bbox = mesh_obj.bound_box  # 8 corners in local space
min_z = min(v[2] for v in bbox)
mesh_obj.location.z -= min_z  # shift up so min Z = 0
bpy.ops.object.transform_apply(location=True, scale=True, rotation=True)
bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
print(f"  Pivot corrected (was min_z={min_z:.4f})")

# ── 4b. Post-pivot disk sweep — now feet are exactly at Z=0 ──────────────────
# Diagnostic confirmed ~166 disk faces survive step 3b with verts up to 10mm from Z=0.
# Be aggressive: delete ANY upward-leaning face (normal.z > 0.5) where ALL verts
# are within 30mm of Z=0.  Foot-sole faces point DOWNWARD so the normal filter
# keeps them; foot-side faces near the sole have ankle verts well above 30mm.
import bmesh as _bm4b
_bme4b = _bm4b.new()
_bme4b.from_mesh(mesh_obj.data)
_post_tol = 0.030  # 30 mm — safely above the 10 mm where diagnostic found survivors
_post_disk = [f for f in _bme4b.faces
              if f.normal.z > 0.5                           # upward-leaning (was 0.75, too strict)
              and all(v.co.z < _post_tol for v in f.verts)] # ALL verts within 30mm of floor
_post_count = len(_post_disk)
if _post_disk:
    _bm4b.ops.delete(_bme4b, geom=_post_disk, context='FACES')
    _orphans4b = [v for v in _bme4b.verts if not v.link_faces]
    if _orphans4b:
        _bm4b.ops.delete(_bme4b, geom=_orphans4b, context='VERTS')
    print(f"  Post-pivot: deleted {_post_count} residual ground faces + {len(_orphans4b)} orphan verts (tol={_post_tol}m)")
else:
    print(f"  Post-pivot: no ground faces found (tol={_post_tol}m) — mesh may already be clean")
_bme4b.to_mesh(mesh_obj.data)
_bme4b.free()

# Get final bounding box for metarig placement
bbox_world = [mesh_obj.matrix_world @ mesh_obj.data.vertices[i].co
              for i in range(0, len(mesh_obj.data.vertices), max(1, len(mesh_obj.data.vertices)//100))]
min_z_w = min(v.z for v in bbox_world)
max_z_w = max(v.z for v in bbox_world)
mesh_height = max_z_w - min_z_w
print(f"  Mesh height: {mesh_height:.3f}m  (min_z={min_z_w:.3f}, max_z={max_z_w:.3f})")

# ── 5. Add Rigify human metarig ───────────────────────────────────────────────
bpy.ops.object.armature_human_metarig_add()
metarig = bpy.context.view_layer.objects.active
metarig.name = "Succubus_Metarig"

# Scale metarig to match mesh height
# Default Rigify metarig is ~2m; scale proportionally
default_height = 2.0
scale_factor = mesh_height / default_height
metarig.scale = (scale_factor, scale_factor, scale_factor)
metarig.location = (0, 0, 0)
bpy.ops.object.transform_apply(location=True, scale=True, rotation=True)
print(f"  Metarig scaled: {scale_factor:.3f}x")

# ── 6. Generate Rigify rig ────────────────────────────────────────────────────
bpy.context.view_layer.objects.active = metarig
bpy.ops.pose.rigify_generate()
rig = bpy.context.view_layer.objects.active
rig.name = "Succubus_Rig"
print(f"  Rig generated: {rig.name}")

# Remove the metarig (no longer needed)
metarig_obj = bpy.data.objects.get("Succubus_Metarig")
if metarig_obj:
    bpy.data.objects.remove(metarig_obj, do_unlink=True)

# ── 7. Expand DEF-bone envelopes so wings/horns fall inside, then parent ──────
# Only widen DEF- (deform) bones — control/MCH bones stay tiny so they don't
# pollute the weight table with irrelevant influences.
import mathutils

set_active(rig)
bpy.ops.object.mode_set(mode='EDIT')
for eb in rig.data.edit_bones:
    if eb.use_deform:                # DEF- bones only
        eb.envelope_distance = 0.5  # 50 cm radius — sweeps entire mesh
        eb.envelope_weight   = 1.0
    else:
        eb.envelope_distance = 0.0  # collapse non-deform bone envelopes to zero
bpy.ops.object.mode_set(mode='OBJECT')

deselect_all()
mesh_obj.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')

# Post-process 1: assign any still-unweighted vertices to the nearest DEF bone
mesh_obj_data = mesh_obj.data
deform_bones  = [b for b in rig.data.bones if b.use_deform]
arm_mat       = rig.matrix_world

def _bone_center_world(bone):
    return arm_mat @ ((bone.head_local + bone.tail_local) * 0.5)

unweighted = []
for v in mesh_obj_data.vertices:
    if not v.groups:
        unweighted.append(v)

if unweighted:
    v_world = [mesh_obj.matrix_world @ v.co for v in unweighted]
    for v, vw in zip(unweighted, v_world):
        nearest_bone = min(deform_bones, key=lambda b: (vw - _bone_center_world(b)).length)
        grp = mesh_obj.vertex_groups.get(nearest_bone.name)
        if grp is None:
            grp = mesh_obj.vertex_groups.new(name=nearest_bone.name)
        grp.add([v.index], 1.0, 'REPLACE')

print(f"  Envelope weight applied; {len(unweighted)} verts needed nearest-bone fallback")

# Post-process 2: limit to 4 influences per vertex (Unity GPU skinning max)
# Uses Blender's built-in Limit Total operator.
set_active(mesh_obj)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
print("  Vertex weights limited to 4 influences and renormalised")

# ── 8. Create animations ──────────────────────────────────────────────────────
# Switch to pose mode on rig
set_active(rig)
bpy.ops.object.mode_set(mode='POSE')

def clear_pose():
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.transforms_clear()

def get_bone(name_options):
    """Return first matching pose bone by name (Rigify uses prefixed names)."""
    for name in name_options:
        b = rig.pose.bones.get(name)
        if b:
            return b
    return None

# Rigify bone name conventions
SPINE   = get_bone(['spine', 'spine_fk', 'DEF-spine', 'ORG-spine'])
CHEST   = get_bone(['chest', 'spine_fk.003', 'DEF-chest'])
NECK    = get_bone(['neck', 'DEF-neck'])
HEAD    = get_bone(['head', 'DEF-head', 'ORG-head'])
THIGH_L = get_bone(['thigh_fk.L', 'DEF-thigh.L', 'ORG-thigh.L'])
THIGH_R = get_bone(['thigh_fk.R', 'DEF-thigh.R', 'ORG-thigh.R'])
SHIN_L  = get_bone(['shin_fk.L', 'DEF-shin.L'])
SHIN_R  = get_bone(['shin_fk.R', 'DEF-shin.R'])
UPPER_ARM_L = get_bone(['upper_arm_fk.L', 'DEF-upper_arm.L'])
UPPER_ARM_R = get_bone(['upper_arm_fk.R', 'DEF-upper_arm.R'])
FOREARM_L   = get_bone(['forearm_fk.L', 'DEF-forearm.L'])
FOREARM_R   = get_bone(['forearm_fk.R', 'DEF-forearm.R'])

def insert_bone_rot(bone, frame, x=0.0, y=0.0, z=0.0):
    """Insert rotation keyframe on a bone (radians, euler XYZ)."""
    if bone is None:
        return
    bone.rotation_mode = 'XYZ'
    bone.rotation_euler = (x, y, z)
    bone.keyframe_insert(data_path='rotation_euler', frame=frame)

def insert_bone_loc(bone, frame, x=0.0, y=0.0, z=0.0):
    if bone is None:
        return
    bone.location = (x, y, z)
    bone.keyframe_insert(data_path='location', frame=frame)


# ── Action 1: Idle (frames 0-60, loopable) ──────────────────────────────────
idle_action = bpy.data.actions.new("Idle")
rig.animation_data_create()
rig.animation_data.action = idle_action

clear_pose()
# Subtle spine sway: +2° at f0/f60, -2° at f30
R = math.radians
for fr in [0, 60]:
    insert_bone_rot(SPINE, fr, x=R(2), y=0, z=0)
    insert_bone_rot(CHEST, fr, x=R(1), y=0, z=0)
insert_bone_rot(SPINE, 30, x=R(-2), y=0, z=0)
insert_bone_rot(CHEST, 30, x=R(-1), y=0, z=0)
# Subtle head nod
if HEAD:
    insert_bone_rot(HEAD, 0,  x=R(0))
    insert_bone_rot(HEAD, 30, x=R(-3))
    insert_bone_rot(HEAD, 60, x=R(0))

# ── Action 2: Walk (frames 0-24, loopable) ──────────────────────────────────
walk_action = bpy.data.actions.new("Walk")
rig.animation_data.action = walk_action

clear_pose()
# Left leg forward / right leg back at f0, swap at f12
swing = R(25)
bend  = R(15)
for f, sign in [(0, 1), (12, -1), (24, 1)]:
    insert_bone_rot(THIGH_L, f, x=swing*sign)
    insert_bone_rot(THIGH_R, f, x=-swing*sign)
    insert_bone_rot(SHIN_L,  f, x=bend*(1-sign)/2)
    insert_bone_rot(SHIN_R,  f, x=bend*(1+sign)/2)
    # Arms swing opposite to legs
    insert_bone_rot(UPPER_ARM_L, f, x=-swing*0.5*sign)
    insert_bone_rot(UPPER_ARM_R, f, x=swing*0.5*sign)

# Spine slight rotation with each step
insert_bone_rot(SPINE, 0,  z=R(5))
insert_bone_rot(SPINE, 12, z=R(-5))
insert_bone_rot(SPINE, 24, z=R(5))

# ── Action 3: Run (frames 0-16, loopable) ───────────────────────────────────
run_action = bpy.data.actions.new("Run")
rig.animation_data.action = run_action

clear_pose()
run_swing = R(40)
run_bend  = R(30)
for f, sign in [(0, 1), (8, -1), (16, 1)]:
    insert_bone_rot(THIGH_L, f, x=run_swing*sign)
    insert_bone_rot(THIGH_R, f, x=-run_swing*sign)
    insert_bone_rot(SHIN_L,  f, x=run_bend*(1-sign)/2)
    insert_bone_rot(SHIN_R,  f, x=run_bend*(1+sign)/2)
    insert_bone_rot(UPPER_ARM_L, f, x=-run_swing*0.7*sign)
    insert_bone_rot(UPPER_ARM_R, f, x=run_swing*0.7*sign)
# Forward lean
insert_bone_rot(CHEST, 0, x=R(10))
insert_bone_rot(CHEST, 8, x=R(10))
insert_bone_rot(CHEST, 16, x=R(10))

bpy.ops.object.mode_set(mode='OBJECT')
print("  3 actions created: Idle(60f), Walk(24f), Run(16f)")

# ── 9. NLA strip setup ────────────────────────────────────────────────────────
ad = rig.animation_data
ad.action = None  # detach current action before NLA

for action, start, name in [
    (idle_action, 0,  "Idle"),
    (walk_action, 61, "Walk"),
    (run_action,  86, "Run"),
]:
    track = ad.nla_tracks.new()
    track.name = name
    strip = track.strips.new(name, start, action)
    strip.action_frame_start = action.frame_range[0]
    strip.action_frame_end   = action.frame_range[1]

print("  NLA tracks configured")

# ── 10. Export FBX ────────────────────────────────────────────────────────────
bpy.ops.object.mode_set(mode='OBJECT')

# Detach custom shapes from ALL pose bones so Rigify widget meshes are unreferenced.
for _pb in rig.pose.bones:
    _pb.custom_shape = None

# Nuclear cleanup: remove every mesh object that is NOT Succubus_Mesh.
# This catches WGT-* widgets, Rigify helper meshes, and any other stray objects
# regardless of their naming convention.
_extra_removed = 0
for _extra in list(bpy.data.objects):
    if _extra.type == 'MESH' and _extra.name != mesh_obj.name:
        print(f"  Removing stray mesh: {_extra.name}  verts={len(_extra.data.vertices)}")
        _extra.parent = None  # unparent to prevent FBX hierarchy pull-in
        bpy.data.objects.remove(_extra, do_unlink=True)
        _extra_removed += 1
print(f"  Removed {_extra_removed} stray mesh objects (WGT widgets, helpers, etc.)")

# Sanity check: log what will actually be exported
print("  Objects selected for export:")
for _obj in bpy.data.objects:
    if _obj.select_get():
        print(f"    {_obj.type}: {_obj.name}")

deselect_all()
rig.select_set(True)
# Re-fetch mesh_obj by name — the Python reference can go stale after mass object removal
_mesh_for_export = bpy.data.objects.get(mesh_obj.name)
if _mesh_for_export:
    _mesh_for_export.select_set(True)
    print(f"  Mesh selected for export: {_mesh_for_export.name}  verts={len(_mesh_for_export.data.vertices):,}")
else:
    print(f"  WARNING: mesh {mesh_obj.name} not found — FBX will rely on armature-dependency export")
bpy.context.view_layer.objects.active = rig

bpy.ops.export_scene.fbx(
    filepath=FBX_OUT,
    use_selection=True,
    # Coordinate system — Unity is Y-up, Blender is Z-up
    axis_forward='-Z',
    axis_up='Y',
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_NONE',
    # Armature
    use_armature_deform_only=True,
    add_leaf_bones=False,
    # Animations
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=1.0,
    # Geometry
    mesh_smooth_type='OFF',
    use_mesh_modifiers=True,
    # Embed the baked texture so Unity's "Extract Textures" works without manual GLB extraction
    embed_textures=True,
    path_mode='COPY',
)
print(f"SUCCESS: FBX exported to {FBX_OUT}")
