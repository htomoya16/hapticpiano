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
    public enum ManualAction
    {
        AccuracyDemo = 0,
        TwinkleDemo = 1,
        ScheduledTasks = 2,
    }

    [Header("References")]
    public SettingsOverlayOpener overlayOpener;
    public EvaluationTaskController evaluation;

    [Header("UI (optional)")]
    public TMP_Text statusText;
    public TMP_InputField participantIdInput;
    public TMP_InputField participantNameInput;

    [Header("Selection Buttons (optional labels)")]
    [Tooltip("『打鍵精度タスク（デモ）』選択ボタンの表示テキスト（任意）。グループA/Bが分かるように末尾を更新する。")]
    public TMP_Text selectAccuracyDemoLabel;

    [Tooltip("『きらきら星（デモ）』選択ボタンの表示テキスト（任意）。グループA/Bが分かるように末尾を更新する。")]
    public TMP_Text selectTwinkleDemoLabel;

    [Tooltip("『一連のタスク』選択ボタンの表示テキスト（任意）。グループA/Bが分かるように末尾を更新する。")]
    public TMP_Text selectScheduledTasksLabel;

    [Header("Task Button (Start/Abort)")]
    [Tooltip("タスク開始/中止のボタンルート（ラベル切替用）。")]
    public GameObject taskButtonRoot;

    [Tooltip("タスク開始/中止ボタン（interactable 切替用）。")]
    public Button taskButton;

    [Tooltip("ボタンの表示テキスト（『スタート』『中止』『完了』を切替）。")]
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

    [Header("Behavior")]
    [Tooltip("ボタン操作後に設定画面を閉じる")]
    public bool closeOverlayAfterAction = true;

    [Header("Selection")]
    [Tooltip("開始ボタンで実行する内容（3択）。")]
    public ManualAction manualSelection = ManualAction.AccuracyDemo;

    [Tooltip("現在の選択を表示するテキスト（任意）。")]
    public TMP_Text manualSelectionText;

    [Tooltip("入力欄の編集終了（Enter/フォーカス解除）で自動適用する")]
    public bool applyOnEndEdit = true;

    private bool _listenersBound;
    private bool _prevLocked;
    private bool _prevCountdownActive;
    private bool _prevTaskRunning;
    private bool _prevIntroActive;

    private void Start()
    {
        if (overlayOpener == null) overlayOpener = FindObjectOfType<SettingsOverlayOpener>();
        if (evaluation == null) evaluation = FindObjectOfType<EvaluationTaskController>();

        BindListenersIfNeeded();
        Refresh();

        if (evaluation != null)
        {
            _prevCountdownActive = evaluation.IsCountdownActive;
            _prevTaskRunning = evaluation.IsTaskRunning;
            _prevIntroActive = evaluation.IsTaskIntroActive;
        }
    }

    private void OnEnable()
    {
        BindListenersIfNeeded();
        Refresh();

        if (evaluation != null)
        {
            _prevCountdownActive = evaluation.IsCountdownActive;
            _prevTaskRunning = evaluation.IsTaskRunning;
            _prevIntroActive = evaluation.IsTaskIntroActive;
        }
    }

    private void Update()
    {
        if (evaluation == null) return;

        bool countdownActive = evaluation.IsCountdownActive;
        bool taskRunning = evaluation.IsTaskRunning;
        bool introActive = evaluation.IsTaskIntroActive;
        bool startedCountdown = countdownActive && !_prevCountdownActive;
        bool startedTask = taskRunning && !_prevTaskRunning;
        bool startedIntro = introActive && !_prevIntroActive;
        _prevCountdownActive = countdownActive;
        _prevTaskRunning = taskRunning;
        _prevIntroActive = introActive;

        // デモ/タスク開始時に設定画面を自動で閉じたい（ただし実行中に開いた場合は閉じない）
        if ((startedCountdown || startedIntro || startedTask) && overlayOpener != null && overlayOpener.IsOpen)
        {
            overlayOpener.Close();
        }

        bool locked = evaluation.HasParticipantInfoLocked;
        if (locked != _prevLocked)
        {
            _prevLocked = locked;
            ApplyIdNameLockState(locked);
        }

        ApplyTaskButtonState();
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
        ApplySelectionButtonLabels();

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
            string groupCore = evaluation.group == EvaluationGroup.A ? "A（順序: no→yes / yes→no）" : "B（順序: yes→no / no→yes）";
            string group = evaluation.HasExplicitGroupSelection ? groupCore : $"(未選択) {groupCore}";
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
        evaluation.MarkGroupSelected();
        evaluation.ResetSchedule();
        SetConditionTouchOff(); // A は最初が touch_off
    }

    public void SelectGroupB()
    {
        if (evaluation == null) return;
        if (evaluation.HasParticipantInfoLocked) return;
        evaluation.group = EvaluationGroup.B;
        evaluation.MarkGroupSelected();
        evaluation.ResetSchedule();
        SetConditionTouchOn(); // B は最初が touch_on
    }

    public void SelectAccuracyDemo()
    {
        manualSelection = ManualAction.AccuracyDemo;
        Refresh();
    }

    public void SelectTwinkleDemo()
    {
        manualSelection = ManualAction.TwinkleDemo;
        Refresh();
    }

    public void SelectScheduledTasks()
    {
        manualSelection = ManualAction.ScheduledTasks;
        Refresh();
    }

    /// <summary>
    /// 選択式（manualSelection）の Start/Abort。
    /// - 待機中: 選択されたデモ/タスクを開始
    /// - 実行中/カウントダウン中/デモ中: 中止
    /// </summary>
    public void StartOrAbortSelected()
    {
        if (evaluation == null) return;

        if (CanStopNow())
        {
            evaluation.AbortCurrentTask();
            AfterAction(forceCloseOverlay: true);
            return;
        }

        if (!CanStartSelectedNow()) return;

        switch (manualSelection)
        {
            case ManualAction.AccuracyDemo:
                evaluation.PlayAccuracyPatternDemoOnce();
                break;
            case ManualAction.TwinkleDemo:
                evaluation.PlayTrainingMidiDemoOnce();
                break;
            case ManualAction.ScheduledTasks:
                evaluation.BeginCountdownToNextScheduledTask();
                break;
        }

        AfterAction(forceCloseOverlay: true);
    }

    private void AfterAction(bool forceCloseOverlay = false)
    {
        Refresh();

        if ((forceCloseOverlay || closeOverlayAfterAction) && overlayOpener != null)
        {
            overlayOpener.Close();
        }
    }

    private bool CanStopNow()
    {
        return evaluation != null && (evaluation.IsTaskRunning || evaluation.IsCountdownActive || evaluation.IsTaskIntroActive || evaluation.IsAnyDemoRunning);
    }

    private bool CanStartSelectedNow()
    {
        if (evaluation == null) return false;
        if (evaluation.IsTaskRunning || evaluation.IsCountdownActive || evaluation.IsTaskIntroActive) return false;
        if (evaluation.IsAnyDemoRunning) return false;

        if (manualSelection == ManualAction.ScheduledTasks)
        {
            if (!evaluation.useGroupSchedule) return false;
            if (!evaluation.HasExplicitGroupSelection) return false;
            if (evaluation.GetScheduleIndex() >= evaluation.GetScheduleLength()) return false;
        }
        return true;
    }

    private void ApplyTaskButtonState()
    {
        if (evaluation == null) return;

        bool canStop = CanStopNow();
        bool canStart = CanStartSelectedNow();

        bool scheduleDone = evaluation.useGroupSchedule &&
            evaluation.GetScheduleLength() > 0 &&
            evaluation.GetScheduleIndex() >= evaluation.GetScheduleLength();

        bool done = !canStop && scheduleDone && manualSelection == ManualAction.ScheduledTasks;

        if (taskButtonLabel != null)
        {
            taskButtonLabel.text = done ? "完了" : (canStop ? "中止" : "スタート");
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

        if (manualSelectionText != null)
        {
            string label = GetManualSelectionLabelJa(manualSelection);
            if (manualSelection == ManualAction.ScheduledTasks)
            {
                manualSelectionText.text = $"{label}{GetGroupSuffixShort()}";
            }
            else
            {
                manualSelectionText.text = label;
            }
        }
    }

    private void ApplySelectionButtonLabels()
    {
        if (evaluation == null) return;

        string groupSuffix = GetGroupSuffixShort();

        // 選択ボタン自体の表示にもグループを出しておく（スタートボタンはグループで文字を変えない）
        if (selectAccuracyDemoLabel != null) selectAccuracyDemoLabel.text = $"打鍵精度タスク（デモ）{groupSuffix}";
        if (selectTwinkleDemoLabel != null) selectTwinkleDemoLabel.text = $"きらきら星（デモ）{groupSuffix}";
        if (selectScheduledTasksLabel != null) selectScheduledTasksLabel.text = $"一連のタスク{groupSuffix}";
    }

    private string GetGroupSuffixShort()
    {
        if (evaluation == null) return "";
        if (!evaluation.HasExplicitGroupSelection) return "（A/B未選択）";
        return evaluation.group == EvaluationGroup.A ? "（A）" : "（B）";
    }

    private static string GetManualSelectionLabelJa(ManualAction a)
    {
        switch (a)
        {
            case ManualAction.AccuracyDemo: return "打鍵精度タスク（デモ）";
            case ManualAction.TwinkleDemo: return "きらきら星（デモ）";
            case ManualAction.ScheduledTasks: return "一連のタスク";
            default: return a.ToString();
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
        // Inspector 側で colorMultiplier が低いと、押せるのに暗く見えることがあるため固定。
        colors.colorMultiplier = 1f;
        b.colors = colors;

        // ColorTint の場合に確実に反映させたい（targetGraphic の初期色が暗い/透明なケース対策）
        if (b.targetGraphic != null) b.targetGraphic.color = colors.normalColor;
    }

    private static Color MultiplyRgb(Color c, float m)
    {
        return new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), c.a);
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
