using UnityEngine;

/// <summary>
/// HandCurlTracker のフィルタ設定などを左右手で共有するためのプリセット。
/// </summary>
[CreateAssetMenu(menuName = "HapticPiano/HandCurlTrackerPreset")]
public class HandCurlTrackerPreset : ScriptableObject
{
    [Header("Filtering")]
    [Tooltip("true のとき curlRaw にローパスフィルタをかけて curl01 を更新する。false ならフィルタなし。")]
    public bool useFiltering = true;

    [Range(0f, 1f)]
    [Tooltip("curlRaw から curl01 への追従係数（大きいほど追従が速くノイズ低減が弱い）。")]
    public float filterAlpha = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("curlRaw と curl01 の差がこの値を超えたとき、一時的に強く追従させるスナップ閾値。")]
    public float snapThreshold = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("curlRaw と curl01 の差がこの値未満のときはノイズとして無視する。")]
    public float noiseThreshold = 0.01f;
}

