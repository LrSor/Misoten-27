# REAPER / Codex 作曲環境

このフォルダーは Unity の Assets 外にある音楽制作専用の作業場所です。

## 連携

Codex が Lua (ReaScript) を作成し、REAPER の Actions > Show action list > New action > Load ReaScript で登録・実行します。Python、API キー、MCP、追加プラグインは不要です。

## 動作確認

`Codex_Setup_Test.lua` は付属 ReaSynth のトラックと約4秒の MIDI を追加します。再生は REAPER の Space キーです。同名のテストトラックがある場合は変更せず終了します。プロジェクトを自動保存したり、外部送信したりする処理はありません。

作業を保存する場合はこのフォルダーの `Projects/` 配下に REAPER の `.rpp` を保存します。Unity に取り込む完成 WAV は必要になった時点で許可された Assets 配下へ追加します。

## 初期構成

- REAPER 7.84 Windows x64 評価版
- 音源: REAPER 付属 ReaSynth（簡易シンセ。生楽器音源は未追加）
- 音声出力: WASAPI / Shared mode / Default output / ステレオ
- 録音入力: 0 チャンネル

実際の出力機器や再生状態は REAPER の Audio > Device とメーターで確認してください。
