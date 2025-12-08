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

        [Header("PC1 Phase Range")]
        [Tooltip("指を閉じていくときに PC1 のどの位相区間を使うか（0〜1）。")]
        [Range(0f, 1f)]
        public float closingPhaseMin = 0.0f;

        [Range(0f, 1f)]
        public float closingPhaseMax = 0.5f;

        [Tooltip("指を開いていくときに PC1 のどの位相区間を使うか（0〜1）。")]
        [Range(0f, 1f)]
        public float openingPhaseMin = 0.5f;

        [Range(0f, 1f)]
        public float openingPhaseMax = 1.0f;

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

    [Header("Direction Detection")]
    [Tooltip("閉じ/開き方向を切り替えるために必要な累積 curl 変化量（0〜1）。|score| がこの値を超えると方向を切り替える。")]
    [Range(0f, 1f)]
    public float directionAccumulationThreshold = 0.05f;

    [Tooltip("過去の方向スコアをどれくらい残すか。0: 直近のみ, 1: ほぼ全履歴。通常は 0.6〜0.9 程度。")]
    [Range(0f, 1f)]
    public float directionAccumulationDecay = 0.8f;

    // 指ごとの方向状態を保持する（ScriptableObject だが、ランタイム中の一時状態として扱う）
    private readonly bool[] lastIsClosing = new bool[5];
    private readonly bool[] hasDirectionState = new bool[5];
    private readonly float[] directionScore = new float[5];

    public override void Evaluate(
        HandFinger finger,
        float curl,
        float previousCurl,
        out float mcpDeg,
        out float pipDeg,
        out float dipDeg)
    {
        FingerConfig cfg = GetConfigForFinger(finger);

        // curl の増減方向（閉じ/開き）としきい値を考慮して phase を決定する。
        float phase = CurlAndDirectionToPhase(finger, cfg, curl, previousCurl);

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

    /// <summary>
    /// curl とその前フレームの値から、打鍵位相 phase（0〜1）を計算する。
    /// - まず curlContact〜curlBottom の区間を 0〜1 の進行度 u に正規化し、
    ///   閉じ動作では「底に向かって 0→1」、開き動作では「底から戻って 0→1」とみなす。
    /// - その進行度 u を FingerConfig の closing/openingPhaseMin/Max にマッピングして
    ///   実際に PC1 曲線上で使う phase を決める。
    /// - 方向（閉じ/開き）の切り替えは、累積 curl 変化量（directionScore）と
    ///   directionAccumulationThreshold / directionAccumulationDecay に基づくヒステリシスで行う。
    /// </summary>
    private float CurlAndDirectionToPhase(HandFinger finger, FingerConfig cfg, float curl, float previousCurl)
    {
        float c    = Mathf.Clamp01(curl);
        float prev = Mathf.Clamp01(previousCurl);

        float cContact = Mathf.Clamp01(cfg.curlContact);
        float cBottom  = Mathf.Clamp01(cfg.curlBottom);

        int fingerIndex = GetFingerIndex(finger);
        if (fingerIndex < 0 || fingerIndex >= 5)
        {
            fingerIndex = 0;
        }

        // 初回は単純に「今の curl の方向」で初期化する
        if (!hasDirectionState[fingerIndex])
        {
            bool initialClosing = c >= prev;
            lastIsClosing[fingerIndex] = initialClosing;
            hasDirectionState[fingerIndex] = true;
            directionScore[fingerIndex] = 0f;
        }
        else
        {
            // 直近数フレームの curl 変化を減衰付きで累積し、
            // 一定以上たまったときだけ方向を切り替える。
            float delta = c - prev;

            float decay = Mathf.Clamp01(directionAccumulationDecay);
            float score = directionScore[fingerIndex] * decay + delta;
            directionScore[fingerIndex] = score;

            if (Mathf.Abs(score) >= directionAccumulationThreshold)
            {
                bool newClosing = score >= 0f;
                if (newClosing != lastIsClosing[fingerIndex])
                {
                    lastIsClosing[fingerIndex] = newClosing;
                }

                // 一度方向を切り替えたらスコアはリセットしておく。
                directionScore[fingerIndex] = 0f;
            }
        }

        bool isClosing = lastIsClosing[fingerIndex];

        // phase 範囲を 0〜1 にクランプし、Min > Max の場合は入れ替える
        float closingMin  = Mathf.Clamp01(cfg.closingPhaseMin);
        float closingMax  = Mathf.Clamp01(cfg.closingPhaseMax);
        if (closingMax < closingMin)
        {
            float tmp = closingMin;
            closingMin = closingMax;
            closingMax = tmp;
        }

        float openingMin  = Mathf.Clamp01(cfg.openingPhaseMin);
        float openingMax  = Mathf.Clamp01(cfg.openingPhaseMax);
        if (openingMax < openingMin)
        {
            float tmp = openingMin;
            openingMin = openingMax;
            openingMax = tmp;
        }

        // 設定がおかしい場合のフォールバック：0〜1 を closing/opening のレンジに線形割り当て
        if (cBottom <= cContact)
        {
            float uFallback = c; // 0〜1
            return isClosing
                ? Mathf.Lerp(closingMin, closingMax, uFallback)
                : Mathf.Lerp(openingMin, openingMax, uFallback);
        }

        float u; // 0〜1 の進行度

        if (isClosing)
        {
            // 指を閉じていく動き: curlContact〜curlBottom を 進行度 u=0〜1 に対応
            if (c <= cContact) u = 0f;
            else if (c >= cBottom) u = 1f;
            else u = Mathf.InverseLerp(cContact, cBottom, c); // 0〜1

            return Mathf.Lerp(closingMin, closingMax, u);
        }
        else
        {
            // 指を開いていく動き: curlBottom〜curlContact を 進行度 u=0〜1 に対応
            if (c >= cBottom) u = 0f;
            else if (c <= cContact) u = 1f;
            else u = Mathf.InverseLerp(cBottom, cContact, c); // 0〜1

            return Mathf.Lerp(openingMin, openingMax, u);
        }
    }

    private int GetFingerIndex(HandFinger finger)
    {
        switch (finger)
        {
            case HandFinger.Thumb:  return 0;
            case HandFinger.Index:  return 1;
            case HandFinger.Middle: return 2;
            case HandFinger.Ring:   return 3;
            case HandFinger.Pinky:  return 4;
            default:                return 0;
        }
    }
}
