using System.Globalization;
using Jotunn.Entities;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Komenda dev do strojenia koloru Szat Widma na zywo (tekstury sa przemalowywane w miejscu,
    /// wiec zmiana widoczna od razu na zalozonej zbroi i nakladce ciala):
    ///   wraithtint                      - wypisuje aktualne parametry
    ///   wraithtint [r] [g] [b] [gain]   - kolor futra (luminancja * gain * tint)
    ///   wraithtint cape [r] [g] [b]     - mnoznik koloru peleryny (0-1)
    ///   wraithtint capetile [sx] [sy]   - powtorzenia kafelka futra na pelerynie
    ///   wraithtint capetex [sciezka.png [raw]|reset] - albedo peleryny z pliku (uklad UV cape2; hot-reload), raw = kolory 1:1, reset = WraithCape.png z DLL
    ///   wraithtint tatter [szerokosc_m]  - szerokosc pasa strzepow przy krawedzi (0 = brak)
    ///   wraithtint capecrop x y w h      - wycinek tekstury Fenrisa (px, 128x128, y od dolu) na kafelek peleryny
    ///   wraithtint capemat [wlasciwosc wartosc...] - dump / ustawienie wlasciwosci materialu peleryny na zywo
    ///   wraithtint capechains on|off     - lancuchy 3D (ClothChain) na pelerynie; domyslnie off, lancuchy sa w teksturze
    ///   wraithtint capecloth [radius r] [radiusbottom r] [angle a] [gravity g] [maxdist m] [cutoff 0-1] [damping d] - symulacja tkaniny peleryny na zywo (radius = odstep od ciala)
    ///   wraithtint capecut [wiersz]      - gorny pas peleryny niewidzialny pod Kapturem Widma (domyslnie 25)
    ///   wraithtint capestrip [gora] [dol] - widoczne wiersze pasa peleryny przypietego do kaptura (domyslnie 34 46)
    ///   wraithtint capedump              - zrzut siatek gracza po skinningu i symulacji tkaniny do BepInEx/capedump_HHmmss.obj
    /// </summary>
    public class WraithTintCommand : ConsoleCommand
    {
        public override string Name => "wraithtint";
        public override string Help => "Strojenie Szat Widma: wraithtint r g b [gain] | wraithtint cape r g b | wraithtint capetex plik.png [raw] | wraithtint capemat [_BumpScale 0 | _BumpMap none|troll|own | _KEYWORD on|off] | wraithtint capetile sx sy | wraithtint tatter 0.05 | wraithtint capecrop 32 24 56 32";

        public override void Run(string[] args)
        {
            if (args.Length == 0)
            {
                Console.instance.Print($"wraithtint: tint={WraithArmor.Tint} gain={WraithArmor.Gain} cape={WraithArmor.CapeTint} capetile={WraithArmor.CapeTiling} tatter={WraithArmor.CapeTatterWidth}");
                return;
            }
            if (args[0] == "capecrop")
            {
                if (args.Length < 5 || !int.TryParse(args[1], out var cx) || !int.TryParse(args[2], out var cy)
                    || !int.TryParse(args[3], out var cw) || !int.TryParse(args[4], out var ch))
                {
                    Console.instance.Print("uzycie: wraithtint capecrop x y w h  (piksele tekstury 128x128, y od dolu)");
                    return;
                }
                WraithArmor.CapeCropX = Mathf.Clamp(cx, 0, 127);
                WraithArmor.CapeCropY = Mathf.Clamp(cy, 0, 127);
                WraithArmor.CapeCropW = Mathf.Clamp(cw, 1, 128 - WraithArmor.CapeCropX);
                WraithArmor.CapeCropH = Mathf.Clamp(ch, 1, 128 - WraithArmor.CapeCropY);
                WraithArmor.ApplyCapeTiling();
                Console.instance.Print($"wraithtint: wycinek peleryny x={WraithArmor.CapeCropX} y={WraithArmor.CapeCropY} w={WraithArmor.CapeCropW} h={WraithArmor.CapeCropH}");
                return;
            }
            if (args[0] == "tatter")
            {
                if (args.Length < 2 || !TryParse(args[1], out var width))
                {
                    Console.instance.Print("uzycie: wraithtint tatter szerokosc_m");
                    return;
                }
                WraithArmor.CapeTatterWidth = width;
                WraithArmor.ApplyCapeTiling();
                Console.instance.Print($"wraithtint: strzepy {width} m");
                return;
            }
            if (args[0] == "capechains")
            {
                if (args.Length < 2 || (args[1] != "on" && args[1] != "off")) { Console.instance.Print("uzycie: wraithtint capechains on|off"); return; }
                Console.instance.Print("wraithtint: " + WraithArmor.SetCapeChains3D(args[1] == "on"));
                return;
            }
            if (args[0] == "capecut")
            {
                // wraithtint capecut [wiersz]  - do ktorego wiersza tekstury (z 128, od kolnierza) peleryna jest niewidzialna pod Kapturem Widma
                if (args.Length > 1 && int.TryParse(args[1], out var row)) WraithArmor.HoodedCutRow = Mathf.Clamp(row, 0, 100);
                WraithArmor.SyncHoodedCape();
                Console.instance.Print($"wraithtint: pod kapturem niewidzialne wiersze 0..{WraithArmor.HoodedCutRow} (kazdy wiersz ~1.6 cm)");
                return;
            }
            if (args[0] == "capestrip")
            {
                // wraithtint capestrip [gora] [dol] - widoczne wiersze pasa peleryny przypietego do kaptura
                if (args.Length > 1 && int.TryParse(args[1], out var top)) WraithArmor.StripTopRow = Mathf.Clamp(top, 0, 57);
                if (args.Length > 2 && int.TryParse(args[2], out var bottom)) WraithArmor.StripBottomRow = Mathf.Clamp(bottom, 0, 57);
                WraithArmor.SyncHoodedCape();
                Console.instance.Print($"wraithtint: pas w kapturze wiersze {WraithArmor.StripTopRow}..{WraithArmor.StripBottomRow} (geometria do 57), peleryna ukryta do {WraithArmor.HoodedCutRow}");
                return;
            }
            if (args[0] == "capecloth")
            {
                // wraithtint capecloth [radius 0.03] [radiusbottom 0.08] [angle 0.15] [gravity 7] [maxdist 0.08] [cutoff 0.5] [damping 0.2]  (-1 = wanilia / maxdist off)
                for (var i = 1; i + 1 < args.Length; i += 2)
                {
                    if (!TryParse(args[i + 1], out var val)) continue;
                    if (args[i] == "radius") WraithCapeCloth.Radius = val;
                    else if (args[i] == "radiusbottom") WraithCapeCloth.RadiusBottom = val;
                    else if (args[i] == "angle") WraithCapeCloth.AngleStiffness = val;
                    else if (args[i] == "gravity") WraithCapeCloth.Gravity = val;
                    else if (args[i] == "maxdist") WraithCapeCloth.MaxDistance = val;
                    else if (args[i] == "cutoff") WraithCapeCloth.LimitedDepth = val;
                    else if (args[i] == "damping") WraithCapeCloth.Damping = val;
                }
                Console.instance.Print("wraithtint: tkanina peleryny " + WraithCapeCloth.ApplyLive());
                return;
            }
            if (args[0] == "capedump")
            {
                Console.instance.Print("wraithtint: " + DumpWornMeshes());
                return;
            }
            if (args[0] == "capemat")
            {
                // wraithtint capemat                       - dump materialu peleryny do konsoli i logu
                // wraithtint capemat _BumpScale 0          - float; _Color 1 1 1 - kolor; _BumpMap none|troll|own; _ALPHATEST_ON on|off
                if (args.Length == 1) { Console.instance.Print(WraithArmor.LogCapeMaterial()); return; }
                var values = new string[args.Length - 2];
                System.Array.Copy(args, 2, values, 0, values.Length);
                Console.instance.Print("wraithtint: " + WraithArmor.SetCapeMaterial(args[1], values));
                WraithArmor.SyncHoodedCape();
                return;
            }
            if (args[0] == "capetex")
            {
                // wraithtint capetex <plik.png> [raw]  - raw = kolory z pliku 1:1 (bez Colorize/tintu)
                var raw = args.Length > 2 && args[args.Length - 1] == "raw";
                var count = args.Length - 1 - (raw ? 1 : 0);
                var path = count > 0 ? string.Join(" ", args, 1, count) : "reset";
                Console.instance.Print("wraithtint: " + WraithArmor.LoadCapeAlbedo(path, raw));
                return;
            }
            if (args[0] == "capetile")
            {
                if (args.Length < 3 || !TryParse(args[1], out var sx) || !TryParse(args[2], out var sy))
                {
                    Console.instance.Print("uzycie: wraithtint capetile sx sy");
                    return;
                }
                WraithArmor.CapeTiling = new Vector2(sx, sy);
                WraithArmor.ApplyCapeTiling();
                Console.instance.Print($"wraithtint: kafelkowanie peleryny {WraithArmor.CapeTiling}");
                return;
            }
            if (args[0] == "cape")
            {
                if (args.Length < 4 || !TryParse(args[1], out var cr) || !TryParse(args[2], out var cg) || !TryParse(args[3], out var cb))
                {
                    Console.instance.Print("uzycie: wraithtint cape r g b");
                    return;
                }
                WraithArmor.CapeTint = new Color(cr, cg, cb);
                WraithArmor.Retint();
                Console.instance.Print($"wraithtint: peleryna {WraithArmor.CapeTint}");
                return;
            }
            if (args.Length < 3 || !TryParse(args[0], out var r) || !TryParse(args[1], out var g) || !TryParse(args[2], out var b))
            {
                Console.instance.Print("uzycie: wraithtint r g b [gain]");
                return;
            }
            WraithArmor.Tint = new Color(r, g, b);
            if (args.Length > 3 && TryParse(args[3], out var gain)) WraithArmor.Gain = gain;
            WraithArmor.Retint();
            Console.instance.Print($"wraithtint: tint={WraithArmor.Tint} gain={WraithArmor.Gain}");
        }

        /// <summary>
        /// Zrzut aktualnie wyrenderowanych siatek gracza (po skinningu i symulacji tkaniny - BakeMesh) do OBJ
        /// w przestrzeni lokalnej gracza (metry): kazdy SkinnedMeshRenderer jako osobny obiekt "o nazwa", peleryna z UV.
        /// Do porownania w ripie (CapeLab), gdzie peleryna faktycznie lezy wzgledem kolnierza kaptura.
        /// </summary>
        private static string DumpWornMeshes()
        {
            var player = Player.m_localPlayer;
            if (!player) return "brak gracza";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# VanilaMagic capedump, lokalne wspolrzedne gracza (m)");
            var offset = 1;
            var names = new System.Collections.Generic.List<string>();
            foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || !smr.sharedMesh) continue;
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                var verts = baked.vertices;
                var uv = baked.uv;
                sb.AppendLine("o " + smr.name + "|" + smr.sharedMesh.name);
                foreach (var v in verts)
                {
                    var p = player.transform.InverseTransformPoint(smr.transform.TransformPoint(v));
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "v {0:0.#####} {1:0.#####} {2:0.#####}", p.x, p.y, p.z));
                }
                // vt zawsze tyle co v (indeksy OBJ sa globalne, wiec musza isc rowno)
                for (var i = 0; i < verts.Length; i++)
                {
                    var t = i < uv.Length ? uv[i] : Vector2.zero;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "vt {0:0.#####} {1:0.#####}", t.x, t.y));
                }
                var tris = baked.triangles;
                for (var i = 0; i + 2 < tris.Length; i += 3)
                {
                    int a = tris[i] + offset, b = tris[i + 1] + offset, c = tris[i + 2] + offset;
                    sb.AppendLine($"f {a}/{a} {b}/{b} {c}/{c}");
                }
                offset += verts.Length;
                names.Add($"{smr.name}({verts.Length})");
                Object.Destroy(baked);
            }
            var path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "capedump_" + System.DateTime.Now.ToString("HHmmss") + ".obj");
            System.IO.File.WriteAllText(path, sb.ToString());
            return $"zapisano {path}: {string.Join(", ", names)}";
        }

        private static bool TryParse(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
