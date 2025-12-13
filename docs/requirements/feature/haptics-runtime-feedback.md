# 触覚フィードバック（ランタイム制御 / 通常追従・ピアノ接触モード・底面停止）

## 背景
- キャリブレーションで得た各指の `released`（テンションがかからない）サーボ値を基準に、ランタイムで触覚を生成する。
- 通常時は指の `curl01` に追従しつつ「指の少し先」にサーボを置く。
- 鍵盤に触れたら「ピアノ接触モード」に入り、通常より指側にサーボを寄せる。
- 鍵盤が底に到達したら、その指のサーボを停止（保持）する。

## 用語
- `released`：指ごとの「完全に緩んだ状態（テンションがかからない状態）」のサーボ値（0–1000）
- `servoMax`：サーボの最大値（本プロジェクトでは 1000）
- `curl01`：`HandCurlTracker.curl01`（0–1）
- `t`：`released`〜`servoMax` を正規化した 0–1 の制御量

## スコープ
### 1) `released` と `servoMax` の正規化
- 指ごとに `released` と `servoMax(=1000)` を用いて、サーボ制御量 `t` を 0–1 に正規化する。
- `t=0` は `released`（緩み）、`t=1` は `servoMax`（最大）を意味する。

### 2) `curl01` と `t` の対応（反対向き）
- `curl01` と触覚（`t`）は反対向きに対応させる（イメージ：`curl01==0` のとき触覚はその反対）。
- 例：`t = 1 - curl01`（必要に応じてスケール・クランプを入れる）

### 3) 通常（追従 + 指の少し先）
- `curl01` から `t` を求め、`released`〜`servoMax` を線形補間して指に追従する目標を作る。
- 通常時は「一定ギャップ」を維持するため、指に追従して計算した目標から `airGapUnits`（サーボ値）だけ `released` 方向へ離す。
  - 例：`servoTarget = Max(released, Lerp(released, servoMax, t) - airGapUnits)`
  - `tStrength01` は効きの強さ（0–1）

### 4) ピアノ接触モード（指側へ寄せる）
- 鍵盤接触をトリガーに「ピアノ触覚モード」へ移行する。
- ピアノ接触中は `pianoGapUnits` を小さく（0推奨）して、通常より指側へ寄せる。
  - 例：`servoTarget = Max(released, Lerp(released, servoMax, t) - pianoGapUnits)`

### 5) 底面到達で停止
- 鍵盤が底に到達したら、その指のサーボ更新を停止し、以後は一定値で保持する（指ごとに独立）。

## 実装メモ
- 送信は `HapticSerialSender` が担当し、送信レート（Hz）は `maxSendHz` で制限する（過剰送信による遅延/ガタつき対策）。

## 非スコープ
- シリアル送信仕様・キャリブレーション方式（`feature/haptics-serial-calibration.md`）
- ESP32 ファームウェア変更

## 依存・関連
- シリアル/キャリブ: `feature/haptics-serial-calibration.md`
- ピアノ連動の設計メモ: `feature/piano-haptics-integration.md`
- ストーリー: `story/haptics-runtime-feedback-piano-mode.md`
