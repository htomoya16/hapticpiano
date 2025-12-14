# 卒論評価（触覚あり/なしの比較実験）

## 背景
- 本研究では、**触覚フィードバックの有無**が XR 仮想ピアノ演奏に与える影響を評価する。
- VR 実験では再現性（手順・刺激・ログ形式）が重要になるため、Unity 側で「タスク提示」と「ログ収集」を最小構成で提供する。

## 評価目的
- 触覚フィードバックの有無が
  - **打鍵精度（客観）**
  - **主観的体験（主観）**
  に与える影響を明らかにする。

## 実験条件（2条件）
- 条件：`touch_off`（触覚なし） / `touch_on`（触覚あり）
- 被験者内比較、順序はカウンターバランスする。

### グループ（順序割当）
本研究では「A/B グループ」は **触覚の有無そのもの**ではなく、
**実施順序（カウンターバランス）**を表す。

- Aグループ：
  - Accuracy：`touch_off → touch_on`
  - Twinkle：`touch_on → touch_off`
- Bグループ（逆順）：
  - Accuracy：`touch_on → touch_off`
  - Twinkle：`touch_off → touch_on`

## タスク構成
評価は「主要評価（客観）＋副次評価（主観）」で構成する。

### 1) 主要評価：打鍵精度タスク（Accuracy）
- テンポ：**60 BPM**（メトロノーム）
- 1拍＝1試行（trial）として扱う
- ターゲット系列（反復）
  - `C4 → D4 → E4 → F4 → G4 → F4 → E4 → D4 → C4`
- 各拍で **正解鍵を光らせる**（ガイド提示）
- ログを取得する

### 2) 副次評価：演奏タスク（Twinkle）
- きらきら星（Twinkle Little Star）
- ガイド（光る鍵盤）を提示する
- 余裕があればログも取得する（ターゲット提示＋押下イベント）

### 事前練習（トレーニング）
- 本評価の学習効果を切り離すためのフェーズ。
- **きらきら星は練習に含めない**。
- 代わりに「ガイドに従って演奏するイメージ」を掴むため、`MidiPlayer` で **デモ再生を1回**流す。

## タスク間インターバル（VR表示）
- 各タスク開始前（初回含む）に **20秒** のインターバルを設ける。
- インターバル中は VR 内に「次のタスク説明」と「開始までのカウントダウン」を表示する。

## ログ設計（CSV）
ログは `persistentDataPath` 配下へ保存し、解析にそのまま使える CSV とする。

1) タスク単位（1行）
- `start_time,end_time,participant_id,condition,task`

2) 正解側（trial/拍ごと）
- `trial_index,beat_time,target_key`

3) 実際の入力側（押下イベント）
- `press_time,pressed_key`

補足：被験者の「表示名」を入力した場合は、セッション単位のメタ情報として `session_meta.csv` に保存する（解析の主キーは `participant_id` を維持する）。

## 既存実装（参照）
- タスク実行・ガイド・ログ：`Assets/Scripts/Evaluation/EvaluationTaskController.cs`
- CSV ロガー：`Assets/Scripts/Evaluation/EvaluationLogging.cs`
- 打鍵イベント（物理押下のみ）：`Assets/Scripts/Piano/PianoKey.cs`
- きらきら星 MIDI：`Assets/StreamingAssets/MIDI/twinkle_twinkle_60bpm_12bars.mid`
