"""Zrzuca hierarchie i pola waniliowych prefabow Valheima wprost z bundli gry (bez Unity).

Uzycie:  python tools/dump_prefab.py StaffFireball DvergerStaffIce_projectile > out.txt
Wymaga:  pip install UnityPy   (bundle maja wbudowane typetree - generator nie jest potrzebny)
Bundle c4210710 = itemy/bronie/postacie/fx; ktory bundle trzyma dany prefab mowi
valheim_Data/StreamingAssets/SoftRef/manifest_extended ("path in bundle: .../<nazwa>.prefab").
"""
import sys, json, UnityPy
from UnityPy.enums import ClassIDType
from UnityPy.classes import PPtr

ROOT = r"C:/Program Files (x86)/Steam/steamapps/common/Valheim"
BDIR = ROOT + "/valheim_Data/StreamingAssets/SoftRef/Bundles/"
MANAGED = ROOT + "/valheim_Data/Managed"
targets = sys.argv[1:]

env = UnityPy.Environment()
for b in ["c4210710", "6a33a62", "2c2cce25", "86c3d76e", "8d5dbad8", "9fe0899c"]:
    env.load_file(BDIR + b)

gos = {}
for obj in env.objects:
    if obj.type == ClassIDType.GameObject:
        try:
            d = obj.read()
        except Exception:
            continue
        gos.setdefault(d.m_Name, []).append(obj)


def name_of(p):
    try:
        if p is None or p.m_PathID == 0:
            return None
        o = p.read()
        return getattr(o, "m_Name", None) or type(o).__name__
    except Exception:
        return "?"


def resolve(tt, reader):
    def walk(v):
        if isinstance(v, dict):
            if "m_FileID" in v and "m_PathID" in v:
                if v["m_PathID"] == 0:
                    return None
                try:
                    p = PPtr(m_FileID=v["m_FileID"], m_PathID=v["m_PathID"], assetsfile=reader.assets_file)
                    o = p.read()
                    return getattr(o, "m_Name", None) or type(o).__name__
                except Exception:
                    return "?(%s,%s)" % (v["m_FileID"], v["m_PathID"])
            return {k: walk(x) for k, x in v.items()}
        if isinstance(v, list):
            out = [walk(x) for x in v[:24]]
            if len(v) > 24:
                out.append("...(%d)" % len(v))
            return out
        if isinstance(v, float):
            return round(v, 4)
        return v
    return walk(tt)


SKIP = {"m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_EditorHideFlags", "m_EditorClassIdentifier"}


def mm(c):
    if not isinstance(c, dict):
        return c
    return "%s/%s st=%s" % (c.get("scalar"), c.get("minScalar"), c.get("minMaxState"))


def col(c):
    if not isinstance(c, dict):
        return c
    mc = c.get("maxColor", {})
    mn = c.get("minColor", {})
    f = lambda d: "(%.3g,%.3g,%.3g,%.3g)" % (d.get("r", 0), d.get("g", 0), d.get("b", 0), d.get("a", 0))
    return "max=%s min=%s st=%s" % (f(mc), f(mn), c.get("minMaxState"))


def ps_summary(tt):
    im = tt.get("InitialModule", {})
    em = tt.get("EmissionModule", {})
    sh = tt.get("ShapeModule", {})
    r = sh.get("radius")
    r = r.get("value") if isinstance(r, dict) else r
    bursts = [(b.get("time"), (b.get("countCurve") or {}).get("scalar")) for b in em.get("m_Bursts", [])]
    return ("dur=%s loop=%s life=%s speed=%s size=%s color=%s rate=%s bursts=%s shape=%s r=%s world=%s "
            "colorOverLife=%s sizeOverLife=%s gravity=%s max=%s" % (
                tt.get("lengthInSec"), tt.get("looping"), mm(im.get("startLifetime")), mm(im.get("startSpeed")),
                mm(im.get("startSize")), col(im.get("startColor")), mm(em.get("rateOverTime")), bursts,
                sh.get("type"), r, tt.get("moveWithTransform"),
                tt.get("ColorModule", {}).get("enabled"), tt.get("SizeModule", {}).get("enabled"),
                mm(im.get("gravityModifier")), tt.get("maxNumParticles")))


