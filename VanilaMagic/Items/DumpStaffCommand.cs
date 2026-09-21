using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Jotunn.Entities;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Tymczasowa komenda diagnostyczna: zrzuca do logu pelna hierarchie itemow
    /// trzymanych w rekach (aktywnosc, warstwy, particle systemy, materialy).
    /// </summary>
    public class DumpStaffCommand : ConsoleCommand
    {
        public override string Name => "dumpstaff";
        public override string Help => "Zrzuca hierarchie trzymanych itemow do logu (diagnostyka); 'dumpstaff armor' - zalozona zbroja";

        public override void Run(string[] args)
        {
            var player = Player.m_localPlayer;
            if (!player)
            {
                Console.instance.Print("Brak lokalnego gracza");
                return;
            }
            var vis = player.GetComponent<VisEquipment>();
            if (!vis)
            {
                Console.instance.Print("Brak VisEquipment");
                return;
            }
            if (args.Length > 0 && args[0] == "armor")
            {
                DumpList(vis, "m_chestItemInstances", "TORS");
                DumpList(vis, "m_legItemInstances", "NOGI");
                DumpList(vis, "m_shoulderItemInstances", "PELERYNA");
                Dump(AccessTools.Field(typeof(VisEquipment), "m_helmetItemInstance")?.GetValue(vis) as GameObject, "HELM");
                Console.instance.Print("dumpstaff armor: hierarchia zapisana do logu");
                return;
            }
            Dump(vis.m_leftItemInstance, "LEWA REKA");
            Dump(vis.m_rightItemInstance, "PRAWA REKA");
            Console.instance.Print("dumpstaff: hierarchia zapisana do logu");
        }

        private static void DumpList(VisEquipment vis, string field, string label)
        {
            var list = AccessTools.Field(typeof(VisEquipment), field)?.GetValue(vis) as List<GameObject>;
            if (list == null || list.Count == 0)
            {
                Jotunn.Logger.LogInfo($"dumpstaff {label}: (pusto)");
                return;
            }
            for (var i = 0; i < list.Count; i++) Dump(list[i], $"{label} [{i}] parent={(list[i] && list[i].transform.parent ? list[i].transform.parent.name : "-")}");
        }

        private static void Dump(GameObject go, string label)
        {
            if (!go)
            {
                Jotunn.Logger.LogInfo($"dumpstaff {label}: (pusto)");
                return;
            }
            var sb = new StringBuilder();
            sb.Append($"dumpstaff {label}:\n");
            Walk(go.transform, "", sb);
            Jotunn.Logger.LogInfo(sb.ToString());
        }

        private static void Walk(Transform t, string indent, StringBuilder sb)
        {
            sb.Append($"{indent}{t.name} act={t.gameObject.activeSelf}/{t.gameObject.activeInHierarchy} " +
                      $"layer={t.gameObject.layer} lossyScale={t.lossyScale}\n");
            foreach (var ps in t.GetComponents<ParticleSystem>())
            {
                sb.Append($"{indent}  [PS] playing={ps.isPlaying} count={ps.particleCount} " +
                          $"emissionOn={ps.emission.enabled} rate={ps.emission.rateOverTime.constant} " +
                          $"startSize={ps.main.startSize.constantMin}-{ps.main.startSize.constantMax}\n");
            }
            foreach (var r in t.GetComponents<Renderer>())
            {
                var mat = r.sharedMaterial;
                sb.Append($"{indent}  [{r.GetType().Name}] enabled={r.enabled} " +
                          $"mat={(mat ? mat.name + " / " + mat.shader.name : "null")}\n");
            }
            foreach (var l in t.GetComponents<Light>())
            {
                sb.Append($"{indent}  [Light] color={l.color} int={l.intensity} range={l.range}\n");
            }
            foreach (Transform c in t)
            {
                Walk(c, indent + "  ", sb);
            }
        }
    }
}
