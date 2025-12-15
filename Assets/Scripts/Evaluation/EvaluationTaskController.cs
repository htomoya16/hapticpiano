using System;
using System.Collections;
using System.IO;
using System.Text;
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

[DisallowMultipleComponent]
public sealed partial class EvaluationTaskController : MonoBehaviour
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

    [Header("Setup Guard")]
    [Tooltip("グループ（A/B）をユーザーが明示選択するまで、タスク開始を許可しない。")]
    public bool requireExplicitGroupSelection = true;

    [SerializeField, Tooltip("グループ（A/B）をボタンで選択済みか（実行中に変更不可）。")]
    private bool hasExplicitGroupSelection;

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

    [Tooltip("Accuracy タスクのガイドを『点灯→フェードアウト』させる秒数（0以下ならフェードなし）。")]
    public float accuracyGuideFadeSeconds = 0.7f;

    [Header("Accuracy Task")]
    public float accuracyDurationSeconds = 30f;

    [Tooltip("Accuracy のターゲット系列を『ドレミファソラシドシラソファミレド』に固定する（シーン側で誤って変更されても開始時に戻す）。")]
    public bool forceFullScaleAccuracyPattern = true;

    [Tooltip("ターゲット系列（譜面上の表記）。このプロジェクトでは C4=60 の表記（いわゆる Scientific Pitch）を前提に扱う。")]
    public string[] accuracyPattern = new[]
    {
        // ドレミファソラシドシラソファミレド
        "C4", "D4", "E4", "F4", "G4", "A4", "B4", "C5", "B4", "A4", "G4", "F4", "E4", "D4", "C4"
    };

    [Tooltip("Accuracy の往復セット数。2セット目以降は先頭（ド）を除外して連結する。")]
    public int accuracySetCount = 3;

    [Header("Note Name Mapping (Octave)")]
    [Tooltip("鍵盤側のノート名（NAudio系の表記）→ログ/譜面側の表記へ変換するためのオクターブ補正。\n例: NAudio は C5=60 なので、C5 を C4 としてログに残すなら -1。\n0 の場合は変換しない。")]
    public int noteOctaveOffset = -1;

    [Header("Twinkle Task")]
    [Tooltip("StreamingAssets/MIDI/<name>.mid の <name> を指定する（拡張子なし）。")]
    public string twinkleMidiFileNameNoExt = "twinkle_twinkle_60bpm_12bars";
    public bool logTwinkleTargets = true;

    [Tooltip("きらきら星タスクのガイドを『点灯→フェードアウト』させる秒数（0以下ならフェードなし）。")]
    public float twinkleGuideFadeSeconds = 0.7f;

    [Header("Training Demo")]
    [Tooltip("デモ（きらきら星）をボタン押下後に開始するまでの遅延（秒）。")]
    public float trainingDemoDelaySeconds = 3f;

    [Header("UI Text")]
    [Tooltip("次タスク説明の『表』表示で、タスク列の幅（PadRight）。")]
    public int nextTaskTableTaskColumnWidth = 8;

    [Tooltip("表表示を TMP の <mspace> で等幅化して、見た目のズレを減らす。")]
    public bool nextTaskTableUseMonospaceTag = true;

    [Tooltip("<mspace> の幅（em）。詰まるなら 1.0 前後へ上げる。")]
    public float nextTaskTableMspaceEm = 1.00f;

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

    [Header("Task Intro Countdown")]
    [Tooltip("各タスク開始直前に行う『準備』カウントダウン（秒）。説明表示の猶予として使う。")]
    public float taskIntroSeconds = 5f;

    [Header("Task End Delay")]
    [Tooltip("タスクが規定回数/終端に到達したあと、終了扱いにするまでの遅延（秒）。")]
    public float taskEndDelaySeconds = 3f;

    [SerializeField] private bool isTaskIntroActive;
    [SerializeField] private float taskIntroRemainingSeconds;
    private float _taskIntroEndRealtime;
    private bool _hasIntroStep;
    private ScheduleStep _introStep;

    private EvaluationLogSession _session;
    private EvaluationLogTask _activeTask;
    private Mode _mode = Mode.None;

    private PianoKey _highlightedKey;

    private float _taskStartRealtime;
    private float _nextBeatRealtime;
    private int _trialIndex;
    private int _accuracyPlannedTrials;

    private bool _isTaskEndPending;
    private float _taskEndEndRealtime;
    private bool _taskEndAdvanceSchedule;

    private MidiNote[] _twinkleNotes;
    private int _twinkleNoteIndex;
    private float _twinkleTimerTicks;

    private bool _hapticsOverridden;
    private bool[] _prevHapticEnableSend;

    private Coroutine _trainingDemoCoroutine;
    private int _trainingDemoToken;
    private Coroutine _accuracyDemoCoroutine;
    private int _accuracyDemoToken;

    public bool IsTaskRunning => _mode != Mode.None;
    public string ActiveTaskId => ToTaskId(_mode);
    public bool HasRunAnyTask { get; private set; }
    public bool HasParticipantInfoLocked { get; private set; }
    public bool HasExplicitGroupSelection => hasExplicitGroupSelection || !requireExplicitGroupSelection;

    public bool IsCountdownActive => isCountingDown;
    public float CountdownRemainingSeconds => countdownRemainingSeconds;
    public bool IsTaskIntroActive => isTaskIntroActive;
    public float TaskIntroRemainingSeconds => taskIntroRemainingSeconds;

    public string CurrentOrIntroTaskId
    {
        get
        {
            if (IsTaskRunning) return ActiveTaskId;
            if (isTaskIntroActive && _hasIntroStep) return ToTaskId(_introStep.task);
            return "none";
        }
    }

    public string NextOrIntroTaskId
    {
        get
        {
            if (IsTaskRunning) return ActiveTaskId;
            if (isTaskIntroActive && _hasIntroStep) return ToTaskId(_introStep.task);
            if (isCountingDown && _hasPendingStep) return ToTaskId(_pendingStep.task);
            return "none";
        }
    }

    private struct ScheduleStep
    {
        public Mode task;
        public EvaluationCondition condition;
    }

    private void Start()
    {
        if (piano == null) piano = FindObjectOfType<PianoKeyController>();
        if (midiPlayer == null) midiPlayer = FindObjectOfType<MidiPlayer>();
        if (hapticSenders == null || hapticSenders.Length == 0) hapticSenders = FindObjectsOfType<HapticSerialSender>();

        if (requireExplicitGroupSelection)
        {
            hasExplicitGroupSelection = false;
        }

        EnsureAccuracyPattern();
        BindKeyEventsIfPossible();
    }

    private void OnDestroy()
    {
        UnbindKeyEventsIfPossible();
        StopCurrentTask();
        try { _session?.Dispose(); } catch { }
        _session = null;
    }

    private void Update()
    {
        TickPendingTaskEnd();
        if (_isTaskEndPending) return;

        TickCountdown();
        TickTaskIntro();

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
}
