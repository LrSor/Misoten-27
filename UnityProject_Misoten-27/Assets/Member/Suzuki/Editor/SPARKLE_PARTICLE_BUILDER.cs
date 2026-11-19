using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

//==================================================
// キラキラ（パーティクル）を自動で作るエディター拡張
//
// 使い方：メニュー「Misoten / キラキラ（パーティクル）を作成」
//
// 次のものを作る・設定する（もう一度実行すると、作り直して上書きする）
//   1. 08_Sprite1 の読み込み設定（透明部分をきれいに）
//   2. マテリアル  Material/Effect/MAT_Sparkle.mat（URP のパーティクル用、半透明）
//   3. プレハブ    Prefab/Effect/FX_Sparkle.prefab（Particle System + SPARKLE_PARTICLE）
//        08_Sprite1 を 2×2 の 4 コマとして、星の光り方が回るようにアニメーションさせる
//   4. ミニゲームに組み込む
//        特急デリバリー     ：配達員（Runner）のまわりに出す
//        落書きクリーンアップ：壁（Wall）の前に広く出す
//      SHAKE_EFFECTS の「パーティクルのキラキラ」に入れて、
//      今までの UI のキラキラ（Sparkle）は使わないようにする
//      ゴール・汚れが 1 段階落ちたときに光らせるキラキラにも設定する
//   5. ブルーム（光のにじみ）
//        Material/Effect/VP_MiniGameBloom.asset（Bloom だけの Volume Profile）
//        ミニゲームの中に BloomVolume（その範囲の中のカメラだけにかかる）を置いて、
//        ミニゲームのカメラのポストプロセスをオンにする
//        → フィールドのカメラや、ほかの人のミニゲームには影響しない
//
// ※ このファイルは Editor フォルダの中に置くこと（ビルドに含まれない）
//==================================================
public static class SPARKLE_PARTICLE_BUILDER
{
    const string TEXTURE_PATH = "Assets/Member/Suzuki/Texture/08_Sprite1.png";
    const string MATERIAL_FOLDER = "Assets/Member/Suzuki/Material/Effect";
    const string MATERIAL_PATH = MATERIAL_FOLDER + "/MAT_Sparkle.mat";
    const string PREFAB_FOLDER = "Assets/Member/Suzuki/Prefab/Effect";
    const string PREFAB_PATH = PREFAB_FOLDER + "/FX_Sparkle.prefab";
    const string DELIVERY_PATH = "Assets/Member/Suzuki/Prefab/MiniGame/MiniGameSystem_Delivery.prefab";
    const string COOKING_PATH = "Assets/Member/Suzuki/Prefab/MiniGame/MiniGameSystem_Cooking.prefab";
    const string GRAFFITI_PATH = "Assets/Member/Suzuki/Prefab/MiniGame/MiniGameSystem_Graffiti.prefab";
    const string FX_NAME = "FX_Sparkle";
    const string PROFILE_PATH = MATERIAL_FOLDER + "/VP_MiniGameBloom.asset";
    const string VOLUME_NAME = "BloomVolume";

    [MenuItem("Misoten/キラキラ（パーティクル）を作成")]
    static void Build()
    {
        Texture2D texture = SetupTexture();

        if (texture == null)
        {
            EditorUtility.DisplayDialog("キラキラ（パーティクル）を作成",
                "画像が見つかりません：\n" + TEXTURE_PATH, "OK");
            return;
        }

        Material material = CreateMaterial(texture);

        if (material == null)
        {
            return;
        }

        GameObject prefab = CreatePrefab(material);
        VolumeProfile profile = CreateBloomProfile();

        // 特急デリバリー：配達員のまわり（直径 1.6m くらいの球）
        bool delivery = AddToMiniGame(DELIVERY_PATH, prefab, "Runner",
            new Vector3(0f, 1f, 0f), ParticleSystemShapeType.Sphere, new Vector3(0.8f, 0.8f, 0.8f), 0.15f, 0.35f, 25f,
            profile, new Vector3(0f, 8f, 25f), new Vector3(40f, 20f, 90f));

        // 落書きクリーンアップ：壁の前（横 7m × 縦 3.5m）
        bool graffiti = AddToMiniGame(GRAFFITI_PATH, prefab, "Wall",
            new Vector3(0f, 2f, -0.4f), ParticleSystemShapeType.Box, new Vector3(7f, 3.5f, 0.2f), 0.3f, 0.6f, 40f,
            profile, new Vector3(0f, 4f, 0f), new Vector3(24f, 12f, 24f));

        // 神速クッキング：まっすぐ切れたときだけ、切り口からパッと出す（振っている間ずっとは出さない）
        bool cooking = AddCutSparkle(COOKING_PATH, prefab, profile);

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("キラキラ（パーティクル）を作成",
            "作成しました。\n\n" +
            "マテリアル：" + MATERIAL_PATH + "\n" +
            "プレハブ：" + PREFAB_PATH + "\n\n" +
            "特急デリバリー：" + (delivery ? "組み込みました" : "見つかりませんでした") + "\n" +
            "神速クッキング：" + (cooking ? "組み込みました" : "見つかりませんでした") + "\n" +
            "落書きクリーンアップ：" + (graffiti ? "組み込みました" : "見つかりませんでした"),
            "OK");

        Selection.activeObject = prefab;
    }

