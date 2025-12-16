using System.Text;
using UnityEngine;

public sealed partial class EvaluationTaskController : MonoBehaviour
{
    /// <summary>
    /// 休憩（カウントダウン）をスキップして、直前5秒カウントダウンへ進める。
    /// </summary>
    public bool SkipRestCountdown()
    {
        if (!isCountingDown) return false;
        _countdownEndRealtime = Time.realtimeSinceStartup;
        TickCountdown();
        return true;
    }

    public void ResetSchedule()
    {
        scheduleStepIndex = 0;
    }

    public int GetScheduleIndex() => scheduleStepIndex;
    public int GetScheduleLength() => GetScheduleSteps(group).Length;

    public void MarkGroupSelected()
    {
        if (requireExplicitGroupSelection) hasExplicitGroupSelection = true;
    }

    public string GetNextScheduleStepLabel()
    {
        var steps = GetScheduleSteps(group);
        if (scheduleStepIndex < 0) return "(none)";
        if (scheduleStepIndex >= steps.Length) return "(done)";
        var step = steps[scheduleStepIndex];
        return $"step {scheduleStepIndex + 1}/{steps.Length}: {ToTaskId(step.task)}({ToConditionString(step.condition)})";
    }

    public string GetNextScheduleStepDescriptionJa()
    {
        var steps = GetScheduleSteps(group);
        if (scheduleStepIndex < 0) return "次: なし";
        if (scheduleStepIndex >= steps.Length) return "完了";
        var step = steps[scheduleStepIndex];
        return FormatTaskTableJa(titlePrefix: $"次({scheduleStepIndex + 1}/{steps.Length})", step.task, step.condition);
    }

    public string GetTaskIntroDescriptionJa()
    {
        if (!_hasIntroStep) return "準備中";
        var steps = GetScheduleSteps(group);
        int total = steps.Length;
        int idx = Mathf.Clamp(scheduleStepIndex, 0, Mathf.Max(0, total));
        return FormatTaskTableJa(titlePrefix: $"開始({Mathf.Clamp(idx + 1, 1, total)}/{Mathf.Max(1, total)})", _introStep.task, _introStep.condition);
    }

    private string FormatTaskTableJa(string titlePrefix, Mode task, EvaluationCondition cond)
    {
        string taskJa = task == Mode.Accuracy ? "打鍵精度" : task == Mode.Twinkle ? "きらきら星" : "-";
        string hapticsJa = cond == EvaluationCondition.TouchOn ? "あり" : "なし";

        // `PadRight` は文字数で揃えるため、表示幅（プロポーショナル）だと ":" がズレる。
        // 既定は `<mspace>` で等幅化してコロン位置を縦に揃える。
        string labelTask = "タスク";
        string labelHaptics = "触覚";
        int minLabelWidth = Mathf.Max(labelTask.Length, labelHaptics.Length);
        int w = Mathf.Clamp(nextTaskTableTaskColumnWidth, minLabelWidth, minLabelWidth + 6);

        string title = titlePrefix ?? "";
        string header = $"{labelTask.PadRight(w)}: {taskJa}";
        string row = $"{labelHaptics.PadRight(w)}: {hapticsJa}";
        string sep = new string('-', Mathf.Max(header.Length, row.Length));

        var sb = new StringBuilder();
        // タイトル→区切り線→内容（区切り線を2行目にする）
        if (!string.IsNullOrEmpty(title)) sb.AppendLine(title);
        sb.AppendLine(sep);
        sb.AppendLine(header);
        sb.Append(row);

        string s = sb.ToString();
        if (!nextTaskTableUseMonospaceTag) return s;
        return $"<mspace={Mathf.Clamp(nextTaskTableMspaceEm, 0.3f, 1.2f):0.###}em>{s}</mspace>";
    }

