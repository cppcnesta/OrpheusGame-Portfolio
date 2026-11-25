# OrpheusGame（俄耳甫斯：众说咏叹）

ギリシャ神話のオルフェウスを題材にした、ビジュアルノベルパートとリズムゲームを組み合わせた 2D ゲームです。
元々はチーム開発用リポジトリで管理しており、本リポジトリは応募提出用に不要な履歴・一時スクリプトを整理し、日本語の README を追加したものです。

※ ゲーム内テキスト、シナリオ構文、Inspector の Tooltip やログはチーム内の開発言語に合わせて中国語で記述しています。

## 開発環境

- Unity 6000.3.24f1 (URP 2D)
- C#
- Unity Input System / TextMesh Pro / Unity Test Framework

## 開発体制・担当範囲

- 開発人数：[6名／プログラマ2名、アート2名、企画1名、音楽1名）]
- 開発期間：[2026年9月〜10月）]

**自分の担当箇所**
- 汎用ビジュアルノベルフレームワークの設計・実装（`Assets/OrpheusVN/`）
- ゲーム全体の進行管理、セーブ・ロード、パズル・演出・タイトル画面の実装（`Assets/Game/Scripts/Runtime/`）
- タイトル水面・紙質感などのカスタムシェーダー（`Assets/Game/Shaders/`）
- シナリオ検証・ビルド用エディタ拡張、自動チェック（`Assets/Game/Scripts/Editor/`, `Diagnostics/`）

※ リズムゲーム本体（`Assets/Game/Rhythm/Scripts/`）は別メンバーが担当しており、自分は `IRhythmAdapter` を介した結合部分を担当しています。

## 動作確認方法

1. Unity 6000.3.24f1 で本プロジェクトを開きます。
2. `Assets/Game/Scenes/Persistent.unity`（または上部メニュー `Orpheus/打开游戏场景`）を開いて再生します。
3. 基本操作はクリック / Space / Enter でテキスト送り、マウスホイールでログ確認です。

**途中から確認したい場合**
- シーン内の `GameFlow` コンポーネントにある `editorStartLabel` に任意のチャプター名を指定して再生すると、タイトルを飛ばしてその場面から開始できます（ビルド版では無効）。

**テスト・静的チェック**
- EditMode テスト：Test Runner から `Orpheus.VN.Tests`（`VNRunnerTests.cs`）を実行できます。
- プロジェクト検証：上部メニュー `Orpheus/检查工程` で、シナリオ構文・素材参照・シーン内の参照漏れをチェックできます。

## ディレクトリ構成（主要スクリプトのみ）

```text
Assets/
├─ OrpheusVN/                  # ゲーム固有処理を持たない汎用 VN フレームワーク（独立 asmdef）
│  ├─ Runtime/
│  │  ├─ Core/                 # 実行器 (VNRunner)、コンテキスト、セーブ処理 (VNSave)
│  │  ├─ Commands/             # 会話・分岐・演出・音声・ミニゲーム呼び出しコマンド
│  │  ├─ Views/                # ダイアログ、背景、立ち絵、CG、音声の表示層
│  │  └─ Data/                 # キャラクター・アセット定義 (ScriptableObject)
│  ├─ Editor/                  # .orph シナリオ用の ScriptedImporter、構文バリデータ
│  └─ Tests/Editor/            # VNRunner / コマンド / セーブ周りの EditMode テスト
└─ Game/
   ├─ Scenes/                  # Persistent.unity（本編単一シーン）、RhythmTest.unity
   ├─ Story/                   # シナリオ本体 (Orpheus.orph)、キャラ表、アセット一覧
   ├─ Rhythm/                  # リズムゲーム本体（他メンバー担当）および譜面データ
   ├─ Shaders/                 # WaterRipple / PaperGrain / AdditiveLight
   └─ Scripts/
      ├─ Runtime/
      │  ├─ GameFlow.cs        # タイトル〜本編〜エンディングまでのエントリーポイント
      │  ├─ VNIntegration/     # VN コマンドと各ゲームシステムを繋ぐアダプタ群
      │  ├─ Stage/             # 紙の落下・堆積 (PaperSpawner)、幕ごとの舞台演出 (StageSceneryView)
      │  ├─ UI/                # タイトル画面、会話レイアウト切替、音符並べ替えパズル
      │  └─ Rhythm/            # IRhythmAdapter 定義、モック実装、シナリオからの台詞抽出
      ├─ Editor/               # シーン起動・工程チェック・ビルドスクリプト (OrpheusTools)
      └─ Diagnostics/          # 開発ビルド用の自動通し確認 (SmokeRunner 等)
```

## 設計・実装メモ

- **VN フレームワークとゲーム側の分離**
  `OrpheusVN` を独立した Assembly Definition に分け、ゲーム固有のミニゲームや特殊演出は `IGameplaySegment` / `IStageEffect` を登録する形にしました。リズムゲーム側とも `IRhythmAdapter` で切り離しているため、リズム部分の完成前から `MockRhythmAdapter` で本編の分岐やリトライ処理を通し確認できました。
- **シナリオのカスタムインポートと検証**
  シナリオは独自拡張子（`.orph`）のテキストとして管理し、`OrpheusScriptImporter`（`ScriptedImporter`）で `ScriptableObject` に変換しています。インポート時に未定義コマンドやジャンプ先ラベルの欠落を検出し、さらに `VNScriptValidator` でアセット登録漏れもチェックできるようにしています。
- **セーブデータの拡張性**
  `VNRunner` 側では直近ラベルからのオフセット・変数・背景や立ち絵の状態を保存し、`Saving` / `Restoring` イベントを通じてゲーム側（床に積もった紙の枚数など）の追加データを `VNSaveData.extras` に載せる構造にしています。
- **演出・UI 周り**
  - `WaterRipple.shader`：タイトル画面の水面揺らぎ・滴の波紋・マウス移動に追従する波紋を UI 用シェーダーで実装しました。
  - `DialogueArtView.cs`：同じ `DialogueView` の出力先を、立ち絵あり・ナレーション・舞台上のキャラ頭上吹き出し・第三幕用の下部字幕へと切り替えています。
  - `PaperSpawner.cs`：落下する紙片の個別アニメーションと、一定枚数ごとに手描きイラストの紙山へフェード統合する処理を管理しています。

## 補足

- `Assets/Game/Scripts/Diagnostics/` は開発ビルド・エディタ用の自動通しテスト用コードで、リリースビルドには含まれません。
- オプション設定画面など一部タイトル画面のボタンは未実装（非活性）となっています。
