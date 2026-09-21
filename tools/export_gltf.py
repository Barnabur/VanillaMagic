"""Eksport postaci gracza, zbroi Fenrisa i peleryny trolla z bundli Valheima do glTF 2.0 (Blender: File > Import > glTF).

Uzycie:  python tools/export_gltf.py [katalog_wyjsciowy]     (domyslnie ./export)
Wymaga:  pip install UnityPy pillow

Co robi:
- hierarchia prefabu Player (Armature, wszystkie kosci) -> wezly glTF, jeden szkielet (skin) na wszystko,
- SkinnedMeshRenderery: Player/body, FenringPants (ArmorFenringChest), FenringBoots (ArmorFenringLegs),
  FenringHood (HelmetFenring), cape2 (CapeTrollHide) -> siatki z JOINTS_0/WEIGHTS_0 i inverseBindMatrices
  z m_BindPose; kosci renderera mapowane po NAZWIE na szkielet gracza,
- tekstury materialow (+ nakladki na cialo FenringArmorChest/Legs, wraith, troll) -> PNG w textures/,
- konwersja Unity (lewoskretny, Y w gore) -> glTF (prawoskretny): x -> -x, kwaternion (x,-y,-z,w),
  odwrocona kolejnosc wierzcholkow trojkatow, v -> 1 - v.
Siatki sa w jednostkach prefabu (renderer ma skale 100, bind pose to uwzglednia) - w Blenderze
po imporcie ewentualnie Apply Scale.
"""
import json
import os
import struct
import sys

import UnityPy
from UnityPy.enums import ClassIDType
from UnityPy.helpers import MeshHelper

ROOT = r"C:/Program Files (x86)/Steam/steamapps/common/Valheim"
BDIR = ROOT + "/valheim_Data/StreamingAssets/SoftRef/Bundles/"
BUNDLES = ["c4210710", "6a33a62", "2c2cce25", "86c3d76e", "8d5dbad8", "9fe0899c"]

# (prefab, nazwa obiektu z SkinnedMeshRenderer) do wyeksportowania
RENDERERS = [
    ("Player", "body"),
    ("ArmorFenringChest", "FenringPants"),
    ("ArmorFenringLegs", "FenringBoots"),
    ("HelmetFenring", "FenringHood"),
    ("CapeTrollHide", "cape2"),
]
EXTRA_TEXTURES = ["FenringArmor_d", "FenringArmor_n", "FenringArmorChest_d", "FenringArmorChest_n",
                  "FenringArmorLegs_d", "FenringArmorLegs_n", "troll_diffuse", "troll_n",
                  "wraith_d", "wraith_n", "wraith_m"]

out_dir = sys.argv[1] if len(sys.argv) > 1 else "export"
tex_dir = os.path.join(out_dir, "textures")
os.makedirs(tex_dir, exist_ok=True)

env = UnityPy.Environment()
for b in BUNDLES:
    env.load_file(BDIR + b)

# ------------------------------------------------------------------ pomocnicze

def comps(go):
    for c in go.m_Components:
        ptr = c.component if hasattr(c, "component") else c
        try:
            yield ptr.read()
        except Exception:
            continue


def transform_of(go):
    for c in comps(go):
        if c.__class__.__name__ in ("Transform", "RectTransform"):
            return c
    return None


def find_root(name):
    for obj in env.objects:
        if obj.type != ClassIDType.GameObject:
            continue
        try:
            go = obj.read()
        except Exception:
            continue
        if go.m_Name != name:
            continue
        tr = transform_of(go)
        if tr is not None and tr.m_Father.m_PathID == 0:
            return go, tr
    raise SystemExit(f"brak prefabu {name}")


def find_in_hierarchy(tr, name):
    go = tr.m_GameObject.read()
    if go.m_Name == name:
        return go
    for ch in tr.m_Children:
        r = find_in_hierarchy(ch.read(), name)
        if r is not None:
            return r
    return None


def q_conv(q):
    return [q.x, -q.y, -q.z, q.w]


def v_conv(v):
    return [-v.x, v.y, v.z]


def mat_conv(m):
    # Unity Matrix4x4f e_rc (wiersz r, kolumna c) -> M' = F M F, F = diag(-1,1,1,1); glTF przechowuje kolumnami
    M = [[m.e00, m.e01, m.e02, m.e03], [m.e10, m.e11, m.e12, m.e13], [m.e20, m.e21, m.e22, m.e23], [m.e30, m.e31, m.e32, m.e33]]
    F = [-1.0, 1.0, 1.0, 1.0]
    out = []
    for c in range(4):
        for r in range(4):
            out.append(F[r] * M[r][c] * F[c])
    return out


# ------------------------------------------------------------------ szkielet gracza -> wezly

nodes = []
node_by_name = {}


