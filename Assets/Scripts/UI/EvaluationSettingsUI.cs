using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 設定画面（SettingsOverlay）から評価タスクを起動/切替するためのUIブリッジ。
/// シーン側の Button OnClick() からこの public メソッドを呼ぶ想定。
/// </summary>
[DisallowMultipleComponent]
public sealed class EvaluationSettingsUI : MonoBehaviour
{
    [Header("References")]
    public SettingsOverlayOpener overlayOpener;
    public EvaluationTaskController evaluation;

    [Header("UI (optional)")]
    public TMP_Text statusText;
    public TMP_InputField participantIdInput;
    public TMP_InputField participantNameInput;

    [Header("Task Button (Start/Abort)")]
    [Tooltip("タスク開始/中止のボタンルート（ラベル切替用）。")]
    public GameObject taskButtonRoot;

    [Tooltip("タスク開始/中止ボタン（interactable 切替用）。")]
    public Button taskButton;

    [Tooltip("ボタンの表示テキスト（『タスクスタート』『中止』『完了』を切替）。")]
    public TMP_Text taskButtonLabel;

    [Header("Task Button (Color)")]
    [Tooltip("メインボタンの色も状態に応じて切り替える")]
    public bool tintTaskButton = true;

    [Tooltip("待機中（開始可能）")]
    public Color taskStartButtonColor = new Color(0.20f, 0.65f, 0.28f, 1f);

    [Tooltip("実行中/カウントダウン中（中止）")]
    public Color taskAbortButtonColor = new Color(0.85f, 0.25f, 0.25f, 1f);

