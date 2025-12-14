using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class PianoKey : MonoBehaviour
{
	public List<AudioSource> AudioSources { get; set; }
	public AudioSource CurrentAudioSource { get; set; }
	public PianoKeyController PianoKeyController { get; set; }

	/// <summary>
	/// PianoKeyController から割り当てられるノート名（例: C4, F#5）。
	/// </summary>
	public string NoteName { get; set; }

	/// <summary>
	/// 物理モードで鍵盤が押下判定された瞬間に発火する。
	/// </summary>
	public event Action<string> Pressed;

	public bool Sustain { get; set; }
	public float SustainSeconds { get; set; }
	public Material Material { get; set; }

	private bool _play = false;
	private bool _played = false;
	private float _velocity;
	private float _length;
	private float _speed;
	private Color _colour;
	private Color _originalColour;
	private float _Timer;
	private float _keyAngle = 360f;
	private bool _guideHighlight;
	private Color _guideHighlightColour;
	private bool _guideHighlightFading;
	private float _guideFadeStartRealtime;
	private float _guideFadeDurationSeconds;
	private Color _guideFadeFromColour;

	private Vector3 _position;
	private Vector3 _rotation;

	private Rigidbody _rigidbody;
	private HingeJoint _springJoint;
	private ConstantForce _constantForce;
	private IEnumerator _playCoro;
	private IEnumerator _volumeCoro;

	private List<AudioSource> _toFade = new List<AudioSource>();

	private bool _depression;
	private float _startAngle;

	// Debug
	public bool TestPlay = false;

void Awake()
{
	// 1鍵分のコンポーネント参照を初期化
	AudioSources = new List<AudioSource>();
	AudioSources.Add(GetComponent<AudioSource>());
	CurrentAudioSource = AudioSources[0];

		_rigidbody = GetComponent<Rigidbody>();
		_springJoint = GetComponent<HingeJoint>();
		_constantForce = GetComponent<ConstantForce>();

		_position = transform.position;
		_rotation = transform.eulerAngles;

		Material = GetComponent<MeshRenderer>().material;
		_originalColour = Material.color;
		_guideHighlightColour = new Color(1f, 0.92f, 0.2f, 1f);
	}

	/// <summary>
	/// 連打時に AddComponent<AudioSource>() が走って一瞬詰まるのを防ぐため、AudioSource を事前に確保する。
	/// </summary>
	public void EnsureAudioSourcePool(int minCount)
	{
		minCount = Mathf.Max(1, minCount);

		if (AudioSources == null)
		{
			AudioSources = new List<AudioSource>();
		}

		if (AudioSources.Count == 0)
		{
			var src = GetComponent<AudioSource>();
			if (src != null)
			{
				AudioSources.Add(src);
				CurrentAudioSource = src;
			}
		}

		if (CurrentAudioSource == null && AudioSources.Count > 0)
		{
			CurrentAudioSource = AudioSources[0];
		}

		if (CurrentAudioSource == null) return;

		while (AudioSources.Count < minCount)
		{
			AudioSources.Add(CloneAudioSource());
		}
	}

	// Update is called once per frame
void Update()
{
	Constrain(); // 位置固定とX回転クランプ

	if (_play)
	{
		KeyPlayMechanics(); // MIDI再生時の押下アニメーション
	}

	UpdateGuideHighlightFade();

	if (PianoKeyController.KeyMode == KeyMode.Physical)
	{
		float x = transform.eulerAngles.x;
		if (x < 180f) x += 360f; // 0..180 を 360..540 に補正（キーは 352..360 付近で動く前提）

		float enter = PianoKeyController != null ? PianoKeyController.PhysicalPressEnterAngleX : 359.5f;
		float exit = PianoKeyController != null ? PianoKeyController.PhysicalPressExitAngleX : 359.8f;
		if (exit < enter) exit = enter;

		if (x <= enter && !_played)
			{
				if (CurrentAudioSource.clip)
					StartCoroutine(PlayPressedAudio());

				Pressed?.Invoke(NoteName);
				_played = true;

				if (_toFade.Count > 0)
				{
					FadeList();
				}
			}
			else if (x >= exit && _played)
			{
				FadeAll();
				
				_played = false;
			}
	}
	else if (PianoKeyController.KeyMode == KeyMode.ForShow)
	{
		// デモ用の仮想押下: 時間経過でフェードアウト
		if (_Timer >= 1)
		{
			FadeAll();
		}
			
			if (_toFade.Count > 0)
			{
				FadeList();
			}
		}

		// Debug
		if (TestPlay)
		{
			Play();
			TestPlay = false;
		}
	}

void Constrain()
{
	// 初期位置を固定し、Y/Z回転をロック、X回転だけ許容
	transform.position = _position;
	transform.rotation = Quaternion.Euler(transform.eulerAngles.x, _rotation.y, _rotation.z);

	if (transform.eulerAngles.x > 0 && transform.eulerAngles.x < 90)
	{
		transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, transform.eulerAngles.z);
	}
		if (transform.eulerAngles.x > 90 && transform.eulerAngles.x < 351)
		{
			transform.rotation = Quaternion.Euler(352, transform.eulerAngles.y, transform.eulerAngles.z);
		}
	}

