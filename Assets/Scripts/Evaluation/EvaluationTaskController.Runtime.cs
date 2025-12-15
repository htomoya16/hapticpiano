using System;
using UnityEngine;

public sealed partial class EvaluationTaskController : MonoBehaviour
{
    private void ClearPendingTaskEnd()
    {
        _isTaskEndPending = false;
        _taskEndEndRealtime = 0f;
        _taskEndAdvanceSchedule = true;
    }

    private void RequestEndAfterDelay(bool advanceSchedule)
    {
        if (_mode == Mode.None && _activeTask == null) return;
        if (_isTaskEndPending) return;

        float delay = Mathf.Max(0f, taskEndDelaySeconds);
        if (delay <= 0f)
        {
            StopCurrentTaskInternal(advanceSchedule);
            return;
        }

        _isTaskEndPending = true;
        _taskEndAdvanceSchedule = advanceSchedule;
        _taskEndEndRealtime = Time.realtimeSinceStartup + delay;
    }

    private void TickPendingTaskEnd()
    {
        if (!_isTaskEndPending) return;
        if (Time.realtimeSinceStartup < _taskEndEndRealtime) return;

        bool advance = _taskEndAdvanceSchedule;
        ClearPendingTaskEnd();
        StopCurrentTaskInternal(advance);
    }

