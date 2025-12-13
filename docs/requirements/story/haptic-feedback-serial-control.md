# ストーリー: 触覚フィードバック用シリアル送信とキャリブレーション

## 目的
MG90S サーボを用いて鍵盤接触の触覚を返すため、シリアル送信とキャリブレーションの基本フローを実装する。

## 受け入れ条件
1. シリアルポートが Open になったタイミングで、全指 0（`A0000B0000C0000D0000E0000`）を 1 回送信できる（安全初期化）。
2. 指が未キャリブレーションの場合、サーボ制御を行わず警告を記録する（例: ログ出力）。
3. キャリブレーション画面で、指ごとに「完全に緩んだ状態（テンションがかからない状態）」のサーボ値（0–1000, `released` 値）を保存できる。保存値はセッション内で再利用可能。
4. キャリブレーションは次の流れで実行できる：
   - 「軽く握ってください」「キャリブレーション中は指の力を抜いてください」を表示し、開始前に 10 秒カウントダウンする
   - 親指から順に（親指→人差し指→中指→薬指→小指）以下を繰り返す：
     - 「○○が動きます」を表示し、開始前に 3 秒カウントダウンする
     - 対象指のみサーボ値を **0 → 1000** に段階的に上げる（他指は `0` 固定）
     - `sensorRaw` が「基準レンジ（±許容値）から外れた」瞬間の **1 ステップ前** のサーボ値を `released` 値として保存する（ノイズ対策で連続回数を持つ）
5. 指ごとに `released` 値を保存した瞬間、その指に対して `released` 値を 1 回シリアル送信できる（触感・配線確認用）。
5. シリアル送信フォーマットは `A####B####C####D####E####`（各 0–1000）で、1 フレーム内で全指分をまとめて送る。
   - チャンネル割り当て（サーボ出力）：A=親指(thumb), B=小指(pinky), C=薬指(ring), D=中指(middle), E=人差し指(index)
   - センサ入力（`sensorRaw`）の並びは従来通り `A=親指, B=人差し指, C=中指, D=薬指, E=小指` とし、サーボ出力のみ上記割り当てへ変換して送信する。
6. COM ポートは設定画面または設定ファイルで指定でき、未指定時は送信しない。
7. `KeyMode` が `Physical` のときのみハプティクスを送信し、`KeyMode.ForShow`（MIDI デモ再生）は送信しない。
8. 送受フォーマットのエンコード/デコードは `SerialPacketCodec`（A/B/C/D/E + 4桁ゼロパディング、0–1000 クランプ、配置: `Assets/Scripts/IO/`）を経由する。
9. 受信系（HandSensorReceiver）は `SerialPacketCodec.TryDecode` で `A####B####C####D####E####` をバリデーションし、不一致行は破棄または警告ログとし、HandCurlTracker へ渡さない（K フラグなどキャリブレーション制御は従来通り考慮）。
10. 受信系（HandSensorReceiver）と送信系（HapticSerialSender）は共通の SerialPortAdapter を介してポートを開閉し、同一 COM を重複オープンしない（配置: `Assets/Scripts/IO/`）。
11. 配置方針：受信系（HandSensorReceiver）は `Assets/Scripts/Hands/Core/`、送信系（HapticSerialSender, キャリブ関連）は `Assets/Scripts/ForceFeedBack/`、共通の SerialPortAdapter / SerialPacketCodec は `Assets/Scripts/IO/`、設定画面 UI は `Assets/Scripts/UI/` に置く。
12. デバッグとして「最後に送った行」「送信状態（Not calibrated / Serial not open / Sent など）」を確認できる（Inspector または UI 表示）。

## メモ / 実装指針
- 係数（先行度合い）、送信レート、デッドゾーンは ScriptableObject などで調整可能にする。
- シリアル送信失敗時はリトライせず、次フレームで再送する。
- SteamVR/OpenGloves の既存入力・当たり判定ロジックは変更しない。
- 将来拡張: 指ごとのオフセット保存、キャリブレーション値の永続化（PlayerPrefs など）。
- 将来拡張（別ストーリーで扱う）:
- 将来拡張（別ストーリーで扱う）:
  - ピアノ鍵盤接触での「ピアノ触覚モード」遷移、底面到達で指ごと送信停止
  - 底面検知は `PianoKey.cs` の角度ベース判定をフックする（物理押下）。`PianoKey.Play()` 経由の MIDI 仮想押下ではハプティクスを発火させない。
  - 通常時の「先行係数（曲げ量よりわずかに先行）」の導入（パラメータ化）
  - キャリブレーション値の永続化（PlayerPrefs 等）
  - 指ごとのオフセット保存
  - ScriptableObject 化（パラメータ調整）
  - SerialPortAdapter の API 例: `TryOpen(string port, int baud)`, `bool IsOpen`, `bool TryWriteLine(string)`, `bool TryReadLatestLine(out string line)`, `void Close()`。