    public bool BeginCountdownToNextScheduledTask()
    {
        if (!useGroupSchedule) return false;
        if (IsTaskRunning) return false;
        if (isCountingDown) return false;
        if (isTaskIntroActive) return false;
        if (requireExplicitGroupSelection && !hasExplicitGroupSelection) return false;

        var steps = GetScheduleSteps(group);
        if (scheduleStepIndex < 0) scheduleStepIndex = 0;
        if (scheduleStepIndex >= steps.Length) return false;

        HasParticipantInfoLocked = true;

        _pendingStep = steps[scheduleStepIndex];
        _hasPendingStep = true;
        condition = _pendingStep.condition;

        // 休憩（カウントダウン）中の条件に合わせて、次が触覚なしなら送信を止める。
        // StopCurrentTaskInternal で RestoreHapticsIfNeeded() が呼ばれているため、ここでは上書きしても復元可能。
        if (disableHapticsDuringRestWhenNextIsTouchOff && condition == EvaluationCondition.TouchOff)
        {
            ApplyHapticsForCondition(condition);
        }

        float sec = Mathf.Max(0f, countdownSeconds);
        _countdownEndRealtime = Time.realtimeSinceStartup + sec;
        isCountingDown = true;
        countdownRemainingSeconds = sec;
        return true;
    }

    private static ScheduleStep[] GetScheduleSteps(EvaluationGroup g)
    {
        if (g == EvaluationGroup.A)
        {
            return new[]
            {
                new ScheduleStep { task = Mode.Accuracy, condition = EvaluationCondition.TouchOff },
                new ScheduleStep { task = Mode.Accuracy, condition = EvaluationCondition.TouchOn },
                new ScheduleStep { task = Mode.Twinkle,  condition = EvaluationCondition.TouchOff },
                new ScheduleStep { task = Mode.Twinkle,  condition = EvaluationCondition.TouchOn },
            };
        }

        return new[]
        {
            new ScheduleStep { task = Mode.Accuracy, condition = EvaluationCondition.TouchOn },
            new ScheduleStep { task = Mode.Accuracy, condition = EvaluationCondition.TouchOff },
            new ScheduleStep { task = Mode.Twinkle,  condition = EvaluationCondition.TouchOn },
            new ScheduleStep { task = Mode.Twinkle,  condition = EvaluationCondition.TouchOff },
        };
    }

    private void TickCountdown()
    {
        if (!isCountingDown)
        {
            countdownRemainingSeconds = 0f;
            return;
        }

        float remain = _countdownEndRealtime - Time.realtimeSinceStartup;
        countdownRemainingSeconds = Mathf.Max(0f, remain);
        if (Time.realtimeSinceStartup < _countdownEndRealtime) return;

        isCountingDown = false;
        countdownRemainingSeconds = 0f;

        if (!_hasPendingStep) return;
        var step = _pendingStep;
        _hasPendingStep = false;
        BeginTaskIntro(step);
    }

    private void CancelCountdown()
    {
        isCountingDown = false;
        countdownRemainingSeconds = 0f;
        _hasPendingStep = false;
    }

    private void BeginTaskIntro(ScheduleStep step)
    {
        CancelTaskIntro();
        condition = step.condition;

        float sec = Mathf.Max(0f, taskIntroSeconds);
        if (sec <= 0f)
        {
            StartStepNow(step);
            return;
        }

        isTaskIntroActive = true;
        taskIntroRemainingSeconds = sec;
        _taskIntroEndRealtime = Time.realtimeSinceStartup + sec;
        _introStep = step;
        _hasIntroStep = true;
    }

    private void TickTaskIntro()
    {
        if (!isTaskIntroActive)
        {
            taskIntroRemainingSeconds = 0f;
            return;
        }

        float remain = _taskIntroEndRealtime - Time.realtimeSinceStartup;
        taskIntroRemainingSeconds = Mathf.Max(0f, remain);
        if (Time.realtimeSinceStartup < _taskIntroEndRealtime) return;

        isTaskIntroActive = false;
        taskIntroRemainingSeconds = 0f;

        if (!_hasIntroStep) return;
        var step = _introStep;
        _hasIntroStep = false;
        StartStepNow(step);
    }

    private void CancelTaskIntro()
    {
        isTaskIntroActive = false;
        taskIntroRemainingSeconds = 0f;
        _hasIntroStep = false;
    }

    private void StartStepNow(ScheduleStep step)
    {
        condition = step.condition;
        if (step.task == Mode.Accuracy) StartAccuracyTask();
        else if (step.task == Mode.Twinkle) StartTwinkleTask();
    }
}