void KeyPlayMechanics()
{
	if (_Timer < 1)
	{
		// スプリング/重力を一時無効化し、トルクで押し込み再現
		_springJoint.useSpring = false;
		_constantForce.enabled = false;
	
		if (transform.eulerAngles.x < 1 || transform.eulerAngles.x > 359.5f)
		{
				_rigidbody.AddTorque(-Vector3.right * _velocity / 1024f);
			}

			if (transform.eulerAngles.x > 1)
			{
				if (PianoKeyController.KeyPressAngleDecay && _depression && transform.eulerAngles.x > PianoKeyController.PressAngleThreshold 
					|| !PianoKeyController.KeyPressAngleDecay && transform.eulerAngles.x < _keyAngle)
				{
					_keyAngle = transform.eulerAngles.x;
				}
				else
				{
					if (transform.eulerAngles.x <= PianoKeyController.PressAngleThreshold)
						_depression = false;
					
					transform.rotation = Quaternion.Euler(_keyAngle, transform.eulerAngles.y, transform.eulerAngles.z);

					if (PianoKeyController.KeyPressAngleDecay && !_depression && transform.eulerAngles.x < 359.5f)
						_keyAngle += Time.deltaTime * PianoKeyController.PressAngleDecay;
				}
			}

			if (PianoKeyController.ShowMIDIChannelColours)
				Material.color = Color.Lerp(_colour, _originalColour, _Timer);
			
			_Timer += Time.deltaTime / _length * _speed;
	}
	else
	{
		ApplyGuideVisual(); // 色を元に戻し（ガイドがあれば維持）、物理を再有効化
		_constantForce.enabled = true;
		_springJoint.useSpring = true;
		_play = false;
	}
}

void FadeAll()
{
	// サスティン設定に応じて全AudioSourceを減衰
	if (_toFade.Count > 0)
		_toFade.RemoveRange(0, _toFade.Count);

	foreach (var audioSource in AudioSources)
	{
			if (audioSource.isPlaying)
			{
				audioSource.volume -= Time.deltaTime / (PianoKeyController.SustainPedalPressed ? PianoKeyController.SustainSeconds : 1f);

				if (audioSource.volume <= 0)
					audioSource.Stop();
			}
		}
	}

void FadeList()
{
	// 最近再生したAudioSourceのみ急速フェード
	for (int i = 0; i < _toFade.Count; i++)
	{
		if (_toFade[i].isPlaying)
		{
			_toFade[i].volume -= Time.deltaTime * 2;

				if (_toFade[i].volume <= 0)
				{
					_toFade[i].volume = 0;
					_toFade[i].Stop();
					_toFade.Remove(_toFade[i]);
					break;
				}
			}
		}
	}

public void Play(float velocity = 10, float length = 1, float speed = 1)
{
	_keyAngle = 360f;
	
	if (_play)
	{
		// 連打時、角度をリセットするか追加トルクを与えるか
		if (PianoKeyController.RepeatedKeyTeleport)
			transform.rotation = Quaternion.Euler(_keyAngle, transform.eulerAngles.y, transform.eulerAngles.z);
		else
			_rigidbody.AddTorque(Vector3.right * 127);
	}
		
		_velocity = velocity;
		_length = length;
		_speed = speed;
		_Timer = 0;
		_play = true;
		_depression = true;

		if (PianoKeyController.KeyMode == KeyMode.ForShow)
			PlayVirtualAudio();
	}

	public void Play(Color colour, float velocity = 10, float length = 1, float speed = 1)
	{
		if (PianoKeyController.ShowMIDIChannelColours)
		{
			_colour = colour;
		}
		
		this.Play(velocity, length, speed);
	}

