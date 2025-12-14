using TMPro;
using UnityEngine;

/// <summary>
/// 次タスクの説明＋カウントダウン（分離）と、現在タスク表示を VR（ワールド空間）で表示する。
/// 位置・サイズ・フォント等のレイアウトは Unity 側（Inspector / RectTransform）で調整する前提。
/// </summary>
[DisallowMultipleComponent]
public sealed class EvaluationCountdownWorldUI : MonoBehaviour
{
    [Header("References")]
    public EvaluationTaskController evaluation;
    public Camera targetCamera;

    [Header("UI (optional)")]
    public Canvas canvas;

    [Tooltip("開始までの秒数表示（例: '開始まで 20 秒'）")]
    public TMP_Text countdownText;

    [Tooltip("次のタスク説明（例: '次(1/4): 打鍵精度 / 触覚なし'）")]
    public TMP_Text taskDescriptionText;

    [Tooltip("現在のタスク表示（例: '打鍵精度 / 触覚あり'）")]
    public TMP_Text currentTaskText;

    [Header("Behavior")]
    [Tooltip("カメラ前に追従させる")]
    public bool followCamera = true;

    public bool showCountdownWhileActive = true;
    public bool showCurrentTaskWhileRunning = true;

    [Header("Follow Layout")]
    public float distanceMeters = 1.2f;
    public float verticalOffsetMeters = -0.12f;
    public float horizontalOffsetMeters = 0.0f;

    private void Start()
    {
        if (evaluation == null) evaluation = FindObjectOfType<EvaluationTaskController>();
        if (targetCamera == null) targetCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        if (canvas == null) canvas = GetComponentInChildren<Canvas>(includeInactive: true);
        UpdateVisibilityAndTexts();
    }

    private void LateUpdate()
    {
        if (evaluation == null) return;
        if (targetCamera == null) return;
        UpdateVisibilityAndTexts();
    }

    private void UpdateVisibilityAndTexts()
    {
        if (evaluation == null || targetCamera == null) return;
        if (canvas == null) return;

        if (followCamera)
        {
            Vector3 pos = targetCamera.transform.position + targetCamera.transform.forward * distanceMeters;
            pos += targetCamera.transform.up * verticalOffsetMeters;
            pos += targetCamera.transform.right * horizontalOffsetMeters;

            canvas.transform.position = pos;
            canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - targetCamera.transform.position, Vector3.up);
        }

        bool showCountdown = showCountdownWhileActive && (evaluation.IsCountdownActive || evaluation.IsTaskIntroActive);
        bool showCurrent = showCurrentTaskWhileRunning && (evaluation.IsTaskRunning || evaluation.IsTaskIntroActive);

        // Visibility & texts
        if (taskDescriptionText != null) taskDescriptionText.gameObject.SetActive(showCountdown);
        if (countdownText != null) countdownText.gameObject.SetActive(showCountdown);
        if (currentTaskText != null) currentTaskText.gameObject.SetActive(showCurrent);

        if (showCountdown)
        {
            float remain = evaluation.IsCountdownActive ? evaluation.CountdownRemainingSeconds : evaluation.TaskIntroRemainingSeconds;
            int sec = Mathf.CeilToInt(Mathf.Max(0f, remain));

            if (taskDescriptionText != null)
            {
                taskDescriptionText.text = evaluation.IsCountdownActive
                    ? evaluation.GetNextScheduleStepDescriptionJa()
                    : evaluation.GetTaskIntroDescriptionJa();
            }

            if (countdownText != null) countdownText.text = $"開始まで {sec} 秒";
        }

        if (showCurrent)
        {
            string tid = evaluation.CurrentOrIntroTaskId;
            string taskJa = tid == "accuracy" ? "打鍵精度" : tid == "twinkle" ? "きらきら星" : tid;
            string condJa = evaluation.condition == EvaluationCondition.TouchOn ? "触覚あり" : "触覚なし";
            if (currentTaskText != null) currentTaskText.text = $"{taskJa} / {condJa}";
        }
    }
}
