using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Jotunn.Entities;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Strojenie kadru ikon renderowanych z modeli, na zywo w grze.
    /// Bez tego kazda proba kata to przebudowa moda i restart gry.
    ///
    ///   wandicon                              - wypisuje itemy i ich aktualne kadry
    ///   wandicon FrostWand 15 -30 35          - obrot XYZ w stopniach
    ///   wandicon FrostWand 15 -30 35 0.6 1.2  - dodatkowo FOV i odsuniecie kamery
    ///
    /// Mniejszy FOV = bardziej "plaski" obiektyw (mniej perspektywy), wiekszy Distance
    /// = przedmiot mniejszy w kadrze.
    /// </summary>
    public class WandIconCommand : ConsoleCommand
    {
        public override string Name => "wandicon";

        public override string Help => "wandicon [item] [rx ry rz] [fov] [dist] - stroi kadr ikony renderowanej z modelu | wandicon export [rozmiar] - zapisuje ikony jako PNG do BepInEx/VanilaMagicIcons";

        public override List<string> CommandOptionList() => RenderedIcons.Registered.ToList();

        public override void Run(string[] args)
        {
            if (args.Length == 0)
            {
                foreach (var name in RenderedIcons.Registered)
                {
                    if (!RenderedIcons.TryGetShot(name, out var s)) continue;
                    Console.instance.Print($"{name}: rot=({s.Euler.x:0.#},{s.Euler.y:0.#},{s.Euler.z:0.#}) fov={s.FieldOfView} dist={s.Distance}");
                }
                return;
            }

            if (args[0].Equals("export", System.StringComparison.OrdinalIgnoreCase))
            {
                int size = args.Length >= 2 && int.TryParse(args[1], out var s) ? Mathf.Clamp(s, 64, 2048) : 512;
                var dir = Path.Combine(BepInEx.Paths.BepInExRootPath, "VanilaMagicIcons");
                foreach (var name in RenderedIcons.Registered.ToList())
                {
                    var path = RenderedIcons.Export(name, size, dir);
                    Console.instance.Print(path != null ? $"wandicon export: {path}" : $"wandicon export: {name} nie wyszedl");
                }
                return;
            }

            var item =RenderedIcons.Registered.FirstOrDefault(n => n.Equals(args[0], System.StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                Console.instance.Print($"wandicon: nie znam itemu '{args[0]}' - dostepne: {string.Join(", ", RenderedIcons.Registered)}");
                return;
            }

            if (!RenderedIcons.TryGetShot(item, out var shot)) return;

            if (args.Length >= 4 && TryParse(args[1], out var rx) && TryParse(args[2], out var ry) && TryParse(args[3], out var rz))
            {
                shot.Euler = new Vector3(rx, ry, rz);
            }
            if (args.Length >= 5 && TryParse(args[4], out var fov)) shot.FieldOfView = Mathf.Clamp(fov, 0.01f, 60f);
            if (args.Length >= 6 && TryParse(args[5], out var dist)) shot.Distance = Mathf.Clamp(dist, 0.1f, 10f);

            RenderedIcons.Retake(item, shot);
            Console.instance.Print($"wandicon {item}: rot=({shot.Euler.x:0.#},{shot.Euler.y:0.#},{shot.Euler.z:0.#}) fov={shot.FieldOfView} dist={shot.Distance}");
        }

        private static bool TryParse(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
