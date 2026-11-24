using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

//==================================================
// 集中線（パーティクル）を自動で作るエディター拡張
//
// 使い方：メニュー「Suzuki / 集中線（パーティクル）を作成」
//
// 次のものを作る・設定する（もう一度実行すると、作り直して上書きする）
//   1. 05_Sprite の読み込み設定（透明部分をきれいに）
//   2. マテリアル  Material/Effect/MAT_SpeedLines.mat（URP のパーティクル用、半透明）
//   3. プレハブ    Prefab/Effect/FX_SpeedLines.prefab（Particle System + SPEED_LINES_PARTICLE）
//        05_Sprite を上下 2 コマとして、パラパラ切りかえる
//        画面いっぱいの板を 1 枚だけ出し続ける
//   4. 3 つのミニゲームのカメラの子に置いて、SHAKE_EFFECTS の「パーティクルの集中線」に入れる
//      今までの UI の集中線（SpeedLines）は使わないようにする
//
// ※ このファイルは Editor フォルダの中に置くこと（ビルドに含まれない）
//==================================================
public static class SPEED_LINES_PARTICLE_BUILDER
{
    const string TEXTURE_PATH = "Assets/Member/Suzuki/Texture/05_Sprite.png";
    const string MATERIAL_FOLDER = "Assets/Member/Suzuki/Material/Effect";
    const string MATERIAL_PATH = MATERIAL_FOLDER + "/MAT_SpeedLines.mat";
    const string PREFAB_FOLDER = "Assets/Member/Suzuki/Prefab/Effect";
    const string PREFAB_PATH = PREFAB_FOLDER + "/FX_SpeedLines.prefab";
    const string FX_NAME = "FX_SpeedLines";

    static readonly string[] MINIGAME_PATHS =
    {
        "Assets/Member/Suzuki/Prefab/MiniGame/MiniGameSystem_Delivery.prefab",
        "Assets/Member/Suzuki/Prefab/MiniGame/MiniGameSystem_Cooking.prefab",
        "Assets/Member/Suzuki/Prefab/MiniGame/MiniGameSystem_Graffiti.prefab",
    };

    // 上下 2 コマ
    const int TILES_X = 1;
    const int TILES_Y = 2;

    [MenuItem("Suzuki/集中線（パーティクル）を作成")]
    static void Build()
    {
        Texture2D texture = SetupTexture();

        if (texture == null)
        {
            EditorUtility.DisplayDialog("集中線（パーティクル）を作成",
                "画像が見つかりません：\n" + TEXTURE_PATH, "OK");
            return;
        }

        // 1 コマの横 ÷ 縦
        float imageAspect = ((float)texture.width / TILES_X) / ((float)texture.height / TILES_Y);

        Material material = CreateMaterial(texture);

        if (material == null)
        {
            return;
        }

        GameObject prefab = CreatePrefab(material, imageAspect);

        string result = "";

        foreach (string path in MINIGAME_PATHS)
        {
            bool ok = AddToMiniGame(path, prefab);
            result += System.IO.Path.GetFileNameWithoutExtension(path) + "：" + (ok ? "組み込みました" : "見つかりませんでした") + "\n";
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("集中線（パーティクル）を作成",
            "作成しました。\n\n" +
            "マテリアル：" + MATERIAL_PATH + "\n" +
            "プレハブ：" + PREFAB_PATH + "\n\n" + result,
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

        // 画面いっぱいに出すので、ぼやけないようにミップマップを使わない
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
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
            EditorUtility.DisplayDialog("集中線（パーティクル）を作成",
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

        material.name = "MAT_SpeedLines";
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        // 画面の手前に出すので、ほかの半透明（キラキラなど）より後に描く
        material.renderQueue = (int)RenderQueue.Transparent + 100;

        EditorUtility.SetDirty(material);

        return material;
    }

    //==================================================
    // 3. プレハブ（Particle System）
    //==================================================
    static GameObject CreatePrefab(Material material, float imageAspect)
    {
        CreateFolder(PREFAB_FOLDER);

        GameObject root = new GameObject(FX_NAME);
        ParticleSystem particle = root.AddComponent<ParticleSystem>();
        SetupParticle(particle, material, imageAspect);

        SPEED_LINES_PARTICLE lines = root.AddComponent<SPEED_LINES_PARTICLE>();
        SerializedObject linesObject = new SerializedObject(lines);
        linesObject.FindProperty("m_imageAspect").floatValue = imageAspect;
        linesObject.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        Object.DestroyImmediate(root);

        return prefab;
    }

    static void SetupParticle(ParticleSystem particle, Material material, float imageAspect)
    {
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 画面いっぱいの板を 1 枚、ずっと出し続ける
        //   1 秒に 1 枚出して、寿命を少しだけ長くする（つなぎ目でとぎれないように）
        ParticleSystem.MainModule main = particle.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = 1.05f;
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = imageAspect;
        main.startSizeY = 1f;
        main.startSizeZ = 1f;
        main.startRotation = 0f;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;   // カメラについていく
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;       // 大きさは SPEED_LINES_PARTICLE が合わせる
        main.maxParticles = 2;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.enabled = true;
        emission.rateOverTime = 1f;

        // 真ん中から出す（広がらない）
        ParticleSystem.ShapeModule shape = particle.shape;
        shape.enabled = false;

        // 上下 2 コマを、パラパラ切りかえる（1 秒に 6 往復 ＝ 1 秒に 12 コマ。アニメっぽい速さ）
        ParticleSystem.TextureSheetAnimationModule sheet = particle.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = TILES_X;
        sheet.numTilesY = TILES_Y;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        sheet.cycleCount = 6;

        // 見た目
        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // 画面より大きく出せるようにする（初期値 0.5 だと画面の半分までしか大きくならない）
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 10f;
    }

    //==================================================
    // 4. ミニゲームのカメラに組み込む
    //==================================================
    static bool AddToMiniGame(string prefabPath, GameObject fxPrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            return false;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            MINIGAME_CAMERA miniGameCamera = contents.GetComponentInChildren<MINIGAME_CAMERA>(true);
            Camera camera = miniGameCamera != null
                ? miniGameCamera.GetComponent<Camera>()
                : contents.GetComponentInChildren<Camera>(true);

            SHAKE_EFFECTS effects = contents.GetComponentInChildren<SHAKE_EFFECTS>(true);

            if (camera == null || effects == null)
            {
                return false;
            }

            // 前に作ったものがあれば作り直す
            Transform old = camera.transform.Find(FX_NAME);

            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            GameObject fx = (GameObject)PrefabUtility.InstantiatePrefab(fxPrefab, camera.transform);
            fx.name = FX_NAME;
            fx.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            fx.transform.localRotation = Quaternion.identity;
            fx.transform.localScale = Vector3.one;

            // SHAKE_EFFECTS につなぐ。UI の集中線は使わない
            SerializedObject effectsObject = new SerializedObject(effects);
            SerializedProperty uiLines = effectsObject.FindProperty("m_speedLines");

            if (uiLines.objectReferenceValue != null)
            {
                Component ui = uiLines.objectReferenceValue as Component;

                if (ui != null)
                {
                    ui.gameObject.SetActive(false);
                }

                uiLines.objectReferenceValue = null;
            }

            effectsObject.FindProperty("m_speedLinesParticle").objectReferenceValue = fx.GetComponent<SPEED_LINES_PARTICLE>();
            effectsObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
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
