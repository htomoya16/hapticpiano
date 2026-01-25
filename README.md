# HAPTICPIANO

グローブ型リアルハプティックデバイスを用いた**仮想空間ピアノ演奏における触覚フィードバック提示システム**

Unity / SteamVR / ESP32 を用いて、XR 空間上のピアノ演奏における  
鍵盤接触感および底付き感を指先への反力として提示する卒業研究用システムである。


## 概要

仮想空間におけるピアノ演奏では、視覚・聴覚提示が中心となり、実演奏において重要な鍵盤接触時の触覚情報が十分に再現されていない。

本研究では、グローブ型ハプティックデバイスを用い、指屈曲量および仮想鍵盤の接触状態に基づいてサーボモータによるワイヤ牽引を制御することで、鍵盤押下過程に整合した触覚フィードバックを提示する。

## 動作動画
https://github.com/user-attachments/assets/6de40469-c0de-404c-8dc8-cb77528468a1

https://github.com/user-attachments/assets/59e44d95-e3a9-42d7-a6c3-a8d4ebdb51c1




## システム概要

![System Overview](docs/presentation/スライド8.PNG)

指の屈曲動作を連続的に取得し、仮想鍵盤の状態（非接触・接触・底部到達）に応じて触覚フィードバックを生成するシステム構成を示す。


## 触覚フィードバック生成機能

![Haptic Feedback Control](docs/presentation/スライド11.PNG)

以下の 3 つの制御モードに基づいてサーボモータを制御し、  
ワイヤ牽引による指先への反力提示を行う。

- **通常追従モード（非接触）**  
  指の自然な屈曲運動に追従し、拘束は与えない
- **ピアノ接触モード（接触）**  
  押下状態に基づき拘束量を算出し、抵抗感を提示
- **底面保持モード（底部到達）**  
  拘束量の更新を停止し、底付き感を安定して保持

---

## 評価

![Evaluation Results](docs/presentation/スライド14.PNG)

- **客観評価**：打鍵精度タスク  
  - オンタイム率
  - 誤鍵率
- **主観評価**：きらきら星演奏タスク  
  - 鍵盤操作の明瞭性
  - 演奏体験の現実感

評価の結果、鍵盤操作の明瞭性および演奏体験の現実感は向上した一方で、打鍵動作の安定性については明確な改善は確認されなかった。


## プレゼン資料について

発表資料の全文は以下にまとめている。

- 全文Markdown: `docs/presentation/hapticpiano_slides.md`
- スライド画像一式: `docs/presentation/`


## ファイル構成

主要なディレクトリのみ抜粋しています。

```
Assets/                     Unity 本体（シーン/スクリプト/プレハブ等）
  Scenes/hapticpiano/        メインシーン
  Scripts/                   実装コード
    Hands/                   手モデル関連（入力/可視化/当たり判定）
      Core/                  HandCurlTracker など主要ロジック
      Colliders/             指のコライダー生成
    ForceFeedBack/           触覚フィードバック制御
      HapticRuntimeFeedbackController.cs
      HapticSerialSender.cs
      HapticGripCalibrationController.cs
    Piano/                   ピアノ鍵盤・接触判定
      PianoKey.cs
      PianoKeyController.cs
      MidiPlayer.cs
      PianoKeySolfegeLabeler.cs
    IO/                      シリアル通信
      SerialPortAdapter.cs
      SerialPacketCodec.cs
    Evaluation/              評価タスク/ログ
      EvaluationTaskController.cs
      EvaluationLogging.cs
    UI/                      設定/キャリブレーション UI
      HapticCalibrationUI.cs
      EvaluationSettingsUI.cs
      SerialSettingUI.cs
docs/
  presentation/              発表スライド一式
Packages/                   Unity パッケージ定義
ProjectSettings/            Unity プロジェクト設定
```