def dump_go(go_reader, indent="", maxdepth=7):
    go = go_reader.read()
    comps = []
    for c in go.m_Components:
        ptr = c.component if hasattr(c, "component") else c
        try:
            reader = ptr.deref()
            comp = reader.read()
        except Exception as e:
            comps.append(("?" + str(e)[:40], None, None))
            continue
        comps.append((type(comp).__name__, comp, reader))
    print("%s%s active=%s layer=%s comps=%s" % (indent, go.m_Name, go.m_IsActive, go.m_Layer, [n for n, _, _ in comps]))
    tr = None
    for name, comp, reader in comps:
        if comp is None:
            continue
        if name in ("Transform", "RectTransform"):
            tr = comp
            p, r, s = comp.m_LocalPosition, comp.m_LocalRotation, comp.m_LocalScale
            print("%s   pos=(%.4g,%.4g,%.4g) rotQ=(%.4g,%.4g,%.4g,%.4g) scale=(%.4g,%.4g,%.4g)" % (
                indent, p.x, p.y, p.z, r.x, r.y, r.z, r.w, s.x, s.y, s.z))
        elif name == "MonoBehaviour":
            script = name_of(comp.m_Script)
            try:
                tt = reader.read_typetree()
                tt = {k: v for k, v in tt.items() if k not in SKIP}
                tt = resolve(tt, reader)
                print("%s   [MB %s] %s" % (indent, script, json.dumps(tt, default=str)[:14000]))
            except Exception as e:
                print("%s   [MB %s] (typetree fail: %s)" % (indent, script, str(e)[:120]))
        elif name == "MeshFilter":
            print("%s   [MeshFilter] mesh=%s" % (indent, name_of(comp.m_Mesh)))
        elif name in ("MeshRenderer", "ParticleSystemRenderer", "SkinnedMeshRenderer", "TrailRenderer", "LineRenderer"):
            mats = [name_of(m) for m in comp.m_Materials]
            extra = ""
            if name == "ParticleSystemRenderer":
                extra = " renderMode=%s mesh=%s" % (getattr(comp, "m_RenderMode", None), name_of(getattr(comp, "m_Mesh", None)))
            if name == "SkinnedMeshRenderer":
                extra = " mesh=%s" % name_of(comp.m_Mesh)
            print("%s   [%s] mats=%s%s" % (indent, name, mats, extra))
        elif name == "Light":
            print("%s   [Light] type=%s color=(%.3g,%.3g,%.3g) int=%s range=%s" % (
                indent, comp.m_Type, comp.m_Color.r, comp.m_Color.g, comp.m_Color.b, comp.m_Intensity, comp.m_Range))
        elif name == "ParticleSystem":
            try:
                tt = reader.read_typetree()
                print("%s   [PS] %s" % (indent, ps_summary(tt)))
            except Exception as e:
                print("%s   [PS] (fail %s)" % (indent, str(e)[:100]))
        elif name in ("BoxCollider", "SphereCollider", "CapsuleCollider"):
            c = comp.m_Center
            sz = getattr(comp, "m_Size", None)
            rad = getattr(comp, "m_Radius", None)
            print("%s   [%s] center=(%.4g,%.4g,%.4g) size=%s radius=%s trigger=%s" % (
                indent, name, c.x, c.y, c.z, (round(sz.x, 4), round(sz.y, 4), round(sz.z, 4)) if sz else None, rad, comp.m_IsTrigger))
        elif name == "AudioSource":
            print("%s   [AudioSource] clip=%s vol=%s" % (indent, name_of(comp.m_audioClip), comp.m_Volume))
        else:
            print("%s   [%s]" % (indent, name))
    if tr is None or maxdepth <= 0:
        return
    for ch in tr.m_Children:
        try:
            cht = ch.read()
            dump_go(cht.m_GameObject.deref(), indent + "  ", maxdepth - 1)
        except Exception as e:
            print("%s  (child fail %s)" % (indent, str(e)[:80]))


for t in targets:
    print("=" * 20, t, "=" * 20)
    found = False
    for o in gos.get(t, []):
        try:
            go = o.read()
            trs = [x for x in ((c.component if hasattr(c, "component") else c).read() for c in go.m_Components)
                   if x.__class__.__name__ in ("Transform", "RectTransform")]
            if trs and trs[0].m_Father.m_PathID != 0:
                continue
        except Exception:
            pass
        found = True
        dump_go(o)
    if not found:
        print("  (not found as root)")
