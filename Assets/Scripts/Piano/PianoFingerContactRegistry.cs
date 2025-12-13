using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PianoFingerContactRegistry : MonoBehaviour
{
    [Header("Finger Filter")]
    [Tooltip("FingerColliderId.segmentIndex がこの値のときだけ接触として扱う（TIP側=0）。")]
    [SerializeField] private int requiredSegmentIndex = 0;

    [Header("Bottom Detection (EulerAngles.x)")]
    [Tooltip("底面到達: eulerAngles.x がこの値以下になったらロックする（0→360補正後の値）。")]
    [SerializeField] private float bottomEnterAngleX = 352.5f;

    [Tooltip("底面解除: eulerAngles.x がこの値以上に戻ったらロック解除する（0→360補正後の値）。")]
    [SerializeField] private float bottomExitAngleX = 354.0f;

    [Header("Debug (read-only)")]
    [SerializeField] private bool logChanges = false;

    [Header("Touch Stability")]
    [Tooltip("接触が一瞬途切れても、ここで指定した秒数は接触中として扱う（ガタつき対策）。")]
    [SerializeField] private float touchReleaseGraceSeconds = 0.08f;

    [Serializable]
    private struct FingerState
    {
        public bool isTouching;
        public bool isBottomLocked;
        public PianoKey primaryKey;
        public float primaryKeyAngleX;
    }

    [SerializeField] private FingerState[] rightHand = new FingerState[5];
    [SerializeField] private FingerState[] leftHand = new FingerState[5];

    private readonly Dictionary<PianoKey, int>[] _rightCounts = InitCounts();
    private readonly Dictionary<PianoKey, int>[] _leftCounts = InitCounts();
    private readonly float[] _rightLastTouchRealtime = new float[5];
    private readonly float[] _leftLastTouchRealtime = new float[5];

    private static Dictionary<PianoKey, int>[] InitCounts()
    {
        var arr = new Dictionary<PianoKey, int>[5];
        for (int i = 0; i < arr.Length; i++) arr[i] = new Dictionary<PianoKey, int>();
        return arr;
    }

    public int RequiredSegmentIndex => requiredSegmentIndex;

    public bool TryGetFingerState(Handedness handedness, FingerId fingerId, out bool isTouching, out bool isBottomLocked, out PianoKey key)
    {
        var s = GetStateRef(handedness, fingerId);
        isTouching = s.isTouching;
        isBottomLocked = s.isBottomLocked;
        key = s.primaryKey;
        return s.isTouching;
    }

    private ref FingerState GetStateRef(Handedness handedness, FingerId fingerId)
    {
        int idx = (int)fingerId;
        if (handedness == Handedness.Right) return ref rightHand[idx];
        return ref leftHand[idx];
    }

    private Dictionary<PianoKey, int> GetCounts(Handedness handedness, FingerId fingerId)
    {
        int idx = (int)fingerId;
        return handedness == Handedness.Right ? _rightCounts[idx] : _leftCounts[idx];
    }

    public void RegisterCollisionEnter(PianoKey key, FingerColliderId fingerColliderId)
    {
        if (key == null || fingerColliderId == null) return;
        if (fingerColliderId.SegmentIndex != requiredSegmentIndex) return;

        var dict = GetCounts(fingerColliderId.Handedness, fingerColliderId.FingerId);
        dict.TryGetValue(key, out int c);
        dict[key] = c + 1;
        RefreshFinger(fingerColliderId.Handedness, fingerColliderId.FingerId);
    }

    public void RegisterCollisionExit(PianoKey key, FingerColliderId fingerColliderId)
    {
        if (key == null || fingerColliderId == null) return;
        if (fingerColliderId.SegmentIndex != requiredSegmentIndex) return;

        var dict = GetCounts(fingerColliderId.Handedness, fingerColliderId.FingerId);
        if (!dict.TryGetValue(key, out int c)) return;
        c -= 1;
        if (c <= 0) dict.Remove(key);
        else dict[key] = c;

        RefreshFinger(fingerColliderId.Handedness, fingerColliderId.FingerId);
    }

    private void Update()
    {
        for (int i = 0; i < 5; i++)
        {
            RefreshFinger(Handedness.Right, (FingerId)i);
            RefreshFinger(Handedness.Left, (FingerId)i);
        }
    }

    private void RefreshFinger(Handedness handedness, FingerId fingerId)
    {
        ref var state = ref GetStateRef(handedness, fingerId);
        var dict = GetCounts(handedness, fingerId);
        float now = Time.realtimeSinceStartup;

        PianoKey bestKey = null;
        float bestAngle = float.PositiveInfinity;
        foreach (var kv in dict)
        {
            if (kv.Key == null) continue;
            float ax = GetAngleX360(kv.Key.transform);
            if (ax < bestAngle)
            {
                bestAngle = ax;
                bestKey = kv.Key;
            }
        }

        if (bestKey != null)
        {
            SetLastTouchRealtime(handedness, fingerId, now);
        }
        else
        {
            float grace = Mathf.Max(0f, touchReleaseGraceSeconds);
            if (grace > 0f && state.primaryKey != null && (now - GetLastTouchRealtime(handedness, fingerId)) <= grace)
            {
                bestKey = state.primaryKey;
                bestAngle = GetAngleX360(bestKey.transform);
            }
        }

        bool touching = bestKey != null;
        bool prevTouching = state.isTouching;
        var prevKey = state.primaryKey;

        state.isTouching = touching;
        state.primaryKey = bestKey;
        state.primaryKeyAngleX = touching ? bestAngle : 0f;

        if (!touching)
        {
            state.isBottomLocked = false;
        }
        else
        {
            if (!state.isBottomLocked && bestAngle <= bottomEnterAngleX)
                state.isBottomLocked = true;
            else if (state.isBottomLocked && bestAngle >= bottomExitAngleX)
                state.isBottomLocked = false;
        }

        if (logChanges && (prevTouching != state.isTouching || prevKey != state.primaryKey))
        {
            Debug.Log($"[PianoFingerContactRegistry] {handedness}/{fingerId} touching={state.isTouching} key={(state.primaryKey ? state.primaryKey.name : "null")} angleX={state.primaryKeyAngleX:F1} bottomLocked={state.isBottomLocked}", this);
        }
    }

    private float GetLastTouchRealtime(Handedness handedness, FingerId fingerId)
    {
        int idx = (int)fingerId;
        return handedness == Handedness.Right ? _rightLastTouchRealtime[idx] : _leftLastTouchRealtime[idx];
    }

    private void SetLastTouchRealtime(Handedness handedness, FingerId fingerId, float t)
    {
        int idx = (int)fingerId;
        if (handedness == Handedness.Right) _rightLastTouchRealtime[idx] = t;
        else _leftLastTouchRealtime[idx] = t;
    }

    private static float GetAngleX360(Transform t)
    {
        float x = t.eulerAngles.x;
        if (x < 180f) x += 360f;
        return x;
    }
}
