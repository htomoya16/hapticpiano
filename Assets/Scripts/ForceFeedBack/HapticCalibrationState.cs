using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 触覚（サーボ）キャリブレーション結果をセッション内で保持するだけの状態コンポーネント。
/// 指ごとの「完全に緩んだ状態（テンションがかからない状態）」のサーボ値（0-1000）を保存する。
/// </summary>
[DisallowMultipleComponent]
public class HapticCalibrationState : MonoBehaviour
{
    private const int FingerCount = 5;

    [Header("Released Servo Values (Session)")]
    [Tooltip("指ごとの『完全に緩んだ状態（テンションがかからない状態）』のサーボ値（0-1000）。順番は Thumb/Index/Middle/Ring/Pinky")]
    [FormerlySerializedAs("closedServoValues")]
    [SerializeField] private int[] releasedServoValues = new int[FingerCount];

    [Tooltip("指ごとに値が保存されているかどうか")]
    [FormerlySerializedAs("hasClosedServoValue")]
    [SerializeField] private bool[] hasReleasedServoValue = new bool[FingerCount];

    public bool IsFullyCalibrated
    {
        get
        {
            EnsureArrays();
            for (int i = 0; i < FingerCount; i++)
            {
                if (!hasReleasedServoValue[i]) return false;
            }
            return true;
        }
    }

    public void ResetAll()
    {
        EnsureArrays();
        for (int i = 0; i < FingerCount; i++)
        {
            releasedServoValues[i] = 0;
            hasReleasedServoValue[i] = false;
        }
    }

    public void SetReleasedServoValue(int fingerIndex, int value)
    {
        EnsureArrays();
        if (fingerIndex < 0 || fingerIndex >= FingerCount) return;

        int v = value < 0 ? 0 : (value > 1000 ? 1000 : value);
        releasedServoValues[fingerIndex] = v;
        hasReleasedServoValue[fingerIndex] = true;
    }

    public bool TryGetReleasedServoValue(int fingerIndex, out int value)
    {
        EnsureArrays();
        value = 0;
        if (fingerIndex < 0 || fingerIndex >= FingerCount) return false;
        if (!hasReleasedServoValue[fingerIndex]) return false;
        value = releasedServoValues[fingerIndex];
        return true;
    }

    public int[] GetReleasedValuesCopyOrNull()
    {
        EnsureArrays();
        if (!IsFullyCalibrated) return null;

        var copy = new int[FingerCount];
        for (int i = 0; i < FingerCount; i++)
        {
            copy[i] = releasedServoValues[i];
        }
        return copy;
    }

    private void OnValidate()
    {
        EnsureArrays();
        for (int i = 0; i < FingerCount; i++)
        {
            if (releasedServoValues[i] < 0) releasedServoValues[i] = 0;
            if (releasedServoValues[i] > 1000) releasedServoValues[i] = 1000;
        }
    }

    private void EnsureArrays()
    {
        if (releasedServoValues == null || releasedServoValues.Length != FingerCount)
        {
            releasedServoValues = new int[FingerCount];
        }

        if (hasReleasedServoValue == null || hasReleasedServoValue.Length != FingerCount)
        {
            hasReleasedServoValue = new bool[FingerCount];
        }
    }
}
