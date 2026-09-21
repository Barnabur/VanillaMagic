using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.DungeonRooms
{
    // Komenda testowa: spawnuje wariant pokoju z grzybkami w miejscu gracza,
    // żeby dało się obejrzeć rozmieszczenie grzybków bez szukania jaskini.
    // Użycie (po devcommands): mushroomroom [nazwa_pokoju]
    internal class SpawnMushroomRoomCommand : ConsoleCommand
    {
        public override string Name => "mushroomroom";

        public override string Help => "Spawnuje testowo wariant pokoju jaskini z grzybkami w miejscu gracza";

        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (!player)
            {
                Console.instance.Print("Musisz być w świecie gry");
                return;
            }

            string baseName = args.Length > 0 ? args[0] : MushroomCaveRooms.RoomNames.First();
            var room = DungeonManager.Instance.GetRoom(baseName + "_mushroom");
            if (room == null)
            {
                Console.instance.Print($"Nie znaleziono pokoju {baseName}_mushroom");
                return;
            }

            Vector3 origin = player.transform.position;
            Object.Instantiate(room.Prefab, origin, Quaternion.identity);
            Console.instance.Print($"Zespawnowano {baseName}_mushroom, origin pokoju: {origin.x:F1} {origin.y:F1} {origin.z:F1}");
            Console.instance.Print("Lokalna pozycja grzybka = pozycja z 'pos' minus origin pokoju");
        }

        public override List<string> CommandOptionList()
        {
            return MushroomCaveRooms.RoomNames.ToList();
        }
    }
}