    //==================================================
    // 1. 画像の読み込み設定
    //==================================================
    static Texture2D SetupTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(TEXTURE_PATH) as TextureImporter;

        if (importer == null)
        {
            return null;
        }

        // 透明な部分のふちに色がにじまないようにする（スプライトの設定はそのまま）
        bool changed = false;

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(TEXTURE_PATH);
    }

    //==================================================
    // 2. マテリアル（URP のパーティクル用・半透明）
    //==================================================
    static Material CreateMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
        {
            EditorUtility.DisplayDialog("キラキラ（パーティクル）を作成",
                "URP のパーティクル用シェーダーが見つかりません。", "OK");
            return null;
        }

        CreateFolder(MATERIAL_FOLDER);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MATERIAL_PATH);
        }
        else
        {
            material.shader = shader;
        }

        material.name = "MAT_Sparkle";
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);

        // 半透明（アルファブレンド）にする
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;

        EditorUtility.SetDirty(material);

        return material;
    }

    //==================================================
    // 3. プレハブ（Particle System）
    //==================================================
    static GameObject CreatePrefab(Material material)
    {
        CreateFolder(PREFAB_FOLDER);

        GameObject root = new GameObject(FX_NAME);
        ParticleSystem particle = root.AddComponent<ParticleSystem>();
        SetupParticle(particle, material);
        root.AddComponent<SPARKLE_PARTICLE>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        Object.DestroyImmediate(root);

        return prefab;
    }

    static void SetupParticle(ParticleSystem particle, Material material)
    {
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 基本
        ParticleSystem.MainModule main = particle.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-15f * Mathf.Deg2Rad, 15f * Mathf.Deg2Rad);
        main.startColor = Color.white;
        main.gravityModifier = -0.05f;                       // 少しだけ上にふわっと
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // 走ると後ろに残る
        main.maxParticles = 200;

        // 出る数
        //   ここで決めた数は、再生していないときのプレビュー用（選ぶとシーンビューで見える）
        //   ゲーム中は SPARKLE_PARTICLE が振り方の強さに合わせて変える
        ParticleSystem.EmissionModule emission = particle.emission;
        emission.enabled = true;
        emission.rateOverTime = 25f;

        // 出る範囲
        ParticleSystem.ShapeModule shape = particle.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;

        // 大きさ：パッと大きくなって、だんだん小さくなる
        ParticleSystem.SizeOverLifetimeModule size = particle.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0f));
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 色：最後に消えていく
        ParticleSystem.ColorOverLifetimeModule color = particle.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        // 4 コマのアニメーション（2×2）
        //   画像のコマの並び：左上 0、右上 1、左下 2、右下 3
        //   光の当たり方が回って見えるように 左上 → 右上 → 右下 → 左下 の順で切り替える
        ParticleSystem.TextureSheetAnimationModule sheet = particle.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 2;
        sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.cycleCount = 2;

        AnimationCurve frameCurve = new AnimationCurve(
            new Keyframe(0f, 0f / 4f, 0f, 0f),
            new Keyframe(0.25f, 1f / 4f, 0f, 0f),
            new Keyframe(0.5f, 3f / 4f, 0f, 0f),
            new Keyframe(0.75f, 2f / 4f, 0f, 0f),
            new Keyframe(0.9999f, 2f / 4f, 0f, 0f));

        for (int i = 0; i < frameCurve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(frameCurve, i, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(frameCurve, i, AnimationUtility.TangentMode.Constant);
        }

        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, frameCurve);

        // 見た目
        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    //==================================================
    // 4. ミニゲームに組み込む
    //==================================================
    static bool AddToMiniGame(string prefabPath, GameObject fxPrefab, string parentName,
                              Vector3 localPosition, ParticleSystemShapeType shapeType, Vector3 shapeSize,
                              float minSize, float maxSize, float maxRate,
                              VolumeProfile profile, Vector3 volumeCenter, Vector3 volumeSize)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            return false;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            Transform parent = FindChild(contents.transform, parentName);
            SHAKE_EFFECTS effects = contents.GetComponentInChildren<SHAKE_EFFECTS>(true);

            if (parent == null || effects == null)
            {
                return false;
            }

            // 前に作ったものがあれば作り直す
            Transform old = parent.Find(FX_NAME);

            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            GameObject fx = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab, parent);
            fx.name = FX_NAME;
            fx.transform.localPosition = localPosition;
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one;

            ParticleSystem particle = fx.GetComponent<ParticleSystem>();

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = shapeType;
            shape.randomDirectionAmount = 1f;   // 四方八方にふわっと

            if (shapeType == ParticleSystemShapeType.Box)
            {
                shape.scale = shapeSize;
            }
            else
            {
                shape.radius = shapeSize.x;
            }

            ParticleSystem.MainModule main = particle.main;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);

            // プレビュー用（再生していないとき、選ぶと一番強いときの量で見える）
            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = maxRate;

            SerializedObject fxObject = new SerializedObject(fx.GetComponent<SPARKLE_PARTICLE>());
            fxObject.FindProperty("m_maxRate").floatValue = maxRate;
            fxObject.ApplyModifiedPropertiesWithoutUndo();

            // SHAKE_EFFECTS につなぐ。UI のキラキラは使わない
            SerializedObject effectsObject = new SerializedObject(effects);
            SerializedProperty uiSparkle = effectsObject.FindProperty("m_sparkle");

            if (uiSparkle.objectReferenceValue != null)
            {
                Component ui = uiSparkle.objectReferenceValue as Component;

                if (ui != null)
                {
                    ui.gameObject.SetActive(false);
                }

                uiSparkle.objectReferenceValue = null;
            }

            effectsObject.FindProperty("m_sparkleParticle").objectReferenceValue = fx.GetComponent<SPARKLE_PARTICLE>();
            effectsObject.ApplyModifiedPropertiesWithoutUndo();

            // ゴール・汚れが 1 段階落ちたときのキラキラ（DELIVERY_GAME / GRAFFITI_GAME の m_goalSparkle）
            foreach (MonoBehaviour behaviour in contents.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null)
                {
                    continue;
                }

                SerializedObject gameObject = new SerializedObject(behaviour);
                SerializedProperty goalSparkle = gameObject.FindProperty("m_goalSparkle");

                if (goalSparkle != null)
                {
                    goalSparkle.objectReferenceValue = fx.GetComponent<SPARKLE_PARTICLE>();
                    gameObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            AddBloom(contents, profile, volumeCenter, volumeSize);

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // 神速クッキング：まな板（Board）の子に置いて、COOKING_BOARD の「切り口から出すキラキラ」に入れる
    static bool AddCutSparkle(string prefabPath, GameObject fxPrefab, VolumeProfile profile)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            return false;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            COOKING_BOARD board = contents.GetComponentInChildren<COOKING_BOARD>(true);

            if (board == null)
            {
                return false;
            }

            Transform old = board.transform.Find(FX_NAME);

            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            GameObject fx = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab, board.transform);
            fx.name = FX_NAME;
            fx.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one;

            ParticleSystem particle = fx.GetComponent<ParticleSystem>();

            // まな板の上なので、小さく・切り口のまわりにだけ出す
            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;
            shape.randomDirectionAmount = 1f;

            ParticleSystem.MainModule main = particle.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);

            // 再生していないときのプレビュー用（ゲーム中は、まっすぐ切れたときだけまとめて出る）
            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTime = 15f;

            SerializedObject fxObject = new SerializedObject(fx.GetComponent<SPARKLE_PARTICLE>());
            fxObject.FindProperty("m_maxRate").floatValue = 15f;
            fxObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject boardObject = new SerializedObject(board);
            boardObject.FindProperty("m_cutSparkle").objectReferenceValue = fx.GetComponent<SPARKLE_PARTICLE>();
            boardObject.ApplyModifiedPropertiesWithoutUndo();

            // ブルームは、まな板のまわり（カメラが入る範囲）
            AddBloom(contents, profile, new Vector3(0f, 2f, 0f), new Vector3(12f, 6f, 12f));

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    //==================================================
    // 5. ブルーム
    //==================================================
    static VolumeProfile CreateBloomProfile()
    {
        CreateFolder(MATERIAL_FOLDER);

        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PROFILE_PATH);
        }

        Bloom bloom;

        if (!profile.TryGet(out bloom))
        {
            bloom = profile.Add<Bloom>(true);
            bloom.name = "Bloom";
            AssetDatabase.AddObjectToAsset(bloom, profile);
        }

        bloom.active = true;
        bloom.threshold.Override(1.1f);   // これより明るいところだけ光がにじむ（キラキラは 1.2 以上）
        bloom.intensity.Override(1f);
        bloom.scatter.Override(0.7f);

        EditorUtility.SetDirty(bloom);
        EditorUtility.SetDirty(profile);

        return profile;
    }

    static void AddBloom(GameObject root, VolumeProfile profile, Vector3 center, Vector3 size)
    {
        if (profile == null)
        {
            return;
        }

        // 前に作ったものがあれば作り直す
        Transform old = root.transform.Find(VOLUME_NAME);

        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        // この範囲の中にいるカメラ（ミニゲームのカメラ）だけにブルームがかかる
        GameObject volumeObject = new GameObject(VOLUME_NAME);
        volumeObject.transform.SetParent(root.transform, false);

        BoxCollider box = volumeObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = center;
        box.size = size;

        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = false;
        volume.blendDistance = 0f;
        volume.priority = 10f;
        volume.sharedProfile = profile;

        // ミニゲームのカメラのポストプロセスをオンにする
        foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
        {
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();

            if (data != null)
            {
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
            }
        }
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    static void CreateFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);

        CreateFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
