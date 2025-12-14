using System.Collections;
using UnityEngine;

public sealed partial class EvaluationTaskController : MonoBehaviour
{
    [ContextMenu("Eval/Play Training MIDI Demo Once")]
    public void PlayTrainingMidiDemoOnce()
    {
        if (midiPlayer == null)
        {
            Debug.LogWarning("[EvaluationTaskController] MidiPlayer is missing.", this);
            return;
        }

        StopCurrentTask();
        CancelTrainingDemo();
        CancelAccuracyPatternDemo();

        float delay = Mathf.Max(0f, trainingDemoDelaySeconds);
        if (delay <= 0f)
        {
            StartTrainingDemoNow();
            return;
        }

        _trainingDemoToken++;
        int token = _trainingDemoToken;
        _trainingDemoCoroutine = StartCoroutine(TrainingDemoAfterDelay(token, delay));
    }

    [ContextMenu("Eval/Play Accuracy Pattern Demo Once")]
    public void PlayAccuracyPatternDemoOnce()
    {
        if (piano == null || piano.PianoNotes == null || piano.PianoNotes.Count == 0)
        {
            Debug.LogWarning("[EvaluationTaskController] PianoKeyController is missing or not initialized.", this);
            return;
        }

        StopCurrentTask();
        CancelTrainingDemo();
        CancelAccuracyPatternDemo();
        EnsureAccuracyPattern();

        float delay = Mathf.Max(0f, trainingDemoDelaySeconds);
        _accuracyDemoToken++;
        int token = _accuracyDemoToken;
        _accuracyDemoCoroutine = StartCoroutine(AccuracyPatternDemoAfterDelay(token, delay));
    }

    private IEnumerator TrainingDemoAfterDelay(int token, float delaySeconds)
    {
        float end = Time.realtimeSinceStartup + delaySeconds;
        while (Time.realtimeSinceStartup < end)
        {
            if (token != _trainingDemoToken) yield break;
            yield return null;
        }

        if (token != _trainingDemoToken) yield break;
        StartTrainingDemoNow();
        _trainingDemoCoroutine = null;
    }

    private void StartTrainingDemoNow()
    {
        if (midiPlayer == null) return;
        midiPlayer.KeyMode = KeyMode.ForShow;
        midiPlayer.PlaySongByFileName(twinkleMidiFileNameNoExt, speed: 1f, details: "Training Demo", loop: false);
    }

    private void CancelTrainingDemo()
    {
        _trainingDemoToken++;
        if (_trainingDemoCoroutine != null)
        {
            StopCoroutine(_trainingDemoCoroutine);
            _trainingDemoCoroutine = null;
        }
    }

    private IEnumerator AccuracyPatternDemoAfterDelay(int token, float delaySeconds)
    {
        float end = Time.realtimeSinceStartup + delaySeconds;
        while (Time.realtimeSinceStartup < end)
        {
            if (token != _accuracyDemoToken) yield break;
            yield return null;
        }

        if (token != _accuracyDemoToken) yield break;
        if (piano == null || piano.PianoNotes == null || piano.PianoNotes.Count == 0) yield break;

        var prevKeyMode = midiPlayer != null ? midiPlayer.KeyMode : KeyMode.Physical;
        if (midiPlayer != null) midiPlayer.KeyMode = KeyMode.ForShow;

        float secondsPerBeat = 60f / Mathf.Max(0.01f, bpm);
        float next = Time.realtimeSinceStartup;

        // デモは 1 セット目のみ（pattern 1回）
        for (int i = 0; i < accuracyPattern.Length; i++)
        {
            if (token != _accuracyDemoToken) break;

            string canonical = accuracyPattern[i] ?? "";
            string systemName = ToSystemNoteName(canonical);

            if (piano.PianoNotes.TryGetValue(systemName, out var key) && key != null)
            {
                key.StartGuideHighlightFade(guideKeyColour, Mathf.Max(0.01f, accuracyGuideFadeSeconds));
                key.Play(velocity: 110f, length: 0.35f, speed: 1f);
            }

            PlayMetronomeClick();

            next += secondsPerBeat;
            while (Time.realtimeSinceStartup < next)
            {
                if (token != _accuracyDemoToken) break;
                yield return null;
            }
        }

        if (midiPlayer != null) midiPlayer.KeyMode = prevKeyMode;
        _accuracyDemoCoroutine = null;
    }

    private void CancelAccuracyPatternDemo()
    {
        _accuracyDemoToken++;
        if (_accuracyDemoCoroutine != null)
        {
            StopCoroutine(_accuracyDemoCoroutine);
            _accuracyDemoCoroutine = null;
        }
    }
}