IEnumerator PlayPressedAudio()
{
	// 物理押下: 再生中なら空いているAudioSourceに切替え
	if (!PianoKeyController.NoMultiAudioSource && CurrentAudioSource.isPlaying)
	{
		bool foundReplacement = false;
		int index = AudioSources.IndexOf(CurrentAudioSource);

			for (int i = 0; i < AudioSources.Count; i++)
			{
				if (i != index && (!AudioSources[i].isPlaying || AudioSources[i].volume <= 0))
				{
					foundReplacement = true;
					CurrentAudioSource = AudioSources[i];
					_toFade.Remove(AudioSources[i]);
					break;
				}
			}
			
			if (!foundReplacement)
			{
				AudioSource newAudioSource = CloneAudioSource();
				AudioSources.Add(newAudioSource);
				CurrentAudioSource = newAudioSource;
			}
			
			_toFade.Add(AudioSources[index]);
		}

		_startAngle = transform.eulerAngles.x;

		yield return new WaitForFixedUpdate();
		yield return new WaitForFixedUpdate();

	if (Mathf.Abs(_startAngle - transform.eulerAngles.x) > 0)
	{
		CurrentAudioSource.volume = Mathf.Lerp(0, 1, Mathf.Clamp((Mathf.Abs(_startAngle - transform.eulerAngles.x) / 2f), 0, 1));
	}

		CurrentAudioSource.Play();
	}

void PlayVirtualAudio()
{
	// デモ再生: 物理角度ではなくベロシティで音量を決定
	if (!PianoKeyController.NoMultiAudioSource && CurrentAudioSource.isPlaying)
	{
		bool foundReplacement = false;
			int index = AudioSources.IndexOf(CurrentAudioSource);

			for (int i = 0; i < AudioSources.Count; i++)
			{
				if (i != index && (!AudioSources[i].isPlaying || AudioSources[i].volume <= 0))
				{
					foundReplacement = true;
					CurrentAudioSource = AudioSources[i];
					_toFade.Remove(AudioSources[i]);
					break;
				}
			}
			
			if (!foundReplacement)
			{
				AudioSource newAudioSource = CloneAudioSource();
				AudioSources.Add(newAudioSource);
				CurrentAudioSource = newAudioSource;
			}
			
			_toFade.Add(AudioSources[index]);
		}

		CurrentAudioSource.volume = _velocity / 127f;

		CurrentAudioSource.Play();
	}

	AudioSource CloneAudioSource()
	{
		AudioSource newAudioSource = gameObject.AddComponent<AudioSource>();
		newAudioSource.volume = CurrentAudioSource.volume;
		newAudioSource.playOnAwake = CurrentAudioSource.playOnAwake;
		newAudioSource.spatialBlend = CurrentAudioSource.spatialBlend;
		newAudioSource.clip = CurrentAudioSource.clip;
		newAudioSource.outputAudioMixerGroup = CurrentAudioSource.outputAudioMixerGroup;

		return newAudioSource;
	}

	public void SetGuideHighlight(bool on, Color colour)
	{
		_guideHighlightFading = false;
		_guideHighlight = on;
		_guideHighlightColour = colour;
		ApplyGuideVisual();
	}

	/// <summary>
	/// ガイド点灯を一定秒でフェードアウトさせる（Accuracy タスク向け）。
	/// </summary>
	public void StartGuideHighlightFade(Color colour, float fadeSeconds)
	{
		_guideHighlight = true;
		_guideHighlightFading = true;
		_guideFadeFromColour = colour;
		_guideHighlightColour = colour;
		_guideFadeStartRealtime = Time.realtimeSinceStartup;
		_guideFadeDurationSeconds = Mathf.Max(0.01f, fadeSeconds);
		ApplyGuideVisual();
	}

	public void ClearGuideHighlight()
	{
		_guideHighlight = false;
		_guideHighlightFading = false;
		ApplyGuideVisual();
	}

	private void UpdateGuideHighlightFade()
	{
		if (!_guideHighlightFading) return;
		if (!_guideHighlight) { _guideHighlightFading = false; return; }
		if (Material == null) return;

		// MIDIのチャンネル色表示中は鍵盤側の色制御を優先
		if (_play && PianoKeyController != null && PianoKeyController.ShowMIDIChannelColours)
		{
			return;
		}

		float t = (Time.realtimeSinceStartup - _guideFadeStartRealtime) / Mathf.Max(0.01f, _guideFadeDurationSeconds);
		if (t >= 1f)
		{
			_guideHighlight = false;
			_guideHighlightFading = false;
			ApplyGuideVisual();
			return;
		}

		_guideHighlightColour = Color.Lerp(_guideFadeFromColour, _originalColour, Mathf.Clamp01(t));
		ApplyGuideVisual();
	}

	private void ApplyGuideVisual()
	{
		if (Material == null) return;
		Material.color = _guideHighlight ? _guideHighlightColour : _originalColour;
	}
}
