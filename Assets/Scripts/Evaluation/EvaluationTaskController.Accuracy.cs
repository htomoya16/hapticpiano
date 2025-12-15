using UnityEngine;

public sealed partial class EvaluationTaskController : MonoBehaviour
{
    [ContextMenu("Eval/Start Accuracy Task")]
    public void StartAccuracyTask()
    {
        StopCurrentTask();
        CancelTrainingDemo();
        CancelAccuracyPatternDemo();

        EnsureAccuracyPattern();
        EnsureSession();
        HasParticipantInfoLocked = true;
        HasRunAnyTask = true;

        if (midiPlayer != null) midiPlayer.KeyMode = KeyMode.Physical;
        ApplyHapticsForCondition(condition);

        _activeTask = _session.BeginTask(ToConditionString(condition), "accuracy");
        _mode = Mode.Accuracy;

        if (_activeTask != null)
        {
            EmitLogMessage($"task start: accuracy / {ToConditionString(condition)}");
            EmitLogMessage($"events: {_activeTask.EventsPath}");
        }

        _trialIndex = 0;
        _accuracyPlannedTrials = GetAccuracyPlannedTrials();

        float secondsPerBeat = 60f / Mathf.Max(0.01f, bpm);
        if (_accuracyPlannedTrials > 0)
        {
            accuracyDurationSeconds = _accuracyPlannedTrials * secondsPerBeat;
        }

        _taskStartRealtime = Time.realtimeSinceStartup;
        _nextBeatRealtime = _taskStartRealtime;
        ClearAllGuideHighlights();
    }

    private void TickAccuracy()
    {
        if (_activeTask == null)
        {
            StopCurrentTask();
            return;
        }

        if (_accuracyPlannedTrials > 0 && _trialIndex >= _accuracyPlannedTrials)
        {
            RequestEndAfterDelay(advanceSchedule: true);
            return;
        }

        float elapsed = Time.realtimeSinceStartup - _taskStartRealtime;
        if (elapsed >= Mathf.Max(0.01f, accuracyDurationSeconds))
        {
            RequestEndAfterDelay(advanceSchedule: true);
            return;
        }

        float secondsPerBeat = 60f / Mathf.Max(0.01f, bpm);
        float now = Time.realtimeSinceStartup;
        while (now >= _nextBeatRealtime)
        {
            float beatStartRealtime = _nextBeatRealtime;
            _nextBeatRealtime += secondsPerBeat;
            FireAccuracyBeat(beatStartRealtime);
        }

        if (_accuracyPlannedTrials > 0 && _trialIndex >= _accuracyPlannedTrials)
        {
            RequestEndAfterDelay(advanceSchedule: true);
        }
    }

    private void FireAccuracyBeat(float beatStartRealtime)
    {
        _trialIndex++;

        string canonical = GetAccuracyCanonicalTarget(_trialIndex);
        string systemName = ToSystemNoteName(canonical);
        string beatUtcIso = UtcIsoFromRealtime(beatStartRealtime);

        HighlightTarget(systemName, fadeOutSeconds: accuracyGuideFadeSeconds);
        _activeTask.LogTrial(_trialIndex, beatUtcIso, canonical);
        PlayMetronomeClick();
    }

    private void EnsureAccuracyPattern()
    {
        if (!forceFullScaleAccuracyPattern) return;

        accuracyPattern = new[]
        {
            "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5", "B4", "A4", "G4", "F4", "E4", "D4", "C4"
        };
    }

    private int GetAccuracyPlannedTrials()
    {
        if (accuracyPattern == null || accuracyPattern.Length == 0) return 0;
        if (accuracySetCount <= 0) return 0;

        int len = accuracyPattern.Length;
        if (len <= 1) return len * accuracySetCount;
        if (accuracySetCount == 1) return len;

        return len + (len - 1) * (accuracySetCount - 1);
    }

    private string GetAccuracyCanonicalTarget(int trialIndex)
    {
        if (accuracyPattern == null || accuracyPattern.Length == 0) return "";
        int len = accuracyPattern.Length;

        if (accuracySetCount <= 1 || len <= 1)
        {
            int idx = (trialIndex - 1) % len;
            if (idx < 0) idx += len;
            return accuracyPattern[idx] ?? "";
        }

        int t = Mathf.Max(0, trialIndex - 1); // 0-based
        int firstLen = len;
        int subsequentLen = len - 1;

        if (t < firstLen) return accuracyPattern[t] ?? "";

        t -= firstLen;
        int pos = t % subsequentLen;
        return accuracyPattern[pos + 1] ?? "";
    }
}