def add_node(tr, parent_index):
    go = tr.m_GameObject.read()
    idx = len(nodes)
    p, r, s = tr.m_LocalPosition, tr.m_LocalRotation, tr.m_LocalScale
    node = {"name": go.m_Name, "translation": v_conv(p), "rotation": q_conv(r), "scale": [s.x, s.y, s.z]}
    nodes.append(node)
    node_by_name.setdefault(go.m_Name, idx)
    if parent_index is not None:
        nodes[parent_index].setdefault("children", []).append(idx)
    for ch in tr.m_Children:
        add_node(ch.read(), idx)
    return idx


player_go, player_tr = find_root("Player")
player_root = add_node(player_tr, None)
print(f"szkielet: {len(nodes)} wezlow z prefabu Player")

# ------------------------------------------------------------------ bufor binarny

blob = bytearray()
buffer_views = []
accessors = []


def add_view(data, target=None):
    while len(blob) % 4:
        blob.append(0)
    view = {"buffer": 0, "byteOffset": len(blob), "byteLength": len(data)}
    if target:
        view["target"] = target
    blob.extend(data)
    buffer_views.append(view)
    return len(buffer_views) - 1


def add_accessor(values, ctype, comp_count, component_type, target=None, minmax=False):
    fmt = {"f": "f", "H": "H", "I": "I"}[ctype]
    flat = [x for v in values for x in (v if comp_count > 1 else (v,))]
    data = struct.pack("<%d%s" % (len(flat), fmt), *flat)
    view = add_view(data, target)
    acc = {"bufferView": view, "componentType": component_type, "count": len(values),
           "type": {1: "SCALAR", 2: "VEC2", 3: "VEC3", 4: "VEC4", 16: "MAT4"}[comp_count]}
    if minmax:
        acc["min"] = [min(v[i] for v in values) for i in range(comp_count)]
        acc["max"] = [max(v[i] for v in values) for i in range(comp_count)]
    accessors.append(acc)
    return len(accessors) - 1


FLOAT, USHORT, UINT = 5126, 5123, 5125
ARRAY_BUFFER, ELEMENT_ARRAY_BUFFER = 34962, 34963

# ------------------------------------------------------------------ tekstury / materialy

images = []
textures = []
materials = []
exported_textures = {}


def export_texture(tex_obj):
    name = tex_obj.m_Name
    if name in exported_textures:
        return exported_textures[name]
    path = os.path.join(tex_dir, name + ".png")
    try:
        tex_obj.image.save(path)
    except Exception as e:
        print(f"  tekstura {name}: blad {e}")
        exported_textures[name] = None
        return None
    images.append({"uri": "textures/" + name + ".png", "name": name})
    textures.append({"source": len(images) - 1, "name": name})
    exported_textures[name] = len(textures) - 1
    print(f"  tekstura {name} {tex_obj.m_Width}x{tex_obj.m_Height}")
    return exported_textures[name]


def tex_envs(mat):
    te = mat.m_SavedProperties.m_TexEnvs
    return te.items() if isinstance(te, dict) else te


def add_material(mat):
    entry = {"name": mat.m_Name, "pbrMetallicRoughness": {"metallicFactor": 0.0, "roughnessFactor": 0.9}, "doubleSided": True}
    for key, env_ in tex_envs(mat):
        ptr = env_.m_Texture
        if ptr is None or ptr.m_PathID == 0:
            continue
        try:
            tex = ptr.read()
        except Exception:
            continue
        ti = export_texture(tex)
        if ti is None:
            continue
        if key == "_MainTex":
            entry["pbrMetallicRoughness"]["baseColorTexture"] = {"index": ti}
            entry["alphaMode"] = "MASK"
            entry["alphaCutoff"] = 0.5
    materials.append(entry)
    return len(materials) - 1


# ------------------------------------------------------------------ siatki

meshes = []
skins = []
scene_nodes = [player_root]


