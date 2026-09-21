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
    ///   wraithtint capetex [sciezka.png|reset] - albedo peleryny z pliku (uklad UV; hot-reload), reset = kafelek Fenrisa
    ///   wraithtint tatter [szerokosc_m]  - szerokosc pasa strzepow przy krawedzi (0 = brak)
    ///   wraithtint capecrop x y w h      - wycinek tekstury Fenrisa (px, 128x128, y od dolu) na kafelek peleryny
    /// </summary>
    public class WraithTintCommand : ConsoleCommand
    {
        public override string Name => "wraithtint";
        public override string Help => "Strojenie Szat Widma: wraithtint r g b [gain] | wraithtint cape r g b | wraithtint capetile sx sy | wraithtint capetex plik.png | wraithtint tatter 0.05 | wraithtint capecrop 32 24 56 32";

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
            if (args[0] == "capetex")
            {
                var path = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : "reset";
                Console.instance.Print("wraithtint: " + WraithArmor.LoadCapeAlbedo(path));
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

        private static bool TryParse(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