    private void EmitLogMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Debug.Log($"[Evaluation] {message}", this);
    }

    [ContextMenu("Eval/Stop Current Task")]
    public void StopCurrentTask()
    {
        ClearPendingTaskEnd();
        StopCurrentTaskInternal(advanceSchedule: true);
    }

    [ContextMenu("Eval/Abort Current Task")]
    public void AbortCurrentTask()
    {
        ClearPendingTaskEnd();
        StopCurrentTaskInternal(advanceSchedule: false);
    }

    private void StopCurrentTaskInternal(bool advanceSchedule)
    {
        ClearPendingTaskEnd();
        CancelTrainingDemo();
        CancelAccuracyPatternDemo();

        if (isCountingDown && _mode == Mode.None && _activeTask == null)
        {
            CancelCountdown();
            RestoreHapticsIfNeeded();
            return;
        }

        if (isTaskIntroActive && _mode == Mode.None && _activeTask == null)
        {
            CancelTaskIntro();
            RestoreHapticsIfNeeded();
            return;
        }

        bool endedTask = _mode != Mode.None || _activeTask != null;

        if (_mode == Mode.None && _activeTask == null)
        {
            RestoreHapticsIfNeeded();
            return;
        }

        _mode = Mode.None;
        _twinkleNotes = null;
        _twinkleNoteIndex = 0;
        _twinkleTimerTicks = 0f;

        ClearHighlight();
        ClearAllGuideHighlights();

        string endedTaskName = _activeTask != null ? _activeTask.Task : "unknown";
        string endedCondition = _activeTask != null ? _activeTask.Condition : "unknown";
        string endedStartUtc = _activeTask != null ? _activeTask.StartUtcIso : "";
        string endedEvents = _activeTask != null ? _activeTask.EventsPath : "";

        _activeTask?.End();
        _activeTask = null;

        RestoreHapticsIfNeeded();

        if (!string.IsNullOrEmpty(endedTaskName))
        {
            EmitLogMessage($"task end: {endedTaskName} / {endedCondition} (start={endedStartUtc})");
            if (!string.IsNullOrEmpty(endedEvents)) EmitLogMessage($"saved: {endedEvents}");
            if (_session != null) EmitLogMessage($"saved: {_session.SummaryPath}");
        }

        if (!advanceSchedule) return;

        if (endedTask && useGroupSchedule)
        {
            scheduleStepIndex = Mathf.Clamp(scheduleStepIndex + 1, 0, GetScheduleLength());
            if (scheduleStepIndex < GetScheduleLength())
            {
                BeginCountdownToNextScheduledTask();
            }
        }
    }

    private void HighlightTarget(string systemNoteName, float fadeOutSeconds)
    {
        if (piano == null || piano.PianoNotes == null) return;

        if (!piano.PianoNotes.TryGetValue(systemNoteName, out var key) || key == null)
        {
            ClearHighlight();
            return;
        }

        if (fadeOutSeconds > 0f)
        {
            key.StartGuideHighlightFade(guideKeyColour, fadeOutSeconds);
            return;
        }

        if (_highlightedKey == key) return;
        ClearHighlight();

        _highlightedKey = key;
        _highlightedKey.SetGuideHighlight(true, guideKeyColour);
    }

    private void ClearHighlight()
    {
        if (_highlightedKey == null) return;
        _highlightedKey.ClearGuideHighlight();
        _highlightedKey = null;
    }

    private void ClearAllGuideHighlights()
    {
        if (piano == null || piano.PianoNotes == null) return;
        foreach (var kv in piano.PianoNotes)
        {
            if (kv.Value == null) continue;
            kv.Value.ClearGuideHighlight();
        }
        _highlightedKey = null;
    }

    private void PlayMetronomeClick()
    {
        if (metronomeAudioSource == null || metronomeClick == null) return;
        metronomeAudioSource.PlayOneShot(metronomeClick);
    }

    private void EnsureSession()
    {
        if (_session != null) return;
        _session = new EvaluationLogSession(participantId, participantName, group.ToString());
        EmitLogMessage($"ログ保存先: {_session.RunDirectory}");
        EmitLogMessage($"session_meta.csv: {_session.MetaPath}");
        EmitLogMessage($"task_summary.csv: {_session.SummaryPath}");
    }

    public void ResetLogSession()
    {
        StopCurrentTask();
        try { _session?.Dispose(); } catch { }
        _session = null;
    }

    private void ApplyHapticsForCondition(EvaluationCondition c)
    {
        if (hapticSenders == null || hapticSenders.Length == 0) return;

        if (_prevHapticEnableSend == null || _prevHapticEnableSend.Length != hapticSenders.Length)
        {
            _prevHapticEnableSend = new bool[hapticSenders.Length];
        }

        for (int i = 0; i < hapticSenders.Length; i++)
        {
            if (hapticSenders[i] == null) continue;
            _prevHapticEnableSend[i] = hapticSenders[i].enableSend;
            hapticSenders[i].enableSend = (c == EvaluationCondition.TouchOn);
        }

        _hapticsOverridden = true;
    }

    private void RestoreHapticsIfNeeded()
    {
        if (!_hapticsOverridden) return;
        _hapticsOverridden = false;

        if (hapticSenders == null || _prevHapticEnableSend == null) return;
        for (int i = 0; i < hapticSenders.Length; i++)
        {
            if (hapticSenders[i] == null) continue;
            if (i >= _prevHapticEnableSend.Length) continue;
            hapticSenders[i].enableSend = _prevHapticEnableSend[i];
        }
    }

    private void BindKeyEventsIfPossible()
    {
        if (piano == null || piano.PianoNotes == null || piano.PianoNotes.Count == 0) return;
        foreach (var kv in piano.PianoNotes)
        {
            if (kv.Value == null) continue;
            kv.Value.Pressed -= OnPianoKeyPressed;
            kv.Value.Pressed += OnPianoKeyPressed;
        }
    }

    private void UnbindKeyEventsIfPossible()
    {
        if (piano == null || piano.PianoNotes == null || piano.PianoNotes.Count == 0) return;
        foreach (var kv in piano.PianoNotes)
        {
            if (kv.Value == null) continue;
            kv.Value.Pressed -= OnPianoKeyPressed;
        }
    }

    private void OnPianoKeyPressed(string systemNoteName)
    {
        if (_activeTask == null) return;
        if (_isTaskEndPending) return;
        _activeTask.LogPress(ToCanonicalNoteName(systemNoteName ?? ""));
    }

    private static string UtcIsoFromRealtime(float eventRealtime)
    {
        double deltaSeconds = eventRealtime - Time.realtimeSinceStartup;
        return DateTimeOffset.UtcNow.AddSeconds(deltaSeconds).ToString("o");
    }

    private static string ToConditionString(EvaluationCondition c)
    {
        return c == EvaluationCondition.TouchOn ? "touch_on" : "touch_off";
    }

    private static string ToTaskId(Mode task)
    {
        return task == Mode.Accuracy ? "accuracy" : task == Mode.Twinkle ? "twinkle" : "none";
    }

    private string ToCanonicalNoteName(string systemNoteName)
    {
        return ApplyOctaveOffset(systemNoteName, noteOctaveOffset);
    }

    private string ToSystemNoteName(string canonicalNoteName)
    {
        return ApplyOctaveOffset(canonicalNoteName, -noteOctaveOffset);
    }

    private static string ApplyOctaveOffset(string noteName, int octaveDelta)
    {
        if (!TrySplitNoteName(noteName, out char letter, out bool sharp, out int octave)) return noteName ?? "";
        int nextOct = octave + octaveDelta;
        return sharp ? $"{letter}#{nextOct}" : $"{letter}{nextOct}";
    }

    private static bool TrySplitNoteName(string noteName, out char letter, out bool sharp, out int octave)
    {
        letter = default;
        sharp = false;
        octave = 0;

        if (string.IsNullOrWhiteSpace(noteName)) return false;
        letter = noteName[0];
        if (letter < 'A' || letter > 'G') return false;

        int idx = 1;
        if (noteName.Length >= 2 && noteName[1] == '#')
        {
            sharp = true;
            idx = 2;
        }

        if (idx >= noteName.Length) return false;
        return int.TryParse(noteName.Substring(idx), out octave);
    }
}