def export_renderer(prefab_name, renderer_name):
    root_go, root_tr = find_root(prefab_name)
    go = find_in_hierarchy(root_tr, renderer_name)
    if go is None:
        print(f"{prefab_name}: brak obiektu {renderer_name}")
        return
    smr = next((c for c in comps(go) if c.__class__.__name__ == "SkinnedMeshRenderer"), None)
    if smr is None:
        print(f"{prefab_name}/{renderer_name}: brak SkinnedMeshRenderer")
        return
    mesh = smr.m_Mesh.read()
    h = MeshHelper.MeshHandler(mesh)
    h.process()
    verts = h.m_Vertices
    normals = h.m_Normals
    uvs = h.m_UV0
    bone_idx = h.m_BoneIndices
    bone_w = h.m_BoneWeights
    n = len(verts)
    print(f"{prefab_name}/{renderer_name}: mesh {mesh.m_Name}, {n} wierzch., {len(smr.m_Bones)} kosci")

    # kosci renderera -> wezly szkieletu gracza (po nazwie)
    joints = []
    missing = []
    for b in smr.m_Bones:
        bname = b.read().m_GameObject.read().m_Name if b.m_PathID else "?"
        if bname in node_by_name:
            joints.append(node_by_name[bname])
        else:
            missing.append(bname)
            joints.append(node_by_name.get("Hips", player_root))
    if missing:
        print(f"  UWAGA: kosci bez odpowiednika w Player: {missing}")

    positions = [v_conv(type("V", (), {"x": v[0], "y": v[1], "z": v[2]})) for v in verts]
    norms = [v_conv(type("V", (), {"x": v[0], "y": v[1], "z": v[2]})) for v in normals] if normals else None
    texcoords = [[u, 1.0 - v] for (u, v) in uvs] if uvs else None
    # niektore siatki (np. FenringBoots) maja 1-2 wagi na wierzcholek - glTF wymaga stale 4
    jnt = [([int(i) for i in bi] + [0, 0, 0, 0])[:4] for bi in bone_idx] if bone_idx else None
    wts = []
    if bone_w:
        for w in bone_w:
            w = (list(w) + [0.0, 0.0, 0.0, 0.0])[:4]
            s = sum(w) or 1.0
            wts.append([x / s for x in w])

    # indeksy: odwrocona kolejnosc (lustro X)
    tris = []
    idx = h.m_IndexBuffer
    for sm in mesh.m_SubMeshes:
        start = sm.firstByte // (2 if h.m_Use16BitIndices else 4)
        part = idx[start:start + sm.indexCount]
        for t in range(0, len(part) - 2, 3):
            tris.extend((part[t], part[t + 2], part[t + 1]))

    attrs = {"POSITION": add_accessor(positions, "f", 3, FLOAT, ARRAY_BUFFER, minmax=True)}
    if norms:
        attrs["NORMAL"] = add_accessor(norms, "f", 3, FLOAT, ARRAY_BUFFER)
    if texcoords:
        attrs["TEXCOORD_0"] = add_accessor(texcoords, "f", 2, FLOAT, ARRAY_BUFFER)
    skin_index = None
    if jnt and wts:
        attrs["JOINTS_0"] = add_accessor(jnt, "H", 4, USHORT, ARRAY_BUFFER)
        attrs["WEIGHTS_0"] = add_accessor(wts, "f", 4, FLOAT, ARRAY_BUFFER)
        ibm = [mat_conv(m) for m in mesh.m_BindPose]
        if len(ibm) != len(joints):
            print(f"  UWAGA: bindPose {len(ibm)} vs kosci {len(joints)}")
            k = min(len(ibm), len(joints))
            ibm, joints = ibm[:k], joints[:k]
        skins.append({"name": f"skin_{renderer_name}", "joints": joints,
                      "inverseBindMatrices": add_accessor(ibm, "f", 16, FLOAT),
                      "skeleton": node_by_name.get("Armature", player_root)})
        skin_index = len(skins) - 1
    indices_acc = add_accessor(tris, "I", 1, UINT, ELEMENT_ARRAY_BUFFER)

    mat_index = None
    if smr.m_Materials:
        try:
            mat_index = add_material(smr.m_Materials[0].read())
        except Exception as e:
            print(f"  material: {e}")
    prim = {"attributes": attrs, "indices": indices_acc, "mode": 4}
    if mat_index is not None:
        prim["material"] = mat_index
    meshes.append({"name": f"{prefab_name}_{renderer_name}", "primitives": [prim]})
    node = {"name": f"{prefab_name}_{renderer_name}", "mesh": len(meshes) - 1}
    if skin_index is not None:
        node["skin"] = skin_index
    nodes.append(node)
    scene_nodes.append(len(nodes) - 1)


for prefab_name, renderer_name in RENDERERS:
    export_renderer(prefab_name, renderer_name)

# dodatkowe tekstury (nakladki na cialo itp.)
for obj in env.objects:
    if obj.type != ClassIDType.Texture2D:
        continue
    d = obj.read()
    if d.m_Name in EXTRA_TEXTURES and d.m_Name not in exported_textures:
        export_texture(d)

# ------------------------------------------------------------------ zapis

bin_name = "valheim_wraith_set.bin"
with open(os.path.join(out_dir, bin_name), "wb") as f:
    f.write(blob)
gltf = {
    "asset": {"version": "2.0", "generator": "VanilaMagic tools/export_gltf.py (UnityPy)"},
    "scene": 0,
    "scenes": [{"nodes": scene_nodes, "name": "Valheim"}],
    "nodes": nodes,
    "meshes": meshes,
    "skins": skins,
    "materials": materials,
    "textures": textures,
    "images": images,
    "accessors": accessors,
    "bufferViews": buffer_views,
    "buffers": [{"uri": bin_name, "byteLength": len(blob)}],
}
with open(os.path.join(out_dir, "valheim_wraith_set.gltf"), "w", encoding="utf-8") as f:
    json.dump(gltf, f, indent=1)
print(f"zapisano {out_dir}/valheim_wraith_set.gltf ({len(blob)} B bufora, {len(meshes)} siatek, {len(textures)} tekstur)")
