using UnityEngine;
using UnityEngine.Serialization;

public enum Handedness
{
    Left = 0,
    Right = 1,
}

public enum FingerId
{
    Thumb = 0,
    Index = 1,
    Middle = 2,
    Ring = 3,
    Pinky = 4,
}

/// <summary>
/// 指コライダに「どの手・どの指か」を付与するためのメタ情報。
/// </summary>
public sealed class FingerColliderId : MonoBehaviour
{
    [FormerlySerializedAs("handSide")]
    [SerializeField] private Handedness handedness;
    [SerializeField] private FingerId fingerId;
    [SerializeField] private int segmentIndex;

    public Handedness Handedness => handedness;
    public FingerId FingerId => fingerId;
    public int SegmentIndex => segmentIndex;

    public void Set(Handedness side, FingerId finger, int segment)
    {
        handedness = side;
        fingerId = finger;
        segmentIndex = segment;
    }
}