    [Tooltip("全タスク完了（押せない）")]
    public Color taskDoneButtonColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Tooltip("ホバー時の明るさ倍率（VRでホバーがない場合は気にしなくてOK）")]
    [Range(1f, 2f)]
    public float buttonHighlightMultiplier = 1.15f;

    [Tooltip("押下時の暗さ倍率")]
    [Range(0.1f, 1f)]
    public float buttonPressedMultiplier = 0.9f;

    [Tooltip("停止ボタンのルート（実行中/カウントダウン中のみ表示したい場合に設定）")]
    public GameObject stopButtonRoot;
    [Tooltip("停止ボタン（実行中/カウントダウン中のみ押せるようにしたい場合に設定）")]
    public Button stopButton;

    [Header("Behavior")]
    [Tooltip("ボタン操作後に設定画面を閉じる")]
    public bool closeOverlayAfterAction = true;

    [Tooltip("入力欄の編集終了（Enter/フォーカス解除）で自動適用する")]
    public bool applyOnEndEdit = true;

    private bool _listenersBound;
    private bool _prevLocked;

    private void Start()
    {
        if (overlayOpener == null) overlayOpener = FindObjectOfType<SettingsOverlayOpener>();
        if (evaluation == null) evaluation = FindObjectOfType<EvaluationTaskController>();

        BindListenersIfNeeded();
        Refresh();
    }

    private void OnEnable()
    {
        BindListenersIfNeeded();
        Refresh();
    }

    private void Update()
    {
        if (evaluation == null) return;

        bool locked = evaluation.HasParticipantInfoLocked;
        if (locked != _prevLocked)
        {
            _prevLocked = locked;
            ApplyIdNameLockState(locked);
        }

        ApplyTaskButtonState();
        ApplyStopButtonState();
    }

    private void OnDisable()
    {
        UnbindListenersIfNeeded();
    }

    public void Refresh()
    {
        if (evaluation == null) return;

        _prevLocked = evaluation.HasParticipantInfoLocked;
        ApplyIdNameLockState(_prevLocked);
        ApplyTaskButtonState();
        ApplyStopButtonState();

        if (participantIdInput != null)
        {
            if (!participantIdInput.isFocused)
            {
                participantIdInput.text = evaluation.participantId ?? string.Empty;
            }
        }

        if (participantNameInput != null)
        {
            if (!participantNameInput.isFocused)
            {
                participantNameInput.text = evaluation.participantName ?? string.Empty;
            }
        }

        if (statusText != null)
        {
            string group = evaluation.group == EvaluationGroup.A ? "A（順序: no→yes / yes→no）" : "B（順序: yes→no / no→yes）";
            bool isTouchOn = evaluation.condition == EvaluationCondition.TouchOn;
            string cond = isTouchOn ? "touch_on" : "touch_off";
            string task = evaluation.IsTaskRunning ? evaluation.ActiveTaskId : "none";
            string schedule = evaluation.useGroupSchedule ? evaluation.GetNextScheduleStepLabel() : "(schedule off)";
            statusText.text = $"participant={evaluation.participantId}\nname={evaluation.participantName}\ngroup={group}\ncondition={cond}\ntask={task}\n{schedule}";
        }
    }

    public void ApplyParticipantId()
    {
        if (evaluation == null || participantIdInput == null) return;
        if (evaluation.HasParticipantInfoLocked) return;
        string sanitized = EvaluationLogSession.SanitizeParticipantId(participantIdInput.text);
        bool changed = !string.Equals(evaluation.participantId ?? "", sanitized, System.StringComparison.Ordinal);

        evaluation.participantId = sanitized;
        if (!participantIdInput.isFocused) participantIdInput.text = sanitized;
        if (changed) evaluation.ResetLogSession();
        Refresh();
    }

    public void ApplyParticipantName()
    {
        if (evaluation == null || participantNameInput == null) return;
        if (evaluation.HasParticipantInfoLocked) return;
        string v = participantNameInput.text ?? "";
        bool changed = !string.Equals(evaluation.participantName ?? "", v, System.StringComparison.Ordinal);

        evaluation.participantName = v;
        if (changed) evaluation.ResetLogSession();
        Refresh();
    }

    public void ApplyParticipantIdAndName()
    {
        if (evaluation == null) return;
        if (evaluation.HasParticipantInfoLocked) return;

        string nextId = evaluation.participantId ?? "";
        string nextName = evaluation.participantName ?? "";

        if (participantIdInput != null)
        {
            nextId = EvaluationLogSession.SanitizeParticipantId(participantIdInput.text);
        }

        if (participantNameInput != null)
        {
            nextName = participantNameInput.text ?? "";
        }

        bool changed =
            !string.Equals(evaluation.participantId ?? "", nextId, System.StringComparison.Ordinal) ||
            !string.Equals(evaluation.participantName ?? "", nextName, System.StringComparison.Ordinal);

        evaluation.participantId = nextId;
        evaluation.participantName = nextName;
        if (changed) evaluation.ResetLogSession();

        Refresh();
    }

    public void RevertInputs()
    {
        Refresh();
    }

    public void SetConditionTouchOn()
    {
        if (evaluation == null) return;
        evaluation.condition = EvaluationCondition.TouchOn;
        Refresh();
    }

    public void SetConditionTouchOff()
    {
        if (evaluation == null) return;
        evaluation.condition = EvaluationCondition.TouchOff;
        Refresh();
    }

    public void SelectGroupA()
    {
        if (evaluation == null) return;
        if (evaluation.HasParticipantInfoLocked) return;
        evaluation.group = EvaluationGroup.A;
        evaluation.ResetSchedule();
        SetConditionTouchOff(); // A は最初が touch_off
    }

    public void SelectGroupB()
    {
        if (evaluation == null) return;
        if (evaluation.HasParticipantInfoLocked) return;
        evaluation.group = EvaluationGroup.B;
        evaluation.ResetSchedule();
        SetConditionTouchOn(); // B は最初が touch_on
    }

    public void StartAccuracyTask()
    {
        if (evaluation == null) return;
        evaluation.StartAccuracyTask();
        AfterAction();
    }

    public void StartTwinkleTask()
    {
        if (evaluation == null) return;
        evaluation.StartTwinkleTask();
        AfterAction();
    }

    public void PlayTrainingDemoOnce()
    {
        if (evaluation == null) return;
        evaluation.PlayTrainingMidiDemoOnce();
        AfterAction();
    }

    public void StopTask()
    {
        if (evaluation == null) return;
        if (!CanStopNow()) return;
        evaluation.StopCurrentTask();
        AfterAction();
    }

    /// <summary>
    /// 1つのボタンで運用するためのメイン操作。
    /// - 待機中: 次のタスク開始（20秒カウントダウン開始）
    /// - 実行中/カウントダウン中: 中止（停止/キャンセル）
    /// </summary>
    public void TaskStartOrAbort()
    {
        if (evaluation == null) return;

        if (CanStopNow())
        {
            evaluation.StopCurrentTask();
        }
        else
        {
            evaluation.BeginCountdownToNextScheduledTask();
        }

        AfterAction();
    }

    private void AfterAction()
    {
        Refresh();

        if (closeOverlayAfterAction && overlayOpener != null)
        {
            overlayOpener.Close();
        }
    }

    private bool CanStopNow()
    {
        return evaluation != null && (evaluation.IsTaskRunning || evaluation.IsCountdownActive);
    }

    private bool CanStartNow()
    {
        if (evaluation == null) return false;
        if (!evaluation.useGroupSchedule) return false;
        if (evaluation.IsTaskRunning || evaluation.IsCountdownActive) return false;
        return evaluation.GetScheduleIndex() < evaluation.GetScheduleLength();
    }

    private void ApplyTaskButtonState()
    {
        if (evaluation == null) return;

        bool canStop = CanStopNow();
        bool canStart = CanStartNow();
        bool done = !canStop && !canStart && evaluation.useGroupSchedule && evaluation.GetScheduleLength() > 0 && evaluation.GetScheduleIndex() >= evaluation.GetScheduleLength();

        if (taskButtonLabel != null)
        {
            taskButtonLabel.text = done ? "完了" : (canStop ? "中止" : "タスクスタート");
        }

        if (taskButton != null)
        {
            taskButton.interactable = canStop || canStart;
            if (tintTaskButton)
            {
                var baseColor = done ? taskDoneButtonColor : (canStop ? taskAbortButtonColor : taskStartButtonColor);
                ApplyButtonTint(taskButton, baseColor);
            }
        }

        if (taskButtonRoot != null && !taskButtonRoot.activeSelf)
        {
            taskButtonRoot.SetActive(true);
        }
    }

    private void ApplyButtonTint(Button b, Color baseColor)
    {
        if (b == null) return;

        var colors = b.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = MultiplyRgb(baseColor, buttonHighlightMultiplier);
        colors.pressedColor = MultiplyRgb(baseColor, buttonPressedMultiplier);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = taskDoneButtonColor;
        b.colors = colors;
    }

    private static Color MultiplyRgb(Color c, float m)
    {
        return new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), c.a);
    }

    private void ApplyStopButtonState()
    {
        bool canStop = CanStopNow();
        if (stopButtonRoot != null)
        {
            if (stopButtonRoot.activeSelf != canStop) stopButtonRoot.SetActive(canStop);
        }

        if (stopButton != null)
        {
            stopButton.interactable = canStop;
            if (stopButton.gameObject.activeSelf != canStop) stopButton.gameObject.SetActive(canStop);
        }
    }

    private void BindListenersIfNeeded()
    {
        if (_listenersBound) return;
        if (!applyOnEndEdit) return;

        if (participantIdInput != null)
        {
            participantIdInput.onEndEdit.RemoveListener(OnParticipantIdEndEdit);
            participantIdInput.onEndEdit.AddListener(OnParticipantIdEndEdit);
        }

        if (participantNameInput != null)
        {
            participantNameInput.onEndEdit.RemoveListener(OnParticipantNameEndEdit);
            participantNameInput.onEndEdit.AddListener(OnParticipantNameEndEdit);
        }

        _listenersBound = true;
    }

    private void UnbindListenersIfNeeded()
    {
        if (!_listenersBound) return;
        _listenersBound = false;

        if (participantIdInput != null)
        {
            participantIdInput.onEndEdit.RemoveListener(OnParticipantIdEndEdit);
        }

        if (participantNameInput != null)
        {
            participantNameInput.onEndEdit.RemoveListener(OnParticipantNameEndEdit);
        }
    }

    private void OnParticipantIdEndEdit(string _)
    {
        ApplyParticipantId();
    }

    private void OnParticipantNameEndEdit(string _)
    {
        ApplyParticipantName();
    }

    private void ApplyIdNameLockState(bool locked)
    {
        bool canEdit = !locked;

        if (participantIdInput != null) participantIdInput.interactable = canEdit;
        if (participantNameInput != null) participantNameInput.interactable = canEdit;
    }
}
