using System;
using System.Collections;
using System.IO;
using UnityEngine;

public enum EvaluationCondition
{
    TouchOn = 0,
    TouchOff = 1,
}

public enum EvaluationGroup
{
    A = 0,
    B = 1,
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

    [Tooltip("A/B は『条件の割り当て順（カウンタバランス）』を表す。")]
    public EvaluationGroup group = EvaluationGroup.A;
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

    [Header("Training Demo")]
    [Tooltip("デモ（きらきら星）をボタン押下後に開始するまでの遅延（秒）。")]
    public float trainingDemoDelaySeconds = 3f;

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

    private Coroutine _trainingDemoCoroutine;
    private int _trainingDemoToken;

    public bool IsTaskRunning => _mode != Mode.None;
    public string ActiveTaskId => _mode == Mode.Accuracy ? "accuracy" : _mode == Mode.Twinkle ? "twinkle" : "none";
    public bool HasRunAnyTask { get; private set; }
    public bool HasParticipantInfoLocked { get; private set; }

    [Header("Schedule (optional)")]
    [Tooltip("グループ（A/B）に基づく手順（Accuracy→Twinkle）で進めるための補助。")]
    public bool useGroupSchedule = true;

    [SerializeField, Tooltip("グループ手順の現在ステップ（0始まり）。")]
    private int scheduleStepIndex = 0;

    [Header("Countdown")]
    [Tooltip("各タスク開始前（初回含む）の待機秒数。")]
    public float countdownSeconds = 20f;

    [SerializeField] private bool isCountingDown;
    [SerializeField] private float countdownRemainingSeconds;
    private float _countdownEndRealtime;
    private bool _hasPendingStep;
    private ScheduleStep _pendingStep;

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

        TickCountdown();

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
        CancelTrainingDemo();

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

    [ContextMenu("Eval/Start Accuracy Task")]
    public void StartAccuracyTask()
    {
        StopCurrentTask();
        CancelTrainingDemo();
        EnsureSession();
        HasParticipantInfoLocked = true;
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
        CancelTrainingDemo();
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

        ClearHighlight();

        if (_twinkleNotes == null || _twinkleNotes.Length == 0)
        {
            StopCurrentTask();
        }
    }

