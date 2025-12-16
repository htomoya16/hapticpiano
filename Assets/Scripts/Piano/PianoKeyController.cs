using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class PianoKeyController : MonoBehaviour
{
	[Header("References")]
	public MidiPlayer MidiPlayer;
	public Transform PianoKeysParent;
	public Transform SustainPedal;
	public AudioClip[] Notes;

	[Header("Properties")]
	public string StartKey = "A";			// If the first key is not "A", change it to the appropriate note.
	public int StartOctave;					// Start Octave can be increased if the piano/keyboard is not full length. 
	public float PedalReleasedAngle;		// Local angle that a pedal is considered to be released, or off.
	public float PedalPressedAngle;			// Local angle that a pedal is considered to be pressed, or on.
	public float SustainSeconds = 5;		// May want to reduce this if there's too many AudioSources being generated per key.
	public float PressAngleThreshold = 355f;// Rate of keys being slowly released.
	public float PressAngleDecay = 1f;		// Rate of keys being slowly released.
	public bool Sort = true;				// Sorts the Notes. If regex is not empty, it will use that to do the sorting.
	public bool NoMultiAudioSource;			// Will prevent duplicates if true, if you need to optimise. Multiple Audio sources are necessary to remove crackling.

	[Header("Physical Press Detection (EulerAngles.x)")]
	[Tooltip("物理押下の判定（0→360補正後の eulerAngles.x）。この値以下になった瞬間に押下扱い。")]
	public float PhysicalPressEnterAngleX = 359.5f;

	[Tooltip("押下状態の解除（0→360補正後の eulerAngles.x）。この値以上に戻ったら次の押下を受け付ける。")]
	public float PhysicalPressExitAngleX = 359.8f;

	[Tooltip("ForShow（デモ）→Physical 切替直後、押下アニメーションによる誤判定を防ぐため物理押下判定を無視する秒数。")]
	public float SuppressPhysicalPressSecondsAfterForShow = 0.35f;

	private float _suppressPhysicalPressUntilRealtime;

	public void SuppressPhysicalPressForSeconds(float seconds)
	{
		_suppressPhysicalPressUntilRealtime = Mathf.Max(
			_suppressPhysicalPressUntilRealtime,
			Time.realtimeSinceStartup + Mathf.Max(0f, seconds));
	}

	public bool IsPhysicalPressSuppressed => Time.realtimeSinceStartup < _suppressPhysicalPressUntilRealtime;

	[Header("AudioSource Pool")]
	[Tooltip("連打時に AddComponent<AudioSource>() が走って遅延するのを防ぐため、各鍵盤に事前に用意する AudioSource 数。")]
	[Min(1)]
	public int PrewarmAudioSourcesPerKey = 2;
	

	[Header("Attributes")]
	public bool SustainPedalPressed = true;	// When enabled, keys will not stop playing immediately after release.
	public bool KeyPressAngleDecay = true;	// When enabled, keys will slowly be released.
	public bool RepeatedKeyTeleport = true;	// When enabled, during midi mode, a note played on a pressed key will force the rotation to reset.
	

	private float _sustainPedalLerp = 1;

	// Should be controlled via MidiPlayer
	public KeyMode KeyMode					
	{
		get
		{
			if (MidiPlayer)
				return MidiPlayer.KeyMode;
			else
				return KeyMode.Physical;
		}
	}

	public bool ShowMIDIChannelColours		
	{
		get
		{
			if (MidiPlayer)
				return MidiPlayer.ShowMIDIChannelColours;
			else
				return false;
		}
	}

	public Color[] MIDIChannelColours					
	{
		get
		{
			if (MidiPlayer)
				return MidiPlayer.MIDIChannelColours;
			else
				return null;
		}
	}

	[Header("Note: Leave regex blank to sort alphabetically")]
    public string Regex;

	public Dictionary<string, PianoKey> PianoNotes = new Dictionary<string, PianoKey>();

	private readonly string[] _keyIndex = new string[12] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

void Awake ()
{
	if (Sort)
	{
		// Note配列を並び替え：可能なら clip.name から MIDI ノート番号（例: "__60-..."）を抽出して数値ソート。
		// それが無理なら、Regex指定時は Regex 抽出値、空なら名前順。
		if (!TrySortNotesByEmbeddedMidi())
		{
			if (Notes == null) Notes = Array.Empty<AudioClip>();

			if (string.IsNullOrEmpty(Regex))
			{
				Notes = Notes.OrderBy(note => note != null ? note.name : "").ToArray();
			}
			else
			{
				try
				{
					Regex sortReg = new Regex(@Regex);
					Notes = Notes.OrderBy(note => note != null ? sortReg.Match(note.name).Value : "").ToArray();
				}
				catch (Exception e)
				{
					Debug.LogWarning($"[PianoKeyController] Invalid sort Regex '{Regex}': {e.Message}. Falling back to clip.name sort.", this);
					Notes = Notes.OrderBy(note => note != null ? note.name : "").ToArray();
				}
			}
		}
	}

	// 可能なら Notes を MIDI ノート番号で引く（Notes 配列の「範囲」がズレていても、含まれている範囲は正しく割り当てられる）
	var clipsByMidi = BuildClipMapByMidi(Notes);
	bool canMapByMidi = clipsByMidi != null && clipsByMidi.Count > 0;
	int availableMinMidi = 0;
	int availableMaxMidi = 0;
	if (canMapByMidi)
	{
		availableMinMidi = clipsByMidi.Keys.Min();
		availableMaxMidi = clipsByMidi.Keys.Max();
	}

	int startKeyIndex = Array.IndexOf(_keyIndex, StartKey);
	if (startKeyIndex < 0) startKeyIndex = 0;

	int keyCountToAssign = GetAssignableKeyCount();
	int startMidi = 0;
	bool useMidiMapping = false;
	if (canMapByMidi && keyCountToAssign > 0)
	{
		// StartOctave が NAudio 系（C5=60）か Scientific（C4=60）かを「Notes 側の MIDI 範囲との一致数」で推定する。
		int startMidiNAudio = StartOctave * 12 + startKeyIndex;
		int startMidiScientific = (StartOctave + 1) * 12 + startKeyIndex;
		int nAudioHits = CountMidiHits(clipsByMidi, startMidiNAudio, keyCountToAssign);
		int scientificHits = CountMidiHits(clipsByMidi, startMidiScientific, keyCountToAssign);

		startMidi = (nAudioHits >= scientificHits) ? startMidiNAudio : startMidiScientific;
		useMidiMapping = true;
	}

	var count = 0;
	int missingMidiClipCount = 0;
	int firstMissingMidi = -1;

		for (int i = 0; i < PianoKeysParent.childCount; i++)
		{
		AudioSource keyAudioSource = PianoKeysParent.GetChild(i).GetComponent<AudioSource>();
		
		if (keyAudioSource)
		{
			// 子のPianoKeyにAudioClipとノート名を割り当てる
			PianoKey pianoKey = PianoKeysParent.GetChild(i).GetComponent<PianoKey>();
			
			if (useMidiMapping)
			{
				int midi = startMidi + count;
				if (clipsByMidi != null && clipsByMidi.TryGetValue(midi, out var clip) && clip != null)
				{
					keyAudioSource.clip = clip;
				}
				else
				{
					// 足りない分は従来のインデックス割り当てへフォールバック（完全に無音になるのを避ける）
					if (Notes != null && count >= 0 && count < Notes.Length)
						keyAudioSource.clip = Notes[count];

					missingMidiClipCount++;
					if (firstMissingMidi < 0) firstMissingMidi = midi;
				}
			}
			else
			{
				if (Notes != null && count >= 0 && count < Notes.Length)
					keyAudioSource.clip = Notes[count];
			}

			string noteName = KeyString(count + startKeyIndex);
			PianoNotes.Add(noteName, pianoKey);
			pianoKey.NoteName = noteName;
			pianoKey.PianoKeyController = this;

			if (!NoMultiAudioSource && PrewarmAudioSourcesPerKey > 1 && pianoKey != null)
				pianoKey.EnsureAudioSourcePool(PrewarmAudioSourcesPerKey);
				
				count++;
			}
		}

	if (useMidiMapping && missingMidiClipCount > 0)
	{
		int expectedEndMidi = startMidi + Mathf.Max(0, count - 1);
		Debug.LogWarning(
			$"[PianoKeyController] AudioClip が見つからない鍵盤が {missingMidiClipCount}/{count} 個あります（例: MIDI {firstMissingMidi}）。" +
			$" Notes 側の MIDI 範囲は {availableMinMidi}..{availableMaxMidi}、鍵盤側の想定は {startMidi}..{expectedEndMidi} です。" +
			$" 全体的に 1 オクターブずれて聞こえる場合、PianoKeyController の Notes 配列に MIDI {startMidi}..{expectedEndMidi} を含めるよう見直してください。",
			this);
	}
}

void Update()
{
	// サスティンペダルの補間（押下/解放角度をスムーズに回転）
	_sustainPedalLerp -= Time.deltaTime * (SustainPedalPressed ? 1 : -1) * 3.5f;
	_sustainPedalLerp = Mathf.Clamp01(_sustainPedalLerp);

		if (PedalPressedAngle > PedalReleasedAngle)
			SustainPedal.localRotation = Quaternion.Lerp(Quaternion.Euler(PedalReleasedAngle, 0, 0), Quaternion.Euler(PedalPressedAngle, 0, 0), _sustainPedalLerp);
		else
			SustainPedal.localRotation = Quaternion.Lerp(Quaternion.Euler(PedalPressedAngle, 0, 0), Quaternion.Euler(PedalReleasedAngle, 0, 0), _sustainPedalLerp);
	}

	string KeyString (int count)
	{
		return _keyIndex[count % 12] + (Mathf.Floor(count / 12) + StartOctave);
	}

	private int GetAssignableKeyCount()
	{
		if (PianoKeysParent == null) return 0;

		int c = 0;
		for (int i = 0; i < PianoKeysParent.childCount; i++)
		{
			if (PianoKeysParent.GetChild(i).GetComponent<AudioSource>() != null) c++;
		}

		return c;
	}

	private static int CountMidiHits(Dictionary<int, AudioClip> clipsByMidi, int startMidi, int keyCount)
	{
		if (clipsByMidi == null || clipsByMidi.Count == 0) return 0;

		int hits = 0;
		for (int i = 0; i < keyCount; i++)
		{
			if (clipsByMidi.ContainsKey(startMidi + i)) hits++;
		}

		return hits;
	}

	private bool TrySortNotesByEmbeddedMidi()
	{
		if (Notes == null || Notes.Length == 0) return false;

		bool any = false;
		for (int i = 0; i < Notes.Length; i++)
		{
			if (Notes[i] == null) continue;
			if (TryExtractEmbeddedMidi(Notes[i].name, out _))
			{
				any = true;
				break;
			}
		}
		if (!any) return false;

		Notes = Notes
			.Select(c =>
			{
				int midi = int.MaxValue;
				if (c != null && TryExtractEmbeddedMidi(c.name, out int m)) midi = m;
				return new { clip = c, midi };
			})
			.OrderBy(x => x.midi)
			.ThenBy(x => x.clip != null ? x.clip.name : "")
			.Select(x => x.clip)
			.ToArray();

		return true;
	}

	private static Dictionary<int, AudioClip> BuildClipMapByMidi(AudioClip[] notes)
	{
		var dict = new Dictionary<int, AudioClip>();
		if (notes == null) return dict;

		for (int i = 0; i < notes.Length; i++)
		{
			var clip = notes[i];
			if (clip == null) continue;
			if (!TryExtractEmbeddedMidi(clip.name, out int midi)) continue;

			// 重複がある場合は先に見つかった方を優先（シーン/配列設定の意図を尊重）
			if (!dict.ContainsKey(midi))
				dict.Add(midi, clip);
		}

		return dict;
	}

	private static bool TryExtractEmbeddedMidi(string clipName, out int midi)
	{
		midi = 0;
		if (string.IsNullOrEmpty(clipName)) return false;

		// 例: "277089__beskhu__21-a0" の末尾 "__21-" を拾う
		int idx = clipName.LastIndexOf("__", StringComparison.Ordinal);
		if (idx < 0) return false;
		idx += 2;

		int end = idx;
		while (end < clipName.Length && char.IsDigit(clipName[end])) end++;
		if (end == idx) return false;
		if (end >= clipName.Length || clipName[end] != '-') return false;

		if (!int.TryParse(clipName.Substring(idx, end - idx), out midi)) return false;
		return midi >= 0 && midi <= 127;
	}
}
