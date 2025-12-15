using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Tooltip("開始直前のカウントダウン（例: '5' '4' ...）※数字のみ表示したい場合はこれを使う")]
    public TMP_Text introCountdownText;

    [Tooltip("次タスクの補足説明（例: '右図のように〜'）。")]
    public TMP_Text instructionText;

    [Tooltip("次タスク案内で表示する図（任意）。")]
    public Image instructionImage;

    [Tooltip("打鍵精度タスク向けの図（任意）。")]
    public Sprite accuracyInstructionSprite;

    [Tooltip("きらきら星タスク向けの図（任意）。")]
    public Sprite twinkleInstructionSprite;

    [Header("Instruction Text (Inspector)")]
    [TextArea(2, 6)]
    public string accuracyInstructionText =
        "右図のように、光って（緑になって）いる鍵盤をテンポに合わせてタッチしてください。\n5秒のカウントダウンのあとに始まります。";

    [TextArea(2, 6)]
    public string twinkleInstructionText =
        "右図のように、光って（緑になって）いる鍵盤を順番にタッチしてください。\n5秒のカウントダウンのあとに始まります。";

    [TextArea(1, 4)]
    public string defaultInstructionText =
        "5秒のカウントダウンのあとに始まります。";

    [Tooltip("次のタスク説明（例: '次(1/4): 打鍵精度 / 触覚なし'）")]
    public TMP_Text taskDescriptionText;

    [Tooltip("現在のタスク表示（例: '打鍵精度 / 触覚あり'）")]
    public TMP_Text currentTaskText;

    [Header("Twinkle Score (optional)")]
    [Tooltip("きらきら星タスク中に表示する楽譜画像の枠（任意）。")]
    public Image twinkleScoreImage;

    [Tooltip("きらきら星タスク中に表示する楽譜画像（任意）。")]
    public Sprite twinkleScoreSprite;

    [Tooltip("きらきら星タスク中に表示するドレミ等のテキスト（任意）。")]
    public TMP_Text twinkleSolfegeText;

    [TextArea(1, 6)]
    public string twinkleSolfege =
        "ド ド ソ ソ ラ ラ ソ\nファ ファ ミ ミ レ レ ド\nソ ソ ファ ファ ミ ミ レ\nソ ソ ファ ファ ミ ミ レ\nド ド ソ ソ ラ ラ ソ\nファ ファ ミ ミ レ レ ド";

    [Tooltip("きらきら星タスク中に楽譜/ドレミ表示を出す。")]
    public bool showTwinkleScoreWhileRunning = true;

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

        bool showCountdown = showCountdownWhileActive && evaluation.IsCountdownActive;
        bool showIntro = showCountdownWhileActive && evaluation.IsTaskIntroActive;
        bool showCurrent = showCurrentTaskWhileRunning && (evaluation.IsTaskRunning || evaluation.IsTaskIntroActive);
        bool showRest = showCountdown;

        bool showTwinkleScore =
            showTwinkleScoreWhileRunning &&
            (
                (evaluation.IsTaskRunning && evaluation.ActiveTaskId == "twinkle") ||
                evaluation.IsTrainingMidiDemoRunning
            );

        // Visibility & texts
        bool introUsesOwnText = showIntro && introCountdownText != null;
        bool introUsesFallbackCountdownText = showIntro && introCountdownText == null && countdownText != null;

        if (taskDescriptionText != null) taskDescriptionText.gameObject.SetActive(showCountdown);
        if (instructionText != null) instructionText.gameObject.SetActive(showCountdown);
        if (instructionImage != null) instructionImage.gameObject.SetActive(showCountdown);
        if (countdownText != null) countdownText.gameObject.SetActive(showCountdown || introUsesFallbackCountdownText);
        if (introCountdownText != null) introCountdownText.gameObject.SetActive(introUsesOwnText);
        if (currentTaskText != null) currentTaskText.gameObject.SetActive(showCurrent || showRest);

        if (twinkleScoreImage != null) twinkleScoreImage.gameObject.SetActive(showTwinkleScore);
        if (twinkleSolfegeText != null) twinkleSolfegeText.gameObject.SetActive(showTwinkleScore);

        if (showCountdown)
        {
            if (taskDescriptionText != null)
            {
                taskDescriptionText.text = evaluation.GetNextScheduleStepDescriptionJa();
            }

            float remain = evaluation.CountdownRemainingSeconds;
            int sec = Mathf.CeilToInt(Mathf.Max(0f, remain));
            if (countdownText != null) countdownText.text = $"休憩時間: {sec}秒";

            string nextId = evaluation.NextOrIntroTaskId;
            if (instructionText != null) instructionText.text = GetInstructionText(nextId);

            if (instructionImage != null)
            {
                var sprite = nextId == "twinkle" ? twinkleInstructionSprite : nextId == "accuracy" ? accuracyInstructionSprite : null;
                instructionImage.sprite = sprite;
                instructionImage.enabled = sprite != null;
            }
        }

        if (showTwinkleScore)
        {
            if (twinkleScoreImage != null)
            {
                twinkleScoreImage.sprite = twinkleScoreSprite;
                twinkleScoreImage.enabled = twinkleScoreSprite != null;
            }

            if (twinkleSolfegeText != null)
            {
                twinkleSolfegeText.text = twinkleSolfege ?? "";
            }
        }

        if (showIntro)
        {
            float remain = evaluation.TaskIntroRemainingSeconds;
            int sec = Mathf.CeilToInt(Mathf.Max(0f, remain));

            if (introCountdownText != null) introCountdownText.text = sec.ToString();
            else if (countdownText != null) countdownText.text = sec.ToString();
        }

        if (showCurrent)
        {
            string tid = evaluation.CurrentOrIntroTaskId;
            string taskJa = tid == "accuracy" ? "打鍵精度" : tid == "twinkle" ? "きらきら星" : tid;
            string condJa = evaluation.condition == EvaluationCondition.TouchOn ? "触覚あり" : "触覚なし";
            if (currentTaskText != null) currentTaskText.text = $"{taskJa} / {condJa}";
        }
        else if (showRest)
        {
            if (currentTaskText != null) currentTaskText.text = "休憩時間";
        }
    }

    private string GetInstructionText(string taskId)
    {
        if (taskId == "accuracy")
        {
            return string.IsNullOrWhiteSpace(accuracyInstructionText) ? (defaultInstructionText ?? "") : accuracyInstructionText;
        }

        if (taskId == "twinkle")
        {
            return string.IsNullOrWhiteSpace(twinkleInstructionText) ? (defaultInstructionText ?? "") : twinkleInstructionText;
        }

        return defaultInstructionText ?? "";
    }
}
