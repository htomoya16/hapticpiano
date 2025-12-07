using UnityEngine;

[CreateAssetMenu(fileName = "PC1CurveSet", menuName = "HapticPiano/PC1 Curve Set")]
public class PC1CurveSet : ScriptableObject
{
    [Header("MCP")]
    public AnimationCurve mcpIndex;
    public AnimationCurve mcpMiddle;
    public AnimationCurve mcpRing;
    public AnimationCurve mcpPinky;

    [Header("PIP")]
    public AnimationCurve pipIndex;
    public AnimationCurve pipMiddle;
    public AnimationCurve pipRing;
    public AnimationCurve pipPinky;
}

