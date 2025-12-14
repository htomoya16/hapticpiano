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

        if (_twinkleNoteIndex >= _twinkleNotes.Length)
        {
            StopCurrentTask();
            return;
        }

        float ticksPerSecond = Mathf.Max(0.01f, (float)_twinkleNotes[_twinkleNoteIndex].Tempo);
        _twinkleTimerTicks += Time.deltaTime * ticksPerSecond;

        while (_twinkleNoteIndex < _twinkleNotes.Length && _twinkleNotes[_twinkleNoteIndex].StartTime < _twinkleTimerTicks)
        {
            var note = _twinkleNotes[_twinkleNoteIndex];
            _twinkleNoteIndex++;

            _trialIndex++;
            HighlightTarget(note.Note, fadeOutSeconds: 0f);

            if (logTwinkleTargets)
            {
                _activeTask.LogTrial(_trialIndex, ToCanonicalNoteName(note.Note));
            }
        }
    }
}

