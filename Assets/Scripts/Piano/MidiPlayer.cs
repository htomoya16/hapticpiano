using System;
using UnityEngine;
using UnityEngine.Events;

public class MidiPlayer : MonoBehaviour
{
	[Header("References")]
	public PianoKeyController PianoKeyDetector;

	[Header("Properties")]
	public float GlobalSpeed = 1;
	public RepeatType RepeatType;

	public KeyMode KeyMode;
	public bool ShowMIDIChannelColours;
	public Color[] MIDIChannelColours;

	[Header("Ensure Song Name is filled for builds")]
	public MidiSong[] MIDISongs;

	[HideInInspector]
	public MidiNote[] MidiNotes;
	public UnityEvent OnPlayTrack { get; set; }

	MidiFileInspector _midi;

	string _path;
	string[] _keyIndex;
	int _noteIndex = 0;
	int _midiIndex;
	float _timer = 0;
	[SerializeField, HideInInspector]
	bool _preset = false;

void Start ()
{
	// 曲開始時にUIへ曲情報を通知
	OnPlayTrack = new UnityEvent();
	OnPlayTrack.AddListener(delegate{FindObjectOfType<MusicText>().StartSequence(MIDISongs[_midiIndex].Details);});
	
	_midiIndex = 0;

		if (!_preset)
			PlayCurrentMIDI();
		else
		{
#if UNITY_EDITOR
			_path = string.Format("{0}/MIDI/{1}.mid", Application.streamingAssetsPath, MIDISongs[0].MIDIFile.name);
#else
			_path = string.Format("{0}/MIDI/{1}.mid", Application.streamingAssetsPath, MIDISongs[0].SongFileName);
#endif
			_midi = new MidiFileInspector(_path);
			
			OnPlayTrack?.Invoke();
		}
	}

void Update ()
{
	// 全曲無しなら停止
	if (MIDISongs.Length <= 0)
		enabled = false;
	
	if (_midi != null && MidiNotes.Length > 0 && _noteIndex < MidiNotes.Length)
	{
		// 経過時間に合わせて未再生ノートを順次発火
		_timer += Time.deltaTime * GlobalSpeed * (float)MidiNotes[_noteIndex].Tempo;

		while (_noteIndex < MidiNotes.Length && MidiNotes[_noteIndex].StartTime < _timer)
		{
			if (PianoKeyDetector.PianoNotes.ContainsKey(MidiNotes[_noteIndex].Note))
				{
					if (ShowMIDIChannelColours)
					{
						PianoKeyDetector.PianoNotes[MidiNotes[_noteIndex].Note].Play(MIDIChannelColours[MidiNotes[_noteIndex].Channel],
																				MidiNotes[_noteIndex].Velocity, 
																				MidiNotes[_noteIndex].Length, 
																				PianoKeyDetector.MidiPlayer.GlobalSpeed * MIDISongs[_midiIndex].Speed);
					}
					else
						PianoKeyDetector.PianoNotes[MidiNotes[_noteIndex].Note].Play(MidiNotes[_noteIndex].Velocity, 
																				MidiNotes[_noteIndex].Length, 
																				PianoKeyDetector.MidiPlayer.GlobalSpeed * MIDISongs[_midiIndex].Speed);
				}

				_noteIndex++;
			}
		}
	else
	{
		// 曲の終端に達したら次の曲へ
		SetupNextMIDI();
	}
}

	void SetupNextMIDI()
	{
		if (_midiIndex >= MIDISongs.Length - 1)
		{
			if (RepeatType != RepeatType.NoRepeat)
				_midiIndex = 0;
			else
			{
				_midi = null;
				return;
			}
		}
		else
		{
			if (RepeatType != RepeatType.RepeatOne)
				_midiIndex++;
		}

		PlayCurrentMIDI();
	}

public void PlayCurrentMIDI()
{
	_timer = 0;

#if UNITY_EDITOR
		_path = string.Format("{0}/MIDI/{1}.mid", Application.streamingAssetsPath, MIDISongs[_midiIndex].MIDIFile.name);
#else
	_path = string.Format("{0}/MIDI/{1}.mid", Application.streamingAssetsPath, MIDISongs[_midiIndex].SongFileName);
#endif
	// MIDIを読み出し、再生キューを初期化
	_midi = new MidiFileInspector(_path);
	MidiNotes = _midi.GetNotes();
	_noteIndex = 0;

	OnPlayTrack?.Invoke();
	}

	/// <summary>
	/// StreamingAssets/MIDI 配下の .mid を 1 曲だけ再生する（シリアライズ設定は変更しない想定で、ランタイムで差し替える）。
	/// </summary>
	public void PlaySongByFileName(string songFileNameNoExt, float speed = 1f, string details = "", bool loop = false)
	{
		if (string.IsNullOrEmpty(songFileNameNoExt)) return;

		MIDISongs = new MidiSong[]
		{
			new MidiSong
			{
				SongFileName = songFileNameNoExt,
				Speed = speed,
				Details = details ?? ""
			}
		};

		_midiIndex = 0;
		RepeatType = loop ? RepeatType.RepeatLoop : RepeatType.NoRepeat;
		_preset = false;

		PlayCurrentMIDI();
	}

	public void StopPlayback()
	{
		_midi = null;
		MidiNotes = Array.Empty<MidiNote>();
		_noteIndex = 0;
		_timer = 0;
	}

	[ContextMenu("Preset MIDI")]
	void PresetFirstMIDI()
	{
#if UNITY_EDITOR
		_path = string.Format("{0}/MIDI/{1}.mid", Application.streamingAssetsPath, MIDISongs[0].MIDIFile.name);
#else
		_path = string.Format("{0}/MIDI/{1}.mid", Application.streamingAssetsPath, MIDISongs[0].SongFileName);
#endif
		_midi = new MidiFileInspector(_path);
		MidiNotes = _midi.GetNotes();
		
		_preset = true;
	}

	[ContextMenu("Clear MIDI")]
	void ClearPresetMIDI()
	{
		MidiNotes = new MidiNote[0];
		_preset = false;
	}

#if UNITY_EDITOR
	[ContextMenu("MIDI to name")]
	public void MIDIToPlaylist()
	{
		for (int i = 0; i < MIDISongs.Length; i++)
		{
			MIDISongs[i].SongFileName = MIDISongs[i].MIDIFile.name;
		}
	}
#endif
}

public enum RepeatType { NoRepeat, RepeatLoop, RepeatOne }
public enum KeyMode { Physical, ForShow }

[Serializable]
public class MidiSong
{
#if UNITY_EDITOR
	public UnityEngine.Object MIDIFile;
#endif
	public string SongFileName;
	public float Speed = 1;
	[TextArea]
	public string Details;
}
