using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PianoKeySolfegeLabeler : MonoBehaviour
{
    [Header("References")]
    public PianoKeyController piano;

    [Tooltip("ラベル生成先（未設定なら各鍵盤の子に生成）。")]
    public Transform labelsRoot;

    [Header("Label")]
    public bool enableLabels = true;

    [Tooltip("黒鍵（#）も表示する")]
    public bool includeSharps = true;

    [Tooltip("オクターブ番号（例: 4）も表示する")]
    public bool showOctaveNumber = false;

    [Tooltip("シャープ記号の表示（例: ♯）。false なら # を使う")]
    public bool useSharpSymbol = true;

    public Color textColor = Color.black;

    [Tooltip("各鍵盤ローカル座標でのオフセット")]
    public Vector3 localOffset = new Vector3(0f, 0.012f, 0.0f);

    [Tooltip("各鍵盤ローカル回転（度）")]
    public Vector3 localEuler = new Vector3(-90f, 0f, 0f);

    [Tooltip("各鍵盤ローカルスケール")]
    public Vector3 localScale = new Vector3(0.02f, 0.02f, 0.02f);

    public float fontSize = 2.0f;
    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;

    [Header("Debug")]
    public bool rebuildOnStart = true;

    private readonly Dictionary<PianoKey, TMP_Text> _labels = new Dictionary<PianoKey, TMP_Text>();

    private void Start()
    {
        if (piano == null) piano = FindObjectOfType<PianoKeyController>();
        if (rebuildOnStart) Rebuild();
    }

    [ContextMenu("Rebuild Labels")]
    public void Rebuild()
    {
        Clear();
        if (!enableLabels) return;
        if (piano == null || piano.PianoNotes == null || piano.PianoNotes.Count == 0) return;

        foreach (var kv in piano.PianoNotes)
        {
            string noteName = kv.Key;
            var key = kv.Value;
            if (key == null) continue;

            if (!includeSharps && noteName != null && noteName.Contains("#")) continue;

            string text = ToSolfege(noteName);
            if (string.IsNullOrEmpty(text)) continue;

            var label = CreateLabelForKey(key, text);
            if (label != null) _labels[key] = label;
        }
    }

    [ContextMenu("Clear Labels")]
    public void Clear()
    {
        foreach (var kv in _labels)
        {
            if (kv.Value == null) continue;
            if (Application.isPlaying) Destroy(kv.Value.gameObject);
            else DestroyImmediate(kv.Value.gameObject);
        }
        _labels.Clear();
    }

    private TMP_Text CreateLabelForKey(PianoKey key, string text)
    {
        Transform parent = labelsRoot != null ? labelsRoot : key.transform;
        var go = new GameObject($"Label_{key.NoteName}");
        go.transform.SetParent(parent, worldPositionStays: false);

        if (labelsRoot != null)
        {
            go.transform.position = key.transform.TransformPoint(localOffset);
            go.transform.rotation = key.transform.rotation * Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
        }
        else
        {
            go.transform.localPosition = localOffset;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
        }

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = textColor;
        tmp.enableWordWrapping = false;

        return tmp;
    }

    private string ToSolfege(string noteName)
    {
        if (string.IsNullOrEmpty(noteName)) return null;

        // 例: C4, C#4, F#3
        char letter = noteName[0];
        bool sharp = noteName.Length >= 2 && noteName[1] == '#';
        int octave = -1;

        int octaveIndex = sharp ? 2 : 1;
        if (octaveIndex < noteName.Length)
        {
            if (int.TryParse(noteName.Substring(octaveIndex), out int o)) octave = o;
        }

        string baseSolfege = letter switch
        {
            'C' => "ド",
            'D' => "レ",
            'E' => "ミ",
            'F' => "ファ",
            'G' => "ソ",
            'A' => "ラ",
            'B' => "シ",
            _ => null,
        };
        if (baseSolfege == null) return null;

        string sharpSuffix = "";
        if (sharp)
        {
            sharpSuffix = useSharpSymbol ? "♯" : "#";
        }

        string octaveSuffix = (showOctaveNumber && octave >= 0) ? octave.ToString() : "";
        return baseSolfege + sharpSuffix + octaveSuffix;
    }
}

