# 15_piano_keypress_facts (facts)

卒論 3.6「鍵盤押下検出機能」を書くために、現行実装（Unity/C#）の事実情報を根拠（ファイルパス:行）付きで整理する。

## 0. 用語（この節での「押下」）
- **鍵盤押下（physical press）**: 鍵盤オブジェクト（`PianoKey`）の **X軸回転角**が閾値を跨いだことによる押下判定（音の発火・イベント）。
- **指の接触（touch/bottom）**: 指コライダと鍵盤コライダの衝突から「どの指がどの鍵に触れているか」を推定する別系統（触覚制御等で利用）。
  - 接触推定は `PianoFingerContactRegistry` が担当し、押下判定そのもの（音の発火）は `PianoKey` の角度判定で行う。

---

## 1. 鍵盤押下検出（Physicalモード）
### 1.1 判定に用いる量（eulerAngles.x の補正）
- `PianoKey.Update()` 内で `KeyMode == Physical` のとき、鍵盤の `transform.eulerAngles.x` を取り出して判定する。  
  `Assets/Scripts/Piano/PianoKey.cs:125`
- 角度が 0..180 側に回り込んだ場合の誤判定を避けるため、`x < 180` のとき `x += 360` する（360..540 に補正）。  
  `Assets/Scripts/Piano/PianoKey.cs:127`〜`129`

### 1.2 閾値（ヒステリシス）
- 押下（Enter）閾値: `PhysicalPressEnterAngleX`（既定 359.5）  
  `Assets/Scripts/Piano/PianoKeyController.cs:28`
- 解放（Exit）閾値: `PhysicalPressExitAngleX`（既定 359.8）  
  `Assets/Scripts/Piano/PianoKeyController.cs:31`
- `PianoKey` は毎フレーム `enter/exit` を `PianoKeyController` から参照し、`exit < enter` の場合は `exit = enter` に補正する。  
  `Assets/Scripts/Piano/PianoKey.cs:130`〜`133`
- シーン実体（`hapticpiano.unity`）でも Enter/Exit は `359.5/359.8` が設定されている。  
  `Assets/Scenes/hapticpiano/hapticpiano.unity:3255`〜`3256`

### 1.3 押下イベントの発火条件（1回のみ）
- `x <= enter` かつ未押下（`_played==false`）の瞬間に押下扱いとなり、音再生と `Pressed` イベントを1回発火する。  
  `Assets/Scripts/Piano/PianoKey.cs:143`〜`150`
- `Pressed` は `event Action<string> Pressed` として定義され、引数は `NoteName`（例: C4 等）である。  
  `Assets/Scripts/Piano/PianoKey.cs:20`, `Assets/Scripts/Piano/PianoKey.cs:148`
- `Pressed` イベントは評価タスク側で購読されている（押下ログ/タスク判定に利用可能）。  
  `Assets/Scripts/Evaluation/EvaluationTaskController.Runtime.cs:318`

### 1.4 解放条件
- `x >= exit` かつ押下中（`_played==true`）の瞬間に解放扱いになり、フェード等を行って `_played=false` に戻す。  
  `Assets/Scripts/Piano/PianoKey.cs:156`〜`161`

---

## 2. 誤判定抑制（ForShow→Physical切替直後）
- デモ（ForShow）から Physical へ戻した直後に押下アニメーションが残っていると、物理押下として誤発火し得るため、一定時間だけ物理押下判定を抑制する仕組みがある。  
  `Assets/Scripts/Piano/PianoKey.cs:134`〜`141`
- 抑制フラグは `PianoKeyController.IsPhysicalPressSuppressed`（リアルタイム秒で判定）で提供される。  
  `Assets/Scripts/Piano/PianoKeyController.cs:45`
- 抑制中はイベント発火を止めつつ、角度に応じて `_played` 状態だけ同期し、抑制解除後の誤発火も防ぐ。  
  `Assets/Scripts/Piano/PianoKey.cs:136`〜`140`

---

## 3. 鍵盤の回転・拘束（押下判定が安定するための前提）
- `PianoKey.Update()` 冒頭で `Constrain()` を呼び、鍵盤の位置固定とX回転クランプを行う。  
  `Assets/Scripts/Piano/PianoKey.cs:116`
- `Constrain()` は
  - 位置を初期位置へ固定し（`transform.position = _position`）
  - Y/Z回転を初期値に固定し（`Quaternion.Euler(x, _rotation.y, _rotation.z)`）
  - X回転が範囲外に入った場合に 0 または 352 付近へ丸める  
  という処理で、押下判定が想定角度帯（352〜360付近）で動く前提を保つ。  
  `Assets/Scripts/Piano/PianoKey.cs:188`〜`199`

---

## 4. 指の接触（touch）・底面（bottom）判定（押下とは別系統）
鍵盤押下（音の発火）とは別に、指がどの鍵盤に触れているかを衝突から推定して保持する機構がある（触覚制御などで利用）。

### 4.1 衝突検出 → レジストリ登録
- 各鍵盤に `PianoKeyFingerContactReporter` を付与し、`OnCollisionEnter/Exit` で衝突相手から `FingerColliderId` を取得して `PianoFingerContactRegistry` へ登録する。  
  `Assets/Scripts/Piano/PianoKeyFingerContactReporter.cs:17`〜`29`
- `FingerColliderId` は「左右手・指ID・セグメント番号」を指コライダに付与するメタ情報。  
  `Assets/Scripts/Hands/Colliders/FingerColliderId.cs:22`〜`38`
- 指コライダ生成側は `FingerColliderBuilder` が担当し、生成したコライダへ `FingerColliderId.Set(handedness, fingerId, segmentIndex)` を付与する（TIP側が `segmentIndex=0`）。  
  `Assets/Scripts/Hands/Colliders/FingerColliderBuilder.cs:171`

### 4.2 TIPのみを接触として採用（セグメントフィルタ）
- `PianoFingerContactRegistry` は `requiredSegmentIndex` と一致する指コライダのみを接触として扱う（既定=0 → TIP）。  
  `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:10`, `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:77`
- シーン実体でも `requiredSegmentIndex=0` が設定されている。  
  `Assets/Scenes/hapticpiano/hapticpiano.unity:6565`

### 4.3 複数鍵に触れている場合の primaryKey 選択
- 指が複数鍵盤に同時接触している場合、`PianoFingerContactRegistry` は「X角（0→360補正後）が最も小さい鍵」を primary として採用する。  
  `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:114`〜`125`
- 鍵盤X角は `GetAngleX360()` で `eulerAngles.x` を 0→360 補正して取得する。  
  `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:180`〜`185`

### 4.4 接触の安定化（grace）
- 衝突が一瞬途切れても、`touchReleaseGraceSeconds` の間は前回の鍵を接触中として扱う（ガタつき対策）。  
  `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:24`, `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:133`〜`138`
- シーン実体の設定値: `touchReleaseGraceSeconds=0.08`。  
  `Assets/Scenes/hapticpiano/hapticpiano.unity:6569`

### 4.5 底面（bottom）判定（ヒステリシス）
- 接触中の鍵盤X角が `bottomEnterAngleX` 以下になったら底面ロックし、`bottomExitAngleX` 以上で解除する。  
  `Assets/Scripts/Piano/PianoFingerContactRegistry.cs:155`〜`158`
- シーン実体の設定値: `bottomEnterAngleX=352.5`, `bottomExitAngleX=354.0`。  
  `Assets/Scenes/hapticpiano/hapticpiano.unity:6566`〜`6567`

