# 触覚フィードバック（サーボ制御）

## 背景
- ピアノ鍵盤との接触を指の触覚として返すため、外部サーボ（MG90S）を制御する。
- SteamVR/OpenGloves で取得した指の curl を用い、シリアル経由でサーボ目標値を送出する。

## スコープ
- サーボ制御方式：指定 COM ポートへ ASCII シリアル送信（例: `A2301B2391C3431D3313E1234`）
- 指 → チャンネル割り当て（サーボ出力）：A=親指(thumb), B=小指(pinky), C=薬指(ring), D=中指(middle), E=人差し指(index)
- 値のレンジ：0–1000（1000 がサーボ最大回転としてファーム側に設定済み）
- キャリブレーション要件：
  - 指ごとに「完全に緩んだ状態（テンションがかからない状態）」のサーボ値（0–1000）を保存する（以降 `released` 値と呼ぶ）
  - 未キャリブレーション時はサーボを動作させない
  - 設定画面でキャリブレーションを実行可能
  - キャリブレーション方式（現行実装）：
    - ユーザへ「軽く握ってください」「キャリブレーション中は指の力を抜いてください」を案内し、開始前にカウントダウンする（既定 10 秒）
    - 親指→人差し指→中指→薬指→小指の順に、対象指のみサーボ値を **0 → 1000** に段階的に上げる（他指は `0` 固定）
    - 指ごとに `sensorRaw` が「基準レンジ（±許容値）から外れた」瞬間の **1 ステップ前** のサーボ値を `released` 値として保存する（ノイズ対策で連続回数を持つ）
    - 各指の開始前に「○○が動きます」を表示し、短いカウントダウンを行う（既定 3 秒）
- ランタイム動作：
  - シリアルポートが Open になったタイミングで全指 0 を 1 回送信（安全初期化）
  - 通常は指の曲げ量より少し先行した値をサーボ角度とする
  - ピアノ接触で「ピアノ触覚モード」に移行し、底面到達でサーボを停止
  - KeyMode が `ForShow`（MIDI デモ）や未キャリブレーション時はサーボを動作させない

## 非スコープ
- ESP32 ファームウェア変更
- OpenXR へのランタイム切替

## 依存・関連
- 手の curl 取得: `Assets/Scripts/Hands/Core/HandCurlTracker.cs`
- ストーリー: `story/haptic-feedback-serial-control.md`
 - 注意: センサ入力（`sensorRaw`）の並びは従来通り `A=親指, B=人差し指, C=中指, D=薬指, E=小指` とし、**サーボ出力のみ**上記の割り当てへ変換して送信する。

## 実装ディレクトリ方針
- 触覚フィードバック「送信」関連は `Assets/Scripts/ForceFeedBack/` 配下にまとめる（サーボ制御、キャリブレーション、パラメータ管理）。
- シリアル入出力の共通ヘルパ（SerialPortAdapter）は `Assets/Scripts/IO/` に配置する。
- センサ受信コンポーネント（HandSensorReceiver）は入力系として `Assets/Scripts/Hands/Core/` に配置する。
- 既存ハンド／ピアノ側スクリプトには最小限のフック（イベント発火・状態取得）だけを追加し、送信ロジック本体は ForceFeedBack 配下に集約する。
- 設定画面（UI）ロジックは `Assets/Scripts/UI/` に集約する（キャリブレーション操作 UI、送信デバッグ UI など）。

## シリアル通信アーキテクチャ（送受分離）
- 共通ポート管理: `SerialPortAdapter`（仮称）で Open/Close/Write/Read を一元化し、受信系（HandSensorReceiver）と送信系（HapticSerialSender）から共有する。ポート競合を避けるため、同一 COM を開くのはこのアダプタのみとする。配置は `Assets/Scripts/IO/`。
- フォーマット共通化: `SerialPacketCodec`（`Assets/Scripts/IO/`）をヘルパとして持ち、`A####B####C####D####E####` の固定並び・4桁ゼロパディング・0–1000 クランプをエンコード/デコードで共通利用する。
- 受信専用（HandSensorReceiver）は入力系に置き、送信は ForceFeedBack 配下の新規コンポーネントで実装する。ハプティクス送信側に受信ロジックは持たせない。
- 受信系は `SerialPacketCodec.TryDecode` でフォーマットバリデーションを行い、フォーマット不正な行は破棄または警告ログのみで HandCurlTracker へ渡さない。

## 現行実装（最小セット）
- キャリブ状態（セッション内）: `Assets/Scripts/ForceFeedBack/HapticCalibrationState.cs`（`released` 値を保持）
- キャリブ制御（0→1000 スイープ / レンジ外検知）: `Assets/Scripts/ForceFeedBack/HapticGripCalibrationController.cs`
- 送信（ガード/デバッグ付き）: `Assets/Scripts/ForceFeedBack/HapticSerialSender.cs`
- 設定画面 UI（右手/左手切替、Start/Cancel 表示切替、表示テキスト）: `Assets/Scripts/UI/HapticCalibrationUI.cs`
- 送信デバッグ表示（最後の送信行/状態）: `Assets/Scripts/UI/HapticSerialSendDebugUI.cs`

## ピアノ鍵盤スクリプトとの連携（既存コード参照）
- 物理押下・底面判定：`Assets/Scripts/Piano/PianoKey.cs` で transform.eulerAngles.x が 350–359° 付近に入ったときに押下とみなす。`PianoKeyController.PressAngleThreshold`（既定 355°）より下がると底面に近づくため、ハプティクス停止トリガーに利用する。
- 再生モード分岐：`Assets/Scripts/Piano/PianoKeyController.cs` の `KeyMode` が Physical のときのみサーボ送信を許可し、`KeyMode.ForShow`（MIDI 自動再生）では触覚を発火させない。
- MIDI 再生時の鍵盤アニメーションは `PianoKey.Play()` を経由するが、触覚は物理押下（手コライダーによる角度変化）にのみ反応する設計とする。
