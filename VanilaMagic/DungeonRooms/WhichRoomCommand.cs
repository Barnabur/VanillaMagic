using Jotunn.Entities;
using UnityEngine;

namespace VanilaMagic.DungeonRooms
{
    // Komenda testowa: pokazuje, w którym pokoju dungeonu stoi gracz,
    // oraz jego lokalną pozycję w pokoju (gotowe koordynaty do RoomMushroomSpots).
    internal class WhichRoomCommand : ConsoleCommand
    {
        public override string Name => "whichroom";

        public override string Help => "Pokazuje nazwę pokoju dungeonu, w którym stoisz, i lokalną pozycję w nim";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (!player)
            {
                Console.instance.Print("Musisz być w świecie gry");
                return;
            }

            Vector3 playerPos = player.transform.position;
            Room best = null;
            Vector3 bestLocal = Vector3.zero;
            float bestVolume = float.MaxValue;

            // Pokoje mogą na siebie nachodzić przy złączkach - bierzemy najmniejszy,
            // który zawiera gracza
            foreach (var room in Object.FindObjectsByType<Room>(FindObjectsSortMode.None))
            {
                Vector3 local = room.transform.InverseTransformPoint(playerPos);
                Vector3 half = (Vector3)room.m_size * 0.5f + Vector3.one;
                if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z)
                {
                    continue;
                }

                float volume = (float)room.m_size.x * room.m_size.y * room.m_size.z;
                if (volume < bestVolume)
                {
                    best = room;
                    bestLocal = local;
                    bestVolume = volume;
                }
            }

            if (best == null)
            {
                Console.instance.Print("Nie stoisz w żadnym pokoju dungeonu");
                return;
            }

            Console.instance.Print($"Pokój: {best.name} (rozmiar {best.m_size.x}x{best.m_size.y}x{best.m_size.z})");
            Console.instance.Print($"Lokalna pozycja (stopy gracza): {bestLocal.x:F2} {bestLocal.y:F2} {bestLocal.z:F2}");
        }
    }
}
