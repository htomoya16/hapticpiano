using System.Collections;
using UnityEngine;
using Valve.VR.InteractionSystem;

// 物理ボタンに付け、手が触れたときに HandVisualFromCurl を再キャリブする
public class CalibPhysicalButton : MonoBehaviour
{
    [Tooltip("このボタンで再キャリブする対象の HandVisualFromCurl")]
    public HandVisualFromCurl targetHand;

    [Tooltip("連打防止のクールダウン秒数")]
    public float cooldownSeconds = 0.5f;

    [Tooltip("成功時にログを出す")]
    public bool logOnSuccess = false;

    [Tooltip("失敗時に警告を出す")]
    public bool logOnFailure = true;

    [Header("Visual Feedback")]
    [Tooltip("ホバー中に適用する色。未設定（アルファ0）の場合は変更しない。")]
    public Color hoverColor = new Color(0, 1, 1, 1); // シアン
    [Tooltip("押下成功時に適用する色。未設定（アルファ0）の場合は変更しない。")]
    public Color pressedColor = new Color(0, 1, 0, 1); // グリーン
    [Tooltip("色変更対象の MeshRenderer。未設定なら自身の MeshRenderer を使用。")]
    public MeshRenderer targetRenderer;

    private bool _coolingDown;
    private Color _baseColor;
    private Material _matInstance;

    private void Awake()
    {
        // 物理ボタンは動かさないのでキネマティックに
        var rb = GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        if (targetRenderer != null)
        {
            // マテリアルインスタンスを確保（共有マテリアルを書き換えないため）
            _matInstance = targetRenderer.material;
            _baseColor = _matInstance.color;
        }
    }

    private void OnHandHoverBegin(Hand hand)
    {
        if (_coolingDown || targetHand == null)
            return;

        bool ok = targetHand.CalibrateFromSkeleton();

        if (ok)
        {
            if (logOnSuccess)
                Debug.Log("[CalibPhysicalButton] キャリブ成功");
            ApplyPressedColor();
        }
        else
        {
            if (logOnFailure)
                Debug.LogWarning("[CalibPhysicalButton] キャリブ失敗: Skeleton を確認してください。");
        }

        StartCoroutine(Cooldown());
    }

    private void OnHandHoverStart(Hand hand)
    {
        ApplyHoverColor();
    }

    private void OnHandHoverEnd(Hand hand)
    {
        RestoreColor();
    }

    private IEnumerator Cooldown()
    {
        _coolingDown = true;
        yield return new WaitForSeconds(cooldownSeconds);
        _coolingDown = false;
    }

    private void ApplyHoverColor()
    {
        if (_matInstance != null && hoverColor.a > 0f)
        {
            _matInstance.color = hoverColor;
        }
    }

    private void ApplyPressedColor()
    {
        if (_matInstance != null && pressedColor.a > 0f)
        {
            _matInstance.color = pressedColor;
        }
    }

    private void RestoreColor()
    {
        if (_matInstance != null)
        {
            _matInstance.color = _baseColor;
        }
    }
}
