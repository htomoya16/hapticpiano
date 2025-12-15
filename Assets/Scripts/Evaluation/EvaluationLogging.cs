using System;
using System.IO;
using System.Text;
using UnityEngine;

public sealed class EvaluationLogSession : IDisposable
{
    private readonly string _participantId;
    private readonly string _participantName;
    private readonly string _group;
    private readonly string _runDirectory;
    private readonly string _summaryPath;
    private readonly string _metaPath;

    public string ParticipantId => _participantId;
    public string ParticipantName => _participantName;
    public string Group => _group;
    public string RunDirectory => _runDirectory;
    public string SummaryPath => _summaryPath;
    public string MetaPath => _metaPath;

    public EvaluationLogSession(string participantId, string participantName, string group)
    {
        _participantId = SanitizeParticipantId(participantId);
        _participantName = participantName ?? "";
        _group = group ?? "";

        string baseDir = Path.Combine(Application.persistentDataPath, "EvaluationLogs", _participantId);
        string runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        _runDirectory = Path.Combine(baseDir, runId);
        Directory.CreateDirectory(_runDirectory);

        _summaryPath = Path.Combine(_runDirectory, "task_summary.csv");
        EnsureHeader(_summaryPath, "start_time,end_time,participant_id,condition,task");

        _metaPath = Path.Combine(_runDirectory, "session_meta.csv");
        EnsureHeader(_metaPath, "created_time,participant_id,participant_name,group");
        AppendSessionMeta();
    }

    public EvaluationLogTask BeginTask(string condition, string task)
    {
        return new EvaluationLogTask(this, condition, task);
    }

    internal void AppendTaskSummary(string startTimeUtcIso, string endTimeUtcIso, string condition, string task)
    {
        using var sw = new StreamWriter(_summaryPath, append: true, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.WriteLine(string.Join(",",
            Csv(startTimeUtcIso),
            Csv(endTimeUtcIso),
            Csv(_participantId),
            Csv(condition),
            Csv(task)));
    }

    public void Dispose()
    {
        // per-task writers are managed by EvaluationLogTask
    }

    internal static string NowUtcIso()
    {
        return DateTimeOffset.UtcNow.ToString("o");
    }

    internal static void EnsureHeader(string path, string headerLine)
    {
        if (File.Exists(path)) return;
        using var sw = new StreamWriter(path, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.WriteLine(headerLine);
    }

    internal static string Csv(string value)
    {
        if (value == null) return "";
        bool needsQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        if (!needsQuote) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private void AppendSessionMeta()
    {
        using var sw = new StreamWriter(_metaPath, append: true, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sw.WriteLine(string.Join(",",
            Csv(NowUtcIso()),
            Csv(_participantId),
            Csv(_participantName),
            Csv(_group)));
    }

    private static string SanitizePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment)) return "unknown";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(segment.Length);
        for (int i = 0; i < segment.Length; i++)
        {
            char c = segment[i];
            bool bad = false;
            for (int j = 0; j < invalid.Length; j++)
            {
                if (c == invalid[j]) { bad = true; break; }
            }
            if (bad) { sb.Append('_'); continue; }
            if (c == '/' || c == '\\') { sb.Append('_'); continue; }
            sb.Append(c);
        }

        string s = sb.ToString().Trim();
        if (string.IsNullOrEmpty(s)) return "unknown";
        if (s == "." || s == "..") return "unknown";
        return s;
    }

    public static string SanitizeParticipantId(string participantId)
    {
        string raw = string.IsNullOrWhiteSpace(participantId) ? "unknown" : participantId.Trim();
        return SanitizePathSegment(raw);
    }
}

public sealed class EvaluationLogTask : IDisposable
{
    private readonly EvaluationLogSession _session;
    private readonly string _condition;
    private readonly string _task;
    private readonly string _startUtcIso;
    private readonly string _taskInstanceId;
    private readonly string _eventsPath;

    private readonly StreamWriter _eventsWriter;
    private bool _ended;
    private int _currentTrialIndex;

    public string Condition => _condition;
    public string Task => _task;
    public string StartUtcIso => _startUtcIso;
    public string TaskInstanceId => _taskInstanceId;
    public string EventsPath => _eventsPath;

    internal EvaluationLogTask(EvaluationLogSession session, string condition, string task)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _condition = string.IsNullOrWhiteSpace(condition) ? "unknown" : condition.Trim();
        _task = string.IsNullOrWhiteSpace(task) ? "unknown" : task.Trim();
        _startUtcIso = EvaluationLogSession.NowUtcIso();
        _taskInstanceId = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss_fff");

        string prefix = $"{_task}_{_condition}_{_taskInstanceId}";
        _eventsPath = Path.Combine(_session.RunDirectory, $"{prefix}_events.csv");

        EvaluationLogSession.EnsureHeader(_eventsPath, "event_time,event_type,trial_index,beat_time,target_key,pressed_key");

        _eventsWriter = new StreamWriter(_eventsPath, append: true, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void LogTrial(int trialIndex, string targetKey)
    {
        LogTrial(trialIndex, EvaluationLogSession.NowUtcIso(), targetKey);
    }

    public void LogTrial(int trialIndex, string beatTimeUtcIso, string targetKey)
    {
        if (_ended) return;
        _currentTrialIndex = Mathf.Max(_currentTrialIndex, trialIndex);

        string eventTime = EvaluationLogSession.NowUtcIso();
        _eventsWriter.WriteLine(string.Join(",",
            EvaluationLogSession.Csv(eventTime),
            "trial",
            trialIndex.ToString(),
            EvaluationLogSession.Csv(beatTimeUtcIso ?? ""),
            EvaluationLogSession.Csv(targetKey ?? ""),
            ""));
        _eventsWriter.Flush();
    }

    public void LogPress(string pressedKey)
    {
        if (_ended) return;
        string t = EvaluationLogSession.NowUtcIso();
        _eventsWriter.WriteLine(string.Join(",",
            EvaluationLogSession.Csv(t),
            "press",
            _currentTrialIndex > 0 ? _currentTrialIndex.ToString() : "",
            "",
            "",
            EvaluationLogSession.Csv(pressedKey ?? "")));
        _eventsWriter.Flush();
    }

    public void End()
    {
        if (_ended) return;
        _ended = true;

        string endUtcIso = EvaluationLogSession.NowUtcIso();
        _session.AppendTaskSummary(_startUtcIso, endUtcIso, _condition, _task);

        Dispose();
    }

    public void Dispose()
    {
        try { _eventsWriter?.Dispose(); } catch { }
    }
}
