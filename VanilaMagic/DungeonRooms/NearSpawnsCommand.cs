using Jotunn.Entities;
using UnityEngine;

namespace VanilaMagic.DungeonRooms
{
    // Komenda testowa: listuje obiekty z RandomSpawn w pobliżu gracza wraz z informacją,
    // czy można pod nie podpiąć grzybka (parametr 'parent' w RoomMushroomSpots).
    // Użycie: nearspawns [zasięg w metrach, domyślnie 5]
    internal class NearSpawnsCommand : ConsoleCommand
    {
        public override string Name => "nearspawns";

        public override string Help => "Listuje obiekty z RandomSpawn w pobliżu gracza (do podpinania grzybków)";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (!player)
            {
                Console.instance.Print("Musisz być w świecie gry");
                return;
            }

            float radius = 5f;
            if (args.Length > 0 && float.TryParse(args[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            {
                radius = parsed;
            }

            Vector3 playerPos = player.transform.position;
            int found = 0;
            foreach (var spawn in Object.FindObjectsByType<RandomSpawn>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(spawn.transform.position, playerPos);
                if (distance > radius)
                {
                    continue;
                }

                found++;
                bool hasNetView = spawn.GetComponentInParent<ZNetView>() != null;
                string verdict = hasNetView ? "ma ZNetView - NIE podpinać" : "można podpinać";
                Console.instance.Print($"{spawn.name}  ({distance:F1}m, {verdict})");
            }

            if (found == 0)
            {
                Console.instance.Print($"Brak obiektów z RandomSpawn w zasięgu {radius}m");
            }
        }
    }
}
