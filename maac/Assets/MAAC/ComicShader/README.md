# Comic Shader (URP 17.6 / Unity 6000.6)

白黒の日本漫画風レンダリング用シェーダー一式。マテリアルのシェーダーを
`Universal Render Pipeline/Comic` にするだけで、セル（段階）陰影・ベタ・スクリーントーン・
斜線・輪郭線・紙の質感が付きます。

## フォルダ構成
```
ComicShader/
├── Shaders/
│   ├── ComicShader.shader              本体 (ForwardLit / Outline / ShadowCaster / DepthOnly / DepthNormalsOnly)
│   ├── ComicScreenSpaceOutline.shader  Renderer Feature 用の全画面エッジ検出 (Hidden)
│   └── Includes/
│       ├── ComicInput.hlsl             UnityPerMaterial CBUFFER（全パス共通 = SRP Batcher 対応）
│       ├── ComicCommon.hlsl            回転/ハッシュ/バリューノイズ, トーン段階化, パターン座標(Screen/World/Object/UV)
│       ├── ComicLighting.hlsl          URP ライティング → スカラー光量 (Main/Additional/Forward+/Shadows/GI/LightLayers)
│       ├── ComicForwardPass.hlsl       メインパスの頂点/フラグメント
│       ├── Halftone.hlsl               網点
│       ├── Hatching.hlsl               斜線・クロスハッチング
│       ├── PaperNoise.hlsl             紙の粒子・インクムラ
│       └── Outline.hlsl                Inverted Hull アウトラインパス
├── Runtime/  (asmdef: MAAC.ComicShader.Runtime)
│   ├── ComicOutlineFeature.cs          Renderer Feature（Render Graph）
│   ├── ComicOutlinePass.cs             Render Graph Raster Pass
│   ├── ComicMaterialUtility.cs         キーワード/パス同期（スクリプトから変更する場合に使用）
│   └── ComicDemoLightAnimator.cs       デモ用ライト回転
├── Editor/   (asmdef: MAAC.ComicShader.Editor)
│   ├── ComicShaderGUI.cs               マテリアルインスペクター（機能別グループ）
│   ├── ComicRendererSetup.cs           Comic_Renderer 作成（Tools/Comic Shader/Create Comic Renderer）
│   ├── ComicDemoSceneBuilder.cs        デモシーン生成（Tools/Comic Shader/Build Demo Scene）
│   ├── ComicSmoothNormalBaker.cs       アウトライン用スムーズ法線を UV3 に焼き込み
│   ├── ComicShaderValidator.cs         代表的なバリアントを一括コンパイル（Tools/Comic Shader/Validate Shader Variants）
│   └── ComicDemoCapture.cs             検証用キャプチャ（プレビューシーンで描画、開いているシーンは変更しない）
├── Settings/Comic_Renderer.asset       PC_Renderer のコピー + Comic Outline Feature（SSAO はこのコピーでのみ無効）
└── Demo/                               デモシーン・マテリアル・TorusKnot メッシュ
```

## 使い方
1. マテリアルのシェーダーを `Universal Render Pipeline/Comic` にする。
2. 内部の折れ線も描きたい場合は、カメラの Renderer を **Comic_Renderer** にする
   （`Tools/Comic Shader/Create Comic Renderer` で PC_RPAsset のリスト末尾に追加済み。既存 index 0 は不変）。
   既存の PC_Renderer に直接付けたい場合は Renderer の *Add Renderer Feature → Comic Outline Feature*。
3. デモ: `Assets/MAAC/ComicShader/Demo/ComicShaderDemo.unity`。Directional Light の
   `ComicDemoLightAnimator` を有効にすると Play 中にライトが回転・明滅します。

## トーンの考え方
- `shade = 輝度(albedo) × (メインライト + 追加ライト×強度 + 環境光×強度)`
- **Shadow Threshold** = 紙の白と最初の影段の境界となる shade 値。**Contrast** = 段が深くなる速さ。
- **Tone Steps** 段に量子化（2 で白黒二値）。最暗段は常にベタ（Ink Color）。
- ハーフトーン有効時は灰色段が網点になります（濃いほど大きい点）。ハッチングは段階化前の連続値を使うので線の太さが滑らかに変化します。

## アウトライン方式の比較と採用理由
| 方式 | 長所 | 短所 |
|---|---|---|
| Inverted Hull（マテリアル内パス） | マテリアル毎に太さ/色、ピクセル幅一定＋距離補正、軽い。OFF でドローコール 0 | 内部の折れ線は出ない。ハードエッジで線が割れる（→スムーズ法線焼き込みで解消） |
| 画面空間 Depth/Normal（Renderer Feature） | 全オブジェクトの外周＋内部の折れ線、割れない | 全画面 1 パス + DepthNormals プリパスが必要（PC_Renderer は SSAO が既に実行） |

両方を併用する構成にしています。どちらも個別に OFF にできます。

## 実装メモ
- URP 17.6 は Render Graph のみ（Compatibility Mode 無し）→ Feature は `RecordRenderGraph` で実装。
- 追加ライトのループは URP の `LIGHT_LOOP_BEGIN` / `URP_FP_DIRECTIONAL_LIGHTS_COUNT` を使い Forward / Forward+ 両対応。
- ShadowCaster / DepthOnly / DepthNormals は URP 同梱の `ShadowCasterPass.hlsl` 等を再利用。
- 機能 ON/OFF は `shader_feature_local(_fragment)`。アウトライン OFF は `SetShaderPassEnabled("SRPDefaultUnlit", false)`。
- 網点・斜線は fwidth で AA。セルが 2〜4px 未満になると同じ濃度のフラットトーンへフェードしモアレ/ちらつきを防止。

## 既知の制約
- 不透明（＋アルファクリップ）のみ。半透明は未対応。
- Meta パス無し：ライトマップのベイクでは albedo/emission が既定値扱い（ライトマップのサンプリング自体は対応）。
- Dynamic Lightmap / Screen Space Irradiance / Light Cookies / Decals(DBuffer) / Deferred GBuffer は非対応（Deferred では Forward-Only として描画）。
- Screen Space モードの網点は画面に固定されるため、物体が動くと模様の上を滑る（"shower door"）。`Screen: Anchor To Object` で物体の原点に追従可能（静的バッチされた物体はバッチ原点基準）。
- World/Object モードはトライプラナー（3 回評価）で、球の斜め部分は模様がブレンドされます。
- 紙ノイズは画面固定（紙が画面に貼り付いている表現）。
- 量子化により影マップのアクネ・フィルタリングノイズや低角度ライトのノイズが段境界で強調されることがあります（Light の Bias や Shadow Resolution で調整）。
- shader_feature のため、ビルドで未使用の組み合わせはストリップされます。ランタイムで切り替える組み合わせは、いずれかのマテリアルか ShaderVariantCollection で参照してください。
- UV3 スムーズ法線はオブジェクト空間で保存するため SkinnedMeshRenderer ではスキニングされません。
- Comic_Renderer を index 1 として PC_RPAsset にのみ追加。Quality=Mobile でそのカメラを使うと既定レンダラーへフォールバック（URP 警告）。
