using System.Collections;
using Jotunn.Entities;
using UnityEngine;

namespace VanilaMagic.DungeonRooms
{
    // Komenda testowa: miga najbliższym obiektem z RandomSpawn (albo obiektem o podanej
    // nazwie), żeby dało się go wzrokowo zidentyfikować przed wpisaniem jako 'parent'.
    // Użycie: blinkspawn [nazwa, np. ice (3)]
    internal class BlinkSpawnCommand : ConsoleCommand
    {
        public override string Name => "blinkspawn";

        public override string Help => "Miga najbliższym obiektem z RandomSpawn (opcjonalnie: o podanej nazwie)";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (!player)
            {
                Console.instance.Print("Musisz być w świecie gry");
                return;
            }

            // nazwy mogą zawierać spacje, np. "ice (3)"
            string filter = args.Length > 0 ? string.Join(" ", args) : null;

            Vector3 playerPos = player.transform.position;
            RandomSpawn best = null;
            float bestDistance = 20f;
            foreach (var spawn in Object.FindObjectsByType<RandomSpawn>(FindObjectsSortMode.None))
            {
                if (filter != null && spawn.name != filter)
                {
                    continue;
                }

                float distance = Vector3.Distance(spawn.transform.position, playerPos);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = spawn;
                }
            }

            if (best == null)
            {
                Console.instance.Print(filter != null
                    ? $"Nie znaleziono obiektu '{filter}' z RandomSpawn w zasięgu 20m"
                    : "Brak obiektów z RandomSpawn w zasięgu 20m");
                return;
            }

            Console.instance.Print($"Miga: {best.name} ({bestDistance:F1}m)");
            player.StartCoroutine(Blink(best.gameObject));
        }

        private static IEnumerator Blink(GameObject go)
        {
            for (int i = 0; i < 4; i++)
            {
                go.SetActive(false);
                yield return new WaitForSeconds(0.4f);
                go.SetActive(true);
                yield return new WaitForSeconds(0.4f);
            }
        }
    }
}