    [ContextMenu("Eval/Stop Current Task")]
    public void StopCurrentTask()
    {
        CancelTrainingDemo();

        // カウントダウン中ならキャンセルする（手順は進めない）
        if (isCountingDown && _mode == Mode.None && _activeTask == null)
        {
            CancelCountdown();
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

        _activeTask?.End();
        _activeTask = null;

        RestoreHapticsIfNeeded();

        // タスクが終わったら手順を進める（手動 Stop も含む）。
        if (endedTask && useGroupSchedule)
        {
            scheduleStepIndex = Mathf.Clamp(scheduleStepIndex + 1, 0, GetScheduleLength());

            // 次が残っているなら自動でカウントダウン開始
            if (scheduleStepIndex < GetScheduleLength())
            {
                BeginCountdownToNextScheduledTask();
            }
        }
    }

    public void ResetSchedule()
    {
        scheduleStepIndex = 0;
    }

    public int GetScheduleIndex() => scheduleStepIndex;

    public int GetScheduleLength() => GetScheduleSteps(group).Length;

    public bool TryStartNextScheduledTask()
    {
        if (!useGroupSchedule) return false;
        if (IsTaskRunning) return false;

        var steps = GetScheduleSteps(group);
        if (scheduleStepIndex < 0) scheduleStepIndex = 0;
        if (scheduleStepIndex >= steps.Length) return false;

        var step = steps[scheduleStepIndex];
        condition = step.condition;

        if (step.task == Mode.Accuracy)
        {
            StartAccuracyTask();
            return true;
        }

        if (step.task == Mode.Twinkle)
        {
            StartTwinkleTask();
            return true;
        }

        return false;
    }

    public bool BeginCountdownToNextScheduledTask()
    {
        if (!useGroupSchedule) return false;
        if (IsTaskRunning) return false;
        if (isCountingDown) return false;

        var steps = GetScheduleSteps(group);
        if (scheduleStepIndex < 0) scheduleStepIndex = 0;
        if (scheduleStepIndex >= steps.Length) return false;

        HasParticipantInfoLocked = true;

        _pendingStep = steps[scheduleStepIndex];
        _hasPendingStep = true;
        condition = _pendingStep.condition; // 表示上も次の条件に合わせる

        float sec = Mathf.Max(0f, countdownSeconds);
        _countdownEndRealtime = Time.realtimeSinceStartup + sec;
        isCountingDown = true;
        countdownRemainingSeconds = sec;

        return true;
    }

    public bool IsCountdownActive => isCountingDown;
    public float CountdownRemainingSeconds => countdownRemainingSeconds;

    public string GetScheduleDescription()
    {
        var steps = GetScheduleSteps(group);
        if (steps == null || steps.Length == 0) return "(no schedule)";

        string s = "";
        for (int i = 0; i < steps.Length; i++)
        {
            if (i > 0) s += " → ";
            s += $"{ToTaskId(steps[i].task)}({ToConditionString(steps[i].condition)})";
        }
        return s;
    }

    public string GetNextScheduleStepLabel()
    {
        var steps = GetScheduleSteps(group);
        if (steps == null || steps.Length == 0) return "(none)";
        if (scheduleStepIndex < 0) return "(none)";
        if (scheduleStepIndex >= steps.Length) return "(done)";

        var step = steps[scheduleStepIndex];
        return $"step {scheduleStepIndex + 1}/{steps.Length}: {ToTaskId(step.task)}({ToConditionString(step.condition)})";
    }

    public string GetNextScheduleStepDescriptionJa()
    {
        var steps = GetScheduleSteps(group);
        if (steps == null || steps.Length == 0) return "次: なし";
        if (scheduleStepIndex < 0) return "次: なし";
        if (scheduleStepIndex >= steps.Length) return "完了";

        var step = steps[scheduleStepIndex];
        string taskJa = step.task == Mode.Accuracy ? "打鍵精度（Accuracy）" : "きらきら星（Twinkle）";
        string condJa = step.condition == EvaluationCondition.TouchOn ? "触覚あり(touch_on)" : "触覚なし(touch_off)";
        return $"次({scheduleStepIndex + 1}/{steps.Length}): {taskJa} / {condJa}";
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
        _session = new EvaluationLogSession(participantId, participantName, group.ToString());
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

    private static string ToTaskId(Mode task)
    {
        return task == Mode.Accuracy ? "accuracy" : task == Mode.Twinkle ? "twinkle" : "none";
    }

    private struct ScheduleStep
    {
        public Mode task;
        public EvaluationCondition condition;
    }

    private static ScheduleStep[] GetScheduleSteps(EvaluationGroup g)
    {
        // ユーザー指定の手順:
        // - A: accuracy off → accuracy on → twinkle on → twinkle off
        // - B: 逆
        if (g == EvaluationGroup.A)
        {
            return new ScheduleStep[]
            {
                new ScheduleStep { task = Mode.Accuracy, condition = EvaluationCondition.TouchOff },
                new ScheduleStep { task = Mode.Accuracy, condition = EvaluationCondition.TouchOn },
                new ScheduleStep { task = Mode.Twinkle,  condition = EvaluationCondition.TouchOn },
                new ScheduleStep { task = Mode.Twinkle,  condition = EvaluationCondition.TouchOff },
            };
        }

        return new ScheduleStep[]
        {
            new ScheduleStep { task = Mode.Accuracy, condition = EvaluationCondition.TouchOn },
            new ScheduleStep { task = Mode.Accuracy, condition = EvaluationCondition.TouchOff },
            new ScheduleStep { task = Mode.Twinkle,  condition = EvaluationCondition.TouchOff },
            new ScheduleStep { task = Mode.Twinkle,  condition = EvaluationCondition.TouchOn },
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

        // 開始
        condition = step.condition;
        if (step.task == Mode.Accuracy) StartAccuracyTask();
        else if (step.task == Mode.Twinkle) StartTwinkleTask();
    }

    private void CancelCountdown()
    {
        isCountingDown = false;
        countdownRemainingSeconds = 0f;
        _hasPendingStep = false;
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
