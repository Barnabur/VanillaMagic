namespace VanilaMagic.StatusEffects;

public class Heal_SE : SE_Stats
{
    // czas trwania per poziom kostura [q1, q2, q3, q4]
    private static readonly float[] DurationPerLevel = { 12f, 15f, 20f, 25f };

    private float _baseHealPerTick = 1f;

    public override void SetLevel(int itemLevel, float skillLevel)
    {
        _baseHealPerTick = 1f + itemLevel / 2f;
        m_healthPerTick = _baseHealPerTick + skillLevel / 20f;
        m_ttl = DurationPerLevel[UnityEngine.Mathf.Clamp(itemLevel, 1, DurationPerLevel.Length) - 1];
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
