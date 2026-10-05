namespace VanilaMagic.StatusEffects;

public class Heal_SE : SE_Stats
{
    private float _baseHealPerTick = 1f;

    public override void SetLevel(int itemLevel, float skillLevel)
    {
        _baseHealPerTick = 1f + itemLevel / 2f;
        m_healthPerTick = _baseHealPerTick + skillLevel / 20f;
        // czas trwania rosnie z poziomem kostura: 10/15/20/25 s
        m_ttl = 5f + 5f * UnityEngine.Mathf.Clamp(itemLevel, 1, 4);
    }

    public override string GetTooltipString()
    {
        string baseDesc = Localization.instance.Localize("$se_heal_desc");
        return string.Format(baseDesc,
            _baseHealPerTick.ToString("0.#"),
            m_healthPerTick.ToString("0.#"),
            m_ttl.ToString("0"));
    }
}
