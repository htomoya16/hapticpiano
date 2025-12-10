# ストーリー: 触覚フィードバック用シリアル送信とキャリブレーション

## 目的
MG90S サーボを用いて鍵盤接触の触覚を返すため、シリアル送信とキャリブレーションの基本フローを実装する。

## 受け入れ条件
1. シーン再生開始時（Play）に全指 0 を送信する。
2. 指が未キャリブレーションの場合、サーボ制御を行わず警告を記録する（例: ログ出力）。
3. キャリブレーション画面で「指を完全に閉じた状態」のサーボ値を保存できる。保存値はセッション内で再利用可能。
4. 通常時、指の曲げ量を基に計算した目標値を送信し、曲げ量よりわずかに先行する（詳細係数は実装メモで可変にする）。
5. ピアノ鍵盤に接触したフレームで「ピアノ触覚モード」に遷移し、底面に到達したフレームで該当指サーボへの送信を停止する。
6. シリアル送信フォーマットは `A####B####C####D####E####`（各 0–1000）で、1 フレーム内で全指分をまとめて送る。
7. COM ポートは設定画面または設定ファイルで指定でき、未指定時は送信しない。
8. `KeyMode` が `Physical` のときのみハプティクスを送信し、`KeyMode.ForShow`（MIDI デモ再生）は送信しない。
9. `PianoKey` の角度が `PianoKeyController.PressAngleThreshold` 付近（例: 355° 以下）に達した瞬間を「底面到達」とみなし、その指のサーボ送信を停止できる。
10. 送受フォーマットのエンコード/デコードは `SerialPacketCodec`（A/B/C/D/E + 4桁ゼロパディング、0–1000 クランプ、配置: `Assets/Scripts/IO/`）を経由する。
11. 受信系（HandSensorReceiver）は `SerialPacketCodec.TryDecode` で `A####B####C####D####E####` をバリデーションし、不一致行は破棄または警告ログとし、HandCurlTracker へ渡さない（K フラグなどキャリブレーション制御は従来通り考慮）。
12. 受信系（HandSensorReceiver）と送信系（HapticSerialSender）は共通の SerialPortAdapter を介してポートを開閉し、同一 COM を重複オープンしない（配置: `Assets/Scripts/IO/`）。
13. 配置方針：受信系（HandSensorReceiver）は `Assets/Scripts/Hands/Core/`、送信系（HapticSerialSender）は `Assets/Scripts/ForceFeedBack/`、共通の SerialPortAdapter / SerialPacketCodec は `Assets/Scripts/IO/` に置く。

## メモ / 実装指針
- 係数（先行度合い）、送信レート、デッドゾーンは ScriptableObject などで調整可能にする。
- シリアル送信失敗時はリトライせず、次フレームで再送する。
- SteamVR/OpenGloves の既存入力・当たり判定ロジックは変更しない。
- 将来拡張: 指ごとのオフセット保存、キャリブレーション値の永続化（PlayerPrefs など）。
 - 底面検知は `PianoKey.cs` の角度ベース判定をフックする（物理押下）。`PianoKey.Play()` 経由の MIDI 仮想押下ではハプティクスを発火させない。
 - 実装ファイルは原則 `Assets/Scripts/ForceFeedBack/` 以下に配置し、ハンド／ピアノ側はイベントや状態取得のフックのみとする。
 - SerialPortAdapter の API 例: `TryOpen(string port, int baud)`, `bool IsOpen`, `bool TryWriteLine(string)`, `bool TryReadLatestLine(out string line)`, `void Close()`。
