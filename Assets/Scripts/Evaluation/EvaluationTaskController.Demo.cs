using System.Collections;
using UnityEngine;

public sealed partial class EvaluationTaskController : MonoBehaviour
{
    private bool _trainingDemoPlaying;
    private KeyMode _trainingDemoPrevKeyMode;
    private Coroutine _trainingDemoPlaybackWatchCoroutine;

    private bool _accuracyDemoPlaying;
    private KeyMode _accuracyDemoPrevKeyMode;

    public bool IsAnyDemoRunning =>
        _trainingDemoCoroutine != null ||
        _trainingDemoPlaybackWatchCoroutine != null ||
        _trainingDemoPlaying ||
        _accuracyDemoCoroutine != null;

    public bool IsTrainingMidiDemoRunning =>
        _trainingDemoCoroutine != null ||
        _trainingDemoPlaybackWatchCoroutine != null ||
        _trainingDemoPlaying;

    public bool IsAccuracyPatternDemoRunning =>
        _accuracyDemoCoroutine != null ||
        _accuracyDemoPlaying;

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
        _trainingDemoPrevKeyMode = midiPlayer.KeyMode;
        midiPlayer.KeyMode = KeyMode.ForShow;
        midiPlayer.PlaySongByFileName(twinkleMidiFileNameNoExt, speed: 1f, details: "Training Demo", loop: false);

        _trainingDemoPlaying = true;

        if (_trainingDemoPlaybackWatchCoroutine != null)
        {
            StopCoroutine(_trainingDemoPlaybackWatchCoroutine);
            _trainingDemoPlaybackWatchCoroutine = null;
        }

        int token = _trainingDemoToken;
        _trainingDemoPlaybackWatchCoroutine = StartCoroutine(TrainingDemoWatchPlayback(token));
    }

    private void CancelTrainingDemo()
    {
        _trainingDemoToken++;
        if (_trainingDemoCoroutine != null)
        {
            StopCoroutine(_trainingDemoCoroutine);
            _trainingDemoCoroutine = null;
        }

        if (_trainingDemoPlaybackWatchCoroutine != null)
        {
            StopCoroutine(_trainingDemoPlaybackWatchCoroutine);
            _trainingDemoPlaybackWatchCoroutine = null;
        }

        if (midiPlayer != null && _trainingDemoPlaying)
        {
            midiPlayer.StopPlayback();
            if (_trainingDemoPrevKeyMode == KeyMode.Physical && piano != null)
            {
                piano.SuppressPhysicalPressForSeconds(piano.SuppressPhysicalPressSecondsAfterForShow);
            }
            midiPlayer.KeyMode = _trainingDemoPrevKeyMode;
        }

        _trainingDemoPlaying = false;
    }

    private IEnumerator TrainingDemoWatchPlayback(int token)
    {
        while (token == _trainingDemoToken && midiPlayer != null && midiPlayer.IsPlaying)
        {
            yield return null;
        }

        if (token != _trainingDemoToken) yield break;

        if (midiPlayer != null)
        {
            if (_trainingDemoPrevKeyMode == KeyMode.Physical && piano != null)
            {
                piano.SuppressPhysicalPressForSeconds(piano.SuppressPhysicalPressSecondsAfterForShow);
            }
            midiPlayer.KeyMode = _trainingDemoPrevKeyMode;
        }

        _trainingDemoPlaying = false;
        _trainingDemoPlaybackWatchCoroutine = null;
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

        if (midiPlayer != null)
        {
            _accuracyDemoPrevKeyMode = midiPlayer.KeyMode;
            midiPlayer.KeyMode = KeyMode.ForShow;
        }
        _accuracyDemoPlaying = true;

        float secondsPerBeat = 60f / Mathf.Max(0.01f, bpm);
        float next = Time.realtimeSinceStartup;

        // デモもタスク同様に「セット数」を反映させる（既定: 3セット）
        int plannedTrials = GetAccuracyPlannedTrials();
        plannedTrials = Mathf.Max(plannedTrials, accuracyPattern != null ? accuracyPattern.Length : 0);

        for (int trial = 1; trial <= plannedTrials; trial++)
        {
            if (token != _accuracyDemoToken) break;

            string canonical = GetAccuracyCanonicalTarget(trial);
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

        if (midiPlayer != null) midiPlayer.KeyMode = _accuracyDemoPrevKeyMode;
        _accuracyDemoPlaying = false;
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

        if (midiPlayer != null && _accuracyDemoPlaying)
        {
            if (_accuracyDemoPrevKeyMode == KeyMode.Physical && piano != null)
            {
                piano.SuppressPhysicalPressForSeconds(piano.SuppressPhysicalPressSecondsAfterForShow);
            }
            midiPlayer.KeyMode = _accuracyDemoPrevKeyMode;
        }
        _accuracyDemoPlaying = false;
    }
}
