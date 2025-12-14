using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PianoKeySolfegeLabeler : MonoBehaviour
{
    public enum PlacementMode
    {
        KeyLocal = 0,
        RendererBoundsTop = 1,
    }

    [Header("References")]
    public PianoKeyController piano;

    [Tooltip("ラベル生成先（未設定なら各鍵盤の子に生成）。")]
    public Transform labelsRoot;

    [Header("Label")]
    public bool enableLabels = true;

    [Header("Font (optional)")]
    [Tooltip("未設定ならTMPの既定フォントが使われる。")]
    public TMP_FontAsset fontAsset;

    [Tooltip("fontAsset が未設定のとき、シーンに読み込まれている TMP_FontAsset から名前一致で探す。")]
    public bool autoFindFontAssetByName = true;

    [Tooltip("自動検索するフォント名（例: 'MSGOTHIC 1 SDF'）。")]
    public string fontAssetName = "MSGOTHIC 1 SDF";

    [Tooltip("黒鍵（#）も表示する")]
    public bool includeSharps = false;

    [Header("Filter")]
    [Tooltip("ノート範囲で生成対象を絞る（例: C4〜G4 のみ）")]
    public bool limitToNoteRange = true;

    [Tooltip("生成対象の最小ノート（例: C4）。")]
    public string minNoteName = "C4";

    [Tooltip("生成対象の最大ノート（例: G4）。")]
    public string maxNoteName = "G4";

    [Tooltip("オクターブ番号（例: 4）も表示する")]
    public bool showOctaveNumber = false;

    [Tooltip("シャープ記号の表示（例: ♯）。false なら # を使う")]
    public bool useSharpSymbol = true;

    public Color textColor = Color.black;

    [Header("Placement")]
    public PlacementMode placementMode = PlacementMode.KeyLocal;

    [Tooltip("RendererBoundsTop のとき、鍵盤の見た目上面（bounds）からさらに上方向へ持ち上げる量（m）。")]
    public float boundsWorldUpOffset = 0.002f;

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
        TryResolveFontAsset();
        if (rebuildOnStart) Rebuild();
    }

    [ContextMenu("Rebuild Labels")]
    public void Rebuild()
    {
        Clear();
        if (!enableLabels) return;
        if (piano == null || piano.PianoNotes == null || piano.PianoNotes.Count == 0) return;

        int? minMidi = null;
        int? maxMidi = null;
        if (limitToNoteRange)
        {
            if (TryParseNoteNameToMidi(minNoteName, out int mn)) minMidi = mn;
            if (TryParseNoteNameToMidi(maxNoteName, out int mx)) maxMidi = mx;
            if (minMidi.HasValue && maxMidi.HasValue && maxMidi.Value < minMidi.Value)
            {
                var t = minMidi.Value;
                minMidi = maxMidi.Value;
                maxMidi = t;
            }
        }

        foreach (var kv in piano.PianoNotes)
        {
            string noteName = kv.Key;
            var key = kv.Value;
            if (key == null) continue;

            if (!includeSharps && noteName != null && noteName.Contains("#")) continue;
            if (limitToNoteRange && !IsInMidiRange(noteName, minMidi, maxMidi)) continue;

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
        var go = new GameObject($"Label_{key.NoteName}");

        if (labelsRoot != null)
        {
            go.transform.SetParent(labelsRoot, worldPositionStays: false);

            Vector3 worldPos;
            if (placementMode == PlacementMode.RendererBoundsTop)
            {
                var r = key.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    var b = r.bounds;
                    worldPos = b.center + Vector3.up * (b.extents.y + Mathf.Max(0f, boundsWorldUpOffset));
                }
                else
                {
                    worldPos = key.transform.TransformPoint(localOffset);
                }
            }
            else
            {
                worldPos = key.transform.TransformPoint(localOffset);
            }

            go.transform.position = worldPos;
            go.transform.rotation = key.transform.rotation * Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
        }
        else
        {
            go.transform.SetParent(key.transform, worldPositionStays: false);

            if (placementMode == PlacementMode.RendererBoundsTop)
            {
                var r = key.GetComponentInChildren<Renderer>();
                Vector3 worldPos;
                if (r != null)
                {
                    var b = r.bounds;
                    worldPos = b.center + Vector3.up * (b.extents.y + Mathf.Max(0f, boundsWorldUpOffset));
                }
                else
                {
                    worldPos = key.transform.TransformPoint(localOffset);
                }

                go.transform.localPosition = key.transform.InverseTransformPoint(worldPos);
            }
            else
            {
                go.transform.localPosition = localOffset;
            }

            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
        }

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = textColor;
        tmp.enableWordWrapping = false;

        return tmp;
    }

    private void TryResolveFontAsset()
    {
        if (fontAsset != null) return;
        if (!autoFindFontAssetByName) return;
        if (string.IsNullOrWhiteSpace(fontAssetName)) return;

        // ビルドでは未ロードのフォントは見つからないので、基本は Inspector で割り当て推奨。
        var assets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        for (int i = 0; i < assets.Length; i++)
        {
            var a = assets[i];
            if (a == null) continue;
            if (string.Equals(a.name, fontAssetName, StringComparison.OrdinalIgnoreCase))
            {
                fontAsset = a;
                return;
            }
        }
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

    private bool IsInMidiRange(string noteName, int? minMidi, int? maxMidi)
    {
        if (!TryParseNoteNameToMidi(noteName, out int v)) return false;
        if (minMidi.HasValue && v < minMidi.Value) return false;
        if (maxMidi.HasValue && v > maxMidi.Value) return false;
        return true;
    }

    private static bool TryParseNoteNameToMidi(string noteName, out int midi)
    {
        midi = 0;
        if (string.IsNullOrWhiteSpace(noteName)) return false;

        // 例: C4, C#4, F#3
        char letter = noteName[0];
        int semitoneBase = letter switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -999,
        };
        if (semitoneBase < -100) return false;

        bool sharp = noteName.Length >= 2 && noteName[1] == '#';
        int octaveIndex = sharp ? 2 : 1;
        if (octaveIndex >= noteName.Length) return false;

        if (!int.TryParse(noteName.Substring(octaveIndex), out int octave)) return false;

        // MIDI: C4 = 60（octave 4 は 5番目の C なので +1）
        int semitone = semitoneBase + (sharp ? 1 : 0);
        midi = (octave + 1) * 12 + semitone;
        return true;
    }
}
