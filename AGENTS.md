# AGENTS.md — Agent Guidelines for HAPTICPIANO

このファイルは、このリポジトリで動作する AI エージェント（Codex 等）が従うべき  
作業ルール・参照範囲・禁止事項・行動方針を定義する。

本プロジェクトの目的：  
Unity（2022.3 LTS）+ SteamVR/OpenVR + OpenGloves/LucidGloves を用いた  
XR ピアノ + 触覚（Force Feedback）体験の実装・評価を行うこと。

今後 piano / haptics / evaluation などの feature が追加されても  
同じ構造（feature/*.md → story/*.md → コード）で扱う。

ESP32 ファームウェアは別リポジトリに存在し、このリポジトリには含めない：
- https://github.com/htomoya16/lucidgloves-old/tree/main/firmware/lucidgloves-firmware

---

## 1. このリポジトリで扱う範囲

エージェントが扱うのは主に Unity 側のロジックである：

- 手モデル / スケルトン処理
- curl 値取得および可視化（HandCurlTracker / HandVisualFromCurl）
- 今後手モデル周りで追加される当たり判定・指の曲がり具合の調整
- XR ピアノ用シーン `hapticpiano.unity` 内での手の振る舞い

ESP32 のコードは外部参照のみで、このリポジトリ内で生成・改変は行わない。

---

## 2. プロジェクト構造の前提
```
HAPTICPIANO/
    Assets/
        Scenes/hapticpiano/hapticpiano.unity
        Scripts/Hands/HandCurlTracker.cs
        Scripts/Hands/HandVisualFromCurl.cs
    Packages/
    ProjectSettings/
    UserSettings/
    AGENTS.md
    docs/
        requirements/
            README.md
            000-overview.md
            feature/
                *.md
            story/
                *.md
        architecture.md
        design-decisions.md
    .gitignore
    .vscode/
```

**Unity プロジェクトのコアは Assets/ 以下であり、  
エージェントの作業対象もここに限定される。**

## 3. 参照すべき要件ドキュメント
### 入口 / 索引
- docs/requirements/README.md

### 上位（全体像）
- docs/requirements/000-overview.md

### 中位
- docs/requirements/feature/*.md

### 下位（ストーリー / 受け入れ条件）
- docs/requirements/story/*.md


## 4. エージェントの役割

1. 対象 feature と story を読み、Unity コードの改善点を抽出する  
2. 小規模で安全な改善提案を行う（理由付き）  
3. diff 形式のコード案を提示する（自動書き換えはしない）  
4. `hapticpiano.unity` での手モデル挙動を改善する方向を優先する  
5. SteamVR/OpenGloves の制約に反しない範囲で助言する

## 5. 禁止事項

エージェントは、ユーザーが明示的に求めない限り、以下を行ってはならない：
- 大規模な自動リファクタリング（多数ファイルの一括変更）
- Unity プロジェクト設定（Input, XR, Package 等）の破壊的変更
- ランタイムを OpenXR に切り替える提案・実行
- SteamVR / OpenGloves の設定ファイルの自動編集
- ESP32 ファームウェアの生成・改変
- ディレクトリ構造の rename / 移動
- rm -rf 等の破壊的 CLI の提案
- セキュリティリスクのあるコード生成

## 6. 作業フロー
エージェントは次の流れで提案する：
1. README → 000-overview → feature → story の順で要件を確認
2. story の受け入れ条件をチェックする
3. コードを読み、問題点 or 目的への合致度を整理
4. 理由付き改善案 + 小さな差分コードを提示。
5. 提案をユーザーが適用・実機確認し、条件を満たしているか確認

## 7. 出力ポリシー

- 回答はすべて **日本語**
- 英語仕様を参照する場合は日本語で要約する

以上に従い、HAPTICPIANO プロジェクトの実装支援を行う。