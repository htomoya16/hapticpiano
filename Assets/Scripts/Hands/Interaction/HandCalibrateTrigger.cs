using UnityEngine;
using Valve.VR.InteractionSystem;

// 触れる or UIボタンからキャリブレーションを実行するコンポーネント
public class HandCalibrateTrigger : MonoBehaviour
{
    [Tooltip("再キャリブする対象の HandVisualFromCurl")]
    public HandVisualFromCurl targetHand;

    [Header("Visual Feedback")]
    [Tooltip("ホバー/接触中に適用する色。未設定（アルファ0）の場合は変更しない。")]
    public Color hoverColor = new Color(0, 1, 1, 1); // シアン
    [Tooltip("キャリブ成功時に適用する色。未設定（アルファ0）の場合は変更しない。")]
    public Color successColor = new Color(0, 1, 0, 1); // グリーン
    [Tooltip("色変更対象の MeshRenderer。未設定なら自身の MeshRenderer を使用。")]
    public MeshRenderer targetRenderer;

    [Header("Touch Trigger")]
    [Tooltip("手やコライダーが触れたときにキャリブレーションを実行する")]
    public bool triggerOnTouch = true;
    [Tooltip("連続発火を防ぐクールダウン秒数")]
    public float cooldownSeconds = 0.5f;

    [Tooltip("キャリブ成功時にログを出す")]
    public bool logOnSuccess = false;
    [Tooltip("キャリブ失敗時に警告を出す")]
    public bool logOnFailure = true;

    private bool _coolingDown = false;
    private Color _baseColor;
    private Material _matInstance;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }
        if (targetRenderer != null)
        {
            _matInstance = targetRenderer.material;
            _baseColor = _matInstance.color;
        }
    }

    // UI Button の OnClick から呼び出す
    public void Calibrate()
    {
        if (targetHand == null)
            return;

        // HandVisualFromCurl のUI経路を利用
        targetHand.RecalibrateFromUIButton();

        if (logOnSuccess)
            Debug.Log("[HandCalibrateTrigger] RecalibrateFromUIButton を呼び出しました");

        ApplySuccessColor();
    }

    // SteamVR InteractionSystem のホバー開始
    private void OnHandHoverBegin(Hand hand)
    {
        if (!triggerOnTouch || _coolingDown) return;
        ApplyHoverColor();
        Calibrate();
        StartCoroutine(Cooldown());
    }

    // 一般的なColliderトリガーでも発火
    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnTouch || _coolingDown) return;
        ApplyHoverColor();
        Calibrate();
        StartCoroutine(Cooldown());
    }

    private System.Collections.IEnumerator Cooldown()
    {
        _coolingDown = true;
        yield return new WaitForSeconds(cooldownSeconds);
        _coolingDown = false;
        RestoreColor();
    }

    private void ApplyHoverColor()
    {
        if (_matInstance != null && hoverColor.a > 0f)
        {
            _matInstance.color = hoverColor;
        }
    }

    private void ApplySuccessColor()
    {
        if (_matInstance != null && successColor.a > 0f)
        {
            _matInstance.color = successColor;
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
