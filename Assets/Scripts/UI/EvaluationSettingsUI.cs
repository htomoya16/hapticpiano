using TMPro;
using UnityEngine;

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

        bool locked = evaluation.HasRunAnyTask;
        if (locked != _prevLocked)
        {
            _prevLocked = locked;
            ApplyIdNameLockState(locked);
        }
    }

    private void OnDisable()
    {
        UnbindListenersIfNeeded();
    }

    public void Refresh()
    {
        if (evaluation == null) return;

        _prevLocked = evaluation.HasRunAnyTask;
        ApplyIdNameLockState(_prevLocked);

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
            bool isTouchOn = evaluation.condition == EvaluationCondition.TouchOn;
            string group = isTouchOn ? "B（触覚あり）" : "A（触覚なし）";
            string cond = isTouchOn ? "touch_on" : "touch_off";
            string task = evaluation.IsTaskRunning ? evaluation.ActiveTaskId : "none";
            statusText.text = $"participant={evaluation.participantId}\nname={evaluation.participantName}\ngroup={group}\ncondition={cond}\ntask={task}";
        }
    }

    public void ApplyParticipantId()
    {
        if (evaluation == null || participantIdInput == null) return;
        if (evaluation.HasRunAnyTask) return;
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
        if (evaluation.HasRunAnyTask) return;
        string v = participantNameInput.text ?? "";
        bool changed = !string.Equals(evaluation.participantName ?? "", v, System.StringComparison.Ordinal);

        evaluation.participantName = v;
        if (changed) evaluation.ResetLogSession();
        Refresh();
    }

    public void ApplyParticipantIdAndName()
    {
        if (evaluation == null) return;
        if (evaluation.HasRunAnyTask) return;

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

    public void SetGroupA_NoHaptics()
    {
        SetConditionTouchOff();
    }

    public void SetGroupB_WithHaptics()
    {
        SetConditionTouchOn();
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
        evaluation.StopCurrentTask();
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
