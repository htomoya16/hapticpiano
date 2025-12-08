using UnityEngine;
using Valve.VR.InteractionSystem;

/// <summary>
/// HandSerialInput が K フラグ（キャリブレーション中）を受信している間、
/// VR 上の手の位置・向きを凍結するコンポーネントである。
/// </summary>
public class HandFreezeOnCalibrate : MonoBehaviour
{
    [Header("References")]
    [Tooltip("K フラグを監視する HandSerialInput")]
    public HandSerialInput serialInput;

    [Tooltip("凍結対象の Transform（通常は Hand が付いているルートオブジェクト）")]
    public Transform handRoot;

    [Header("Debug")]
    [Tooltip("現在フリーズ中かどうか（デバッグ表示用）")]
    public bool isFrozen = false;

    private bool _wasCalibrating = false;
    private bool _hasFrozenPose = false;
    private Vector3 _frozenPosition;
    private Quaternion _frozenRotation;

    private void Reset()
    {
        // 自動アサイン用の簡易ヘルパー
        if (handRoot == null)
        {
            handRoot = transform;
        }
    }

    private void LateUpdate()
    {
        if (serialInput == null || handRoot == null)
        {
            isFrozen = false;
            _hasFrozenPose = false;
            _wasCalibrating = false;
            return;
        }

        bool nowCalibrating = serialInput.isCalibrating;

        // キャリブレーション開始時に現在の姿勢を記録
        if (nowCalibrating && !_wasCalibrating)
        {
            _frozenPosition = handRoot.position;
            _frozenRotation = handRoot.rotation;
            _hasFrozenPose = true;
        }

        if (nowCalibrating && _hasFrozenPose)
        {
            // K が来ている間は位置・向きを固定
            handRoot.position = _frozenPosition;
            handRoot.rotation = _frozenRotation;
            isFrozen = true;
        }
        else
        {
            // K が来ていないときは自由に動かす
            isFrozen = false;
            _hasFrozenPose = false;
        }

        _wasCalibrating = nowCalibrating;
    }
}

