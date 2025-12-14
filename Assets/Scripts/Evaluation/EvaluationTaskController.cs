using System;
using System.IO;
using UnityEngine;

public enum EvaluationCondition
{
    TouchOn = 0,
    TouchOff = 1,
}

public sealed class EvaluationTaskController : MonoBehaviour
{
    private enum Mode
    {
        None = 0,
        Accuracy = 1,
        Twinkle = 2,
    }

    [Header("Session")]
    public string participantId = "P01";
    public string participantName = "";
    public EvaluationCondition condition = EvaluationCondition.TouchOn;

    [Header("References")]
    public PianoKeyController piano;
    public MidiPlayer midiPlayer;
    public HapticSerialSender[] hapticSenders;

    [Header("Metronome (optional)")]
    [Range(30f, 240f)]
    public float bpm = 60f;
    public AudioSource metronomeAudioSource;
    public AudioClip metronomeClick;

    [Header("Guide Light")]
    public Color guideKeyColour = new Color(1f, 0.92f, 0.2f, 1f);

    [Header("Accuracy Task")]
    public float accuracyDurationSeconds = 30f;
    public string[] accuracyPattern = new[]
    {
        "C4", "D4", "E4", "F4", "G4", "F4", "E4", "D4", "C4"
    };

    [Header("Twinkle Task")]
    [Tooltip("StreamingAssets/MIDI/<name>.mid の <name> を指定する（拡張子なし）。")]
    public string twinkleMidiFileNameNoExt = "twinkle_twinkle_60bpm_12bars";
    public bool logTwinkleTargets = true;

    [Header("Debug")]
    public bool enableKeyboardShortcuts = true;

    private EvaluationLogSession _session;
    private EvaluationLogTask _activeTask;
    private Mode _mode = Mode.None;

    private PianoKey _highlightedKey;

    // Accuracy state
    private float _taskStartRealtime;
    private float _nextBeatRealtime;
    private int _trialIndex;

    // Twinkle state
    private MidiNote[] _twinkleNotes;
    private int _twinkleNoteIndex;
    private float _twinkleTimerTicks;

    // Haptics override (per task)
    private bool _hapticsOverridden;
    private bool[] _prevHapticEnableSend;

    public bool IsTaskRunning => _mode != Mode.None;
    public string ActiveTaskId => _mode == Mode.Accuracy ? "accuracy" : _mode == Mode.Twinkle ? "twinkle" : "none";
    public bool HasRunAnyTask { get; private set; }

    private void Start()
    {
        if (piano == null) piano = FindObjectOfType<PianoKeyController>();
        if (midiPlayer == null) midiPlayer = FindObjectOfType<MidiPlayer>();
        if (hapticSenders == null || hapticSenders.Length == 0) hapticSenders = FindObjectsOfType<HapticSerialSender>();

        BindKeyEventsIfPossible();
    }

    private void OnDestroy()
    {
        UnbindKeyEventsIfPossible();
        StopCurrentTask();
    }

    private void Update()
    {
        if (enableKeyboardShortcuts)
        {
            if (Input.GetKeyDown(KeyCode.F5)) StartAccuracyTask();
            if (Input.GetKeyDown(KeyCode.F6)) StartTwinkleTask();
            if (Input.GetKeyDown(KeyCode.F7)) PlayTrainingMidiDemoOnce();
            if (Input.GetKeyDown(KeyCode.F8)) StopCurrentTask();
        }

        switch (_mode)
        {
            case Mode.Accuracy:
                TickAccuracy();
                break;
            case Mode.Twinkle:
                TickTwinkle();
                break;
        }
    }

    [ContextMenu("Eval/Play Training MIDI Demo Once")]
    public void PlayTrainingMidiDemoOnce()
    {
        if (midiPlayer == null)
        {
            Debug.LogWarning("[EvaluationTaskController] MidiPlayer is missing.");
            return;
        }

        StopCurrentTask();

        midiPlayer.KeyMode = KeyMode.ForShow;
        midiPlayer.PlaySongByFileName(twinkleMidiFileNameNoExt, speed: 1f, details: "Training Demo", loop: false);
    }

    [ContextMenu("Eval/Start Accuracy Task")]
    public void StartAccuracyTask()
    {
        StopCurrentTask();
        EnsureSession();
        HasRunAnyTask = true;

        if (midiPlayer != null) midiPlayer.KeyMode = KeyMode.Physical;

        ApplyHapticsForCondition(condition);

        _activeTask = _session.BeginTask(ToConditionString(condition), "accuracy");
        _mode = Mode.Accuracy;

        _trialIndex = 0;
        _taskStartRealtime = Time.realtimeSinceStartup;
        _nextBeatRealtime = _taskStartRealtime;

        ClearHighlight();
    }

    [ContextMenu("Eval/Start Twinkle Task")]
    public void StartTwinkleTask()
    {
        StopCurrentTask();
        EnsureSession();
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

        ClearHighlight();

        if (_twinkleNotes == null || _twinkleNotes.Length == 0)
        {
            StopCurrentTask();
        }
    }

    [ContextMenu("Eval/Stop Current Task")]
    public void StopCurrentTask()
    {
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

        _activeTask?.End();
        _activeTask = null;

        RestoreHapticsIfNeeded();
    }

    private void TickAccuracy()
    {
        if (_activeTask == null)
        {
            StopCurrentTask();
            return;
        }

        float elapsed = Time.realtimeSinceStartup - _taskStartRealtime;
        if (elapsed >= Mathf.Max(0.01f, accuracyDurationSeconds))
        {
            StopCurrentTask();
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
    }

    private void FireAccuracyBeat(float beatStartRealtime)
    {
        _trialIndex++;
        string target = GetAccuracyTarget(_trialIndex);
        string beatUtcIso = UtcIsoFromRealtime(beatStartRealtime);

        HighlightTarget(target);
        _activeTask?.LogTrial(_trialIndex, beatUtcIso, target);
        PlayMetronomeClick();
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
            HighlightTarget(note.Note);
            if (logTwinkleTargets) _activeTask.LogTrial(_trialIndex, note.Note);
        }
    }

    private void HighlightTarget(string noteName)
    {
        if (piano == null || piano.PianoNotes == null)
        {
            return;
        }

        if (!piano.PianoNotes.TryGetValue(noteName, out var key) || key == null)
        {
            ClearHighlight();
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

    private void PlayMetronomeClick()
    {
        if (metronomeAudioSource == null || metronomeClick == null) return;
        metronomeAudioSource.PlayOneShot(metronomeClick);
    }

    private string GetAccuracyTarget(int trialIndex)
    {
        if (accuracyPattern == null || accuracyPattern.Length == 0) return "";
        int idx = (trialIndex - 1) % accuracyPattern.Length;
        if (idx < 0) idx += accuracyPattern.Length;
        return accuracyPattern[idx] ?? "";
    }

    private void EnsureSession()
    {
        if (_session != null) return;
        _session = new EvaluationLogSession(participantId, participantName);
        Debug.Log($"[EvaluationTaskController] Log folder: {_session.RunDirectory}", this);
    }

    public void ResetLogSession()
    {
        StopCurrentTask();
        try { _session?.Dispose(); } catch { }
        _session = null;
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

    private void OnPianoKeyPressed(string noteName)
    {
        if (_activeTask == null) return;
        _activeTask.LogPress(noteName ?? "");
    }
}
