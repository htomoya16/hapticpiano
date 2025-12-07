using UnityEngine;

/// <summary>
/// PC1 カーブを用いた指関節シナジーモデル。
/// curl → phase → PC1 → angle の変換をカプセル化する。
/// </summary>
[CreateAssetMenu(menuName = "HapticPiano/Kinematics/PC1")]
public class Pc1HandKinematicsProfile : HandKinematicsProfile
{
    [System.Serializable]
    public class FingerConfig
    {
        [Header("Curl → Phase Mapping")]
        [Range(0f, 1f)]
        public float curlContact = 0.25f;

        [Range(0f, 1f)]
        public float curlBottom = 0.85f;

        [Header("Angle Parameters (deg)")]
        public float baseMcpDeg = 0f;
        public float basePipDeg = 0f;
        public float baseDipDeg = 0f;

        [Tooltip("PC1(MCP) の振幅を何度に相当させるか")]
        public float mcpScaleDeg = 0.03f;

        [Tooltip("PC1(PIP) の振幅を何度に相当させるか")]
        public float pipScaleDeg = 0.03f;

        [Tooltip("DIP 角を PIP 角からどの比率で決めるか（例: 0.5）")]
        [Range(0f, 1f)]
        public float dipFromPipRatio = 0.5f;
    }

    [Header("PC1 Curves")]
    public PC1CurveSet pc1Curves;

    [Header("Finger Configs")]
    public FingerConfig thumb = new FingerConfig();
    public FingerConfig index = new FingerConfig();
    public FingerConfig middle = new FingerConfig();
    public FingerConfig ring = new FingerConfig();
    public FingerConfig pinky = new FingerConfig();

    public override void Evaluate(
        HandFinger finger,
        float curl,
        out float mcpDeg,
        out float pipDeg,
        out float dipDeg)
    {
        FingerConfig cfg = GetConfigForFinger(finger);

        float phase = CurlToPhase(cfg, curl);

        float pc1Mcp = 0f;
        float pc1Pip = 0f;

        if (pc1Curves != null)
        {
            AnimationCurve mcpCurve = null;
            AnimationCurve pipCurve = null;

            switch (finger)
            {
                case HandFinger.Index:
                    mcpCurve = pc1Curves.mcpIndex;
                    pipCurve = pc1Curves.pipIndex;
                    break;
                case HandFinger.Middle:
                    mcpCurve = pc1Curves.mcpMiddle;
                    pipCurve = pc1Curves.pipMiddle;
                    break;
                case HandFinger.Ring:
                    mcpCurve = pc1Curves.mcpRing;
                    pipCurve = pc1Curves.pipRing;
                    break;
                case HandFinger.Pinky:
                    mcpCurve = pc1Curves.mcpPinky;
                    pipCurve = pc1Curves.pipPinky;
                    break;
                // 親指は現状 PC1 カーブ未定義のため、0 扱いとする。
                case HandFinger.Thumb:
                default:
                    break;
            }

            if (mcpCurve != null)
            {
                pc1Mcp = mcpCurve.Evaluate(phase);
            }

            if (pipCurve != null)
            {
                pc1Pip = pipCurve.Evaluate(phase);
            }
        }

        mcpDeg = cfg.baseMcpDeg + pc1Mcp * cfg.mcpScaleDeg;
        pipDeg = cfg.basePipDeg + pc1Pip * cfg.pipScaleDeg;
        dipDeg = cfg.baseDipDeg + cfg.dipFromPipRatio * (pipDeg - cfg.basePipDeg);
    }

    private FingerConfig GetConfigForFinger(HandFinger finger)
    {
        switch (finger)
        {
            case HandFinger.Thumb:  return thumb;
            case HandFinger.Index:  return index;
            case HandFinger.Middle: return middle;
            case HandFinger.Ring:   return ring;
            case HandFinger.Pinky:  return pinky;
            default:                return index;
        }
    }

    private float CurlToPhase(FingerConfig cfg, float curl)
    {
        float c = Mathf.Clamp01(curl);

        float cContact = Mathf.Clamp01(cfg.curlContact);
        float cBottom = Mathf.Clamp01(cfg.curlBottom);

        if (cBottom <= cContact)
        {
            // 異常値の場合は単純線形
            return c;
        }

        if (c <= cContact)
            return 0f;
        if (c >= cBottom)
            return 1f;

        return Mathf.InverseLerp(cContact, cBottom, c);
    }
}

