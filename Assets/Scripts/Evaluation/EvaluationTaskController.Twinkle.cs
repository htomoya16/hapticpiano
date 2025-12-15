using System;
using System.IO;
using UnityEngine;

public sealed partial class EvaluationTaskController : MonoBehaviour
{
    [ContextMenu("Eval/Start Twinkle Task")]
    public void StartTwinkleTask()
    {
        StopCurrentTask();
        CancelTrainingDemo();
        CancelAccuracyPatternDemo();

        EnsureSession();
        HasParticipantInfoLocked = true;
        HasRunAnyTask = true;

        if (midiPlayer != null) midiPlayer.KeyMode = KeyMode.Physical;
        ApplyHapticsForCondition(condition);

        _activeTask = _session.BeginTask(ToConditionString(condition), "twinkle");
        _mode = Mode.Twinkle;

        if (_activeTask != null)
        {
            EmitLogMessage($"task start: twinkle / {ToConditionString(condition)}");
            EmitLogMessage($"events: {_activeTask.EventsPath}");
        }

        _trialIndex = 0;
        _twinkleNoteIndex = 0;
        _twinkleTimerTicks = 0f;

        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, "MIDI", twinkleMidiFileNameNoExt + ".mid");
            var inspector = new MidiFileInspector(path);
            _twinkleNotes = inspector.GetNotes();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EvaluationTaskController] Failed to load twinkle midi: {e.Message}", this);
            _twinkleNotes = null;
        }

        ClearAllGuideHighlights();
        if (_twinkleNotes == null || _twinkleNotes.Length == 0)
        {
            StopCurrentTask();
        }
    }

    private void TickTwinkle()
    {
        if (_activeTask == null || _twinkleNotes == null)
        {
            StopCurrentTask();
            return;
        }

        // きらきら星「演奏」タスクは時間制限なし（終了はユーザー操作で行う）。
        if (_twinkleNoteIndex >= _twinkleNotes.Length) return;

        float ticksPerSecond = Mathf.Max(0.01f, (float)_twinkleNotes[_twinkleNoteIndex].Tempo);
        _twinkleTimerTicks += Time.deltaTime * ticksPerSecond;

        while (_twinkleNoteIndex < _twinkleNotes.Length && _twinkleNotes[_twinkleNoteIndex].StartTime < _twinkleTimerTicks)
        {
            var note = _twinkleNotes[_twinkleNoteIndex];
            _twinkleNoteIndex++;

            _trialIndex++;
            // きらきら星タスクは「ガイド提示なし（光らせない）」運用：自由に演奏してもらう。
            // ただしログ用に target(trial) は記録する（分析時に参照できるようにする）。

            if (logTwinkleTargets)
            {
                _activeTask.LogTrial(_trialIndex, ToCanonicalNoteName(note.Note));
            }
        }

        // MIDI末尾に到達しても自動終了しない（自由演奏を継続）
    }
}
