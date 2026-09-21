using System.Collections.Generic;
using System.Linq;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.DungeonRooms
{
    // Komenda testowa: spawnuje wariant pokoju jak mushroomroom, ale wszystkie obiekty
    // z RandomSpawn (półki, kryształy itd.) są ukryte - symulacja najgorszego losowania.
    // Grzybki bez rodzica są zawsze widoczne; grzybki podpięte pod rodzica znikają razem
    // z nim (tak ma być). Jeśli jakiś grzybek lewituje - brakuje mu parametru parent.
    // Użycie (po devcommands): mushroomonly [nazwa_pokoju]
    internal class MushroomOnlyCommand : ConsoleCommand
    {
        public override string Name => "mushroomonly";

        public override string Help => "Spawnuje pokój z ukrytymi obiektami RandomSpawn (test lewitujących grzybków)";

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

            // Chowamy dekoracje NA PREFABIE przed spawnem (ZNetView ukrytych dzieci wtedy
            // w ogóle nie powstają), a zaraz po spawnie przywracamy stan prefabu
            var hidden = new List<GameObject>();
            foreach (var spawn in room.Prefab.GetComponentsInChildren<RandomSpawn>(true))
            {
                if (spawn.gameObject.name == MushroomCaveRooms.MushroomName || !spawn.gameObject.activeSelf)
                {
                    continue;
                }

                spawn.gameObject.SetActive(false);
                hidden.Add(spawn.gameObject);
            }

            Vector3 origin = player.transform.position;
            Object.Instantiate(room.Prefab, origin, Quaternion.identity);

            foreach (var go in hidden)
            {
                go.SetActive(true);
            }

            Console.instance.Print($"Zespawnowano {baseName}_mushroom bez {hidden.Count} obiektów RandomSpawn, origin: {origin.x:F1} {origin.y:F1} {origin.z:F1}");
            Console.instance.Print("Lewitujący grzybek = brakuje mu rodzica; grzybek podpięty znika razem z półką");
        }

        public override List<string> CommandOptionList()
        {
            return MushroomCaveRooms.RoomNames.ToList();
        }
    }
}
