using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

//==================================================
// 検証用の簡易フィールドを自動で組み立てるエディター拡張
//
// 使い方：メニュー「Misoten / 簡易フィールドを作成」→ 数値を確認して「作成」
//
// 手作業で作る場合も、この構造（名前と並び）に合わせて作っておけば、
// あとで自動生成に切り替えても、他のスクリプトから同じように扱える。
//
//   TestField
//   ├─ Ground        地面（上の面が高さ 0）
//   ├─ Grid          10m ごとの目盛り線（当たり判定なし）
//   ├─ Walls         外周の壁 + 内側の壁
//   ├─ Slopes        15° / 30° / 45° の坂と、その先の台
//   ├─ TimeGates     移動時間を計るゲート（両端）
//   └─ SpawnPoints
//      ├─ Player     プレイヤーの出現地点 ×4
//      ├─ Quest      クエストの出現地点（グループ A～E × 2）
//      ├─ Part       部品の出現地点（グループ A～E × 4）
//      └─ NPC        NPC 用の地点 ×8
//
// 出現地点は空のオブジェクトで、見た目確認用の目印（当たり判定なし）を子に持つ。
// クエスト・部品の地点には安井さんの CustomSpawnPoint を付ける。
//
// ※ このファイルは Editor フォルダの中に置くこと（ビルドに含まれない）
//==================================================
public class FIELD_BUILDER : ScriptableWizard
{
    [Header("広さ")]
    [Tooltip("フィールドの1辺の長さ (m)。歩き 5m/秒 × 15秒 = 75m")]
    public float m_fieldSize = 75f;

    [Header("壁")]
    public float m_wallHeight = 3f;
    public float m_wallThickness = 1f;
    [Tooltip("フィールドの内側にも壁を置く")]
    public bool m_createInnerWalls = true;
    [Tooltip("壁を摩擦0にする（プレイヤーが壁に張り付かないように）")]
    public bool m_noFrictionWalls = true;

    [Header("坂")]
    public float[] m_slopeAngles = { 15f, 30f, 45f };
    public float m_slopeLength = 8f;
    public float m_slopeWidth = 4f;

    [Header("その他")]
    [Tooltip("10m ごとの目盛り線を引く")]
    public bool m_createGrid = true;
    [Tooltip("移動時間を計るゲートを両端に置く")]
    public bool m_createTimeGates = true;
    [Tooltip("シーンに同じ名前のフィールドがあれば消して作り直す")]
    public bool m_replaceExisting = true;

    const string ROOT_NAME = "TestField";
    const string MATERIAL_FOLDER = "Assets/Member/Suzuki/Material/Field";

    // 75m のときの配置。広さを変えると比率で拡大縮小する
    const float BASE_SIZE = 75f;

    static readonly Vector2[][] QUEST_POINTS =
    {
        new[] { new Vector2(-25f, 25f), new Vector2(22f, -8f) },    // A
        new[] { new Vector2(0f, 28f),   new Vector2(-28f, 6f) },    // B
        new[] { new Vector2(26f, 26f),  new Vector2(6f, -12f) },    // C
        new[] { new Vector2(-12f, 14f), new Vector2(28f, -28f) },   // D
        new[] { new Vector2(14f, 24f),  new Vector2(-6f, -6f) },    // E
    };

    static readonly Vector2[] PLAYER_POINTS =
    {
        new Vector2(-3f, 0f), new Vector2(3f, 0f), new Vector2(0f, 3f), new Vector2(0f, -3f),
    };

    static readonly Vector2[] NPC_POINTS =
    {
        new Vector2(-32f, -32f), new Vector2(32f, -32f), new Vector2(-32f, 32f), new Vector2(32f, 32f),
        new Vector2(-32f, 0f),   new Vector2(32f, 0f),   new Vector2(0f, 32f),   new Vector2(20f, -32f),
    };

    static readonly Color[] GROUP_COLORS =
    {
        Color.red, Color.blue, Color.green, Color.yellow, Color.magenta,
    };

    static readonly string[] GROUP_NAMES = { "A", "B", "C", "D", "E" };

    // 作成したマテリアルの使い回し
    private readonly Dictionary<string, Material> m_materials =
        new Dictionary<string, Material>();

    private PhysicsMaterial m_noFrictionMaterial;

    [MenuItem("Misoten/簡易フィールドを作成")]
    static void Open()
    {
        DisplayWizard<FIELD_BUILDER>("簡易フィールドを作成", "作成");
    }

    void OnWizardUpdate()
    {
        helpString =
            "今開いているシーンに「" + ROOT_NAME + "」を作ります。\n" +
            "作ったあとは自由に手で動かして大丈夫です（作り直すと元に戻ります）。";

        m_fieldSize = Mathf.Max(10f, m_fieldSize);
        m_wallHeight = Mathf.Max(0.5f, m_wallHeight);
        m_wallThickness = Mathf.Max(0.1f, m_wallThickness);
        m_slopeLength = Mathf.Max(1f, m_slopeLength);
        m_slopeWidth = Mathf.Max(1f, m_slopeWidth);
    }

    void OnWizardCreate()
    {
        if (m_replaceExisting)
        {
            GameObject existing = GameObject.Find(ROOT_NAME);

            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }
        }

        GameObject root = new GameObject(ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(root, "簡易フィールドを作成");

        PrepareAssets();

        BuildGround(root.transform);

        if (m_createGrid)
        {
            BuildGrid(root.transform);
        }

        BuildWalls(root.transform);
        BuildSlopes(root.transform);

        if (m_createTimeGates)
        {
            BuildTimeGates(root.transform);
        }

        BuildSpawnPoints(root.transform);

        Selection.activeGameObject = root;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);

        Debug.Log(
            "簡易フィールドを作成しました（" + m_fieldSize + "m × " + m_fieldSize + "m）。シーンを保存してください。",
            root);
    }

    //==================================================
    // 地面・目盛り
    //==================================================

    void BuildGround(Transform root)
    {
        // 厚みのある箱にして、上の面がちょうど高さ 0 になるように置く
        GameObject ground = CreateBlock(
            "Ground", root,
            new Vector3(0f, -0.5f, 0f),
            new Vector3(m_fieldSize, 1f, m_fieldSize),
            GetMaterial("Field_Ground", new Color(0.55f, 0.55f, 0.55f)));

        ground.isStatic = true;
    }

    void BuildGrid(Transform root)
    {
        Transform grid = CreateGroup("Grid", root);

        float half = m_fieldSize * 0.5f;
        Material material = GetMaterial("Field_Grid", new Color(0.3f, 0.3f, 0.3f));

        // 中心から 10m ごと
        for (float offset = 0f; offset <= half; offset += 10f)
        {
            foreach (float sign in offset == 0f ? new[] { 1f } : new[] { -1f, 1f })
            {
                float position = offset * sign;

                CreateMarker("Line_X_" + position, PrimitiveType.Cube, grid,
                    new Vector3(position, 0.01f, 0f),
                    new Vector3(0.1f, 0.02f, m_fieldSize), material);

                CreateMarker("Line_Z_" + position, PrimitiveType.Cube, grid,
                    new Vector3(0f, 0.01f, position),
                    new Vector3(m_fieldSize, 0.02f, 0.1f), material);
            }
        }
    }

    //==================================================
    // 壁
    //==================================================

    void BuildWalls(Transform root)
    {
        Transform walls = CreateGroup("Walls", root);

        float half = m_fieldSize * 0.5f;
        float y = m_wallHeight * 0.5f;
        float offset = half + m_wallThickness * 0.5f;
        float length = m_fieldSize + m_wallThickness * 2f;

        Material material = GetMaterial("Field_Wall", new Color(0.85f, 0.85f, 0.8f));

        CreateWall("Wall_North", walls, new Vector3(0f, y, offset), new Vector3(length, m_wallHeight, m_wallThickness), material);
        CreateWall("Wall_South", walls, new Vector3(0f, y, -offset), new Vector3(length, m_wallHeight, m_wallThickness), material);
        CreateWall("Wall_East", walls, new Vector3(offset, y, 0f), new Vector3(m_wallThickness, m_wallHeight, m_fieldSize), material);
        CreateWall("Wall_West", walls, new Vector3(-offset, y, 0f), new Vector3(m_wallThickness, m_wallHeight, m_fieldSize), material);

        if (!m_createInnerWalls)
        {
            return;
        }

        float s = m_fieldSize / BASE_SIZE;

        // まっすぐな壁 ×2 と L字の壁（角に引っかからないかの確認用）
        CreateWall("Wall_Inner_1", walls, new Vector3(15f * s, y, 14f * s), new Vector3(14f * s, m_wallHeight, m_wallThickness), material);
        CreateWall("Wall_Inner_2", walls, new Vector3(-18f * s, y, 5f * s), new Vector3(m_wallThickness, m_wallHeight, 14f * s), material);
        CreateWall("Wall_Inner_L1", walls, new Vector3(19f * s, y, -18f * s), new Vector3(10f * s, m_wallHeight, m_wallThickness), material);
        CreateWall("Wall_Inner_L2", walls, new Vector3(24f * s, y, -14f * s), new Vector3(m_wallThickness, m_wallHeight, 8f * s), material);
    }

    void CreateWall(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject wall = CreateBlock(name, parent, position, scale, material);

        wall.isStatic = true;

        if (m_noFrictionWalls)
        {
            wall.GetComponent<Collider>().sharedMaterial = m_noFrictionMaterial;
        }
    }

    //==================================================
    // 坂
    //==================================================

    void BuildSlopes(Transform root)
    {
        Transform slopes = CreateGroup("Slopes", root);

        float half = m_fieldSize * 0.5f;
        float s = m_fieldSize / BASE_SIZE;
        float thickness = 0.5f;

        Material slopeMaterial = GetMaterial("Field_Slope", new Color(0.95f, 0.6f, 0.25f));
        Material platformMaterial = GetMaterial("Field_Platform", new Color(0.8f, 0.5f, 0.2f));

        // 南の壁沿いに、奥（+Z）へ登る坂を並べる
        float startZ = -half + 1.5f;

        for (int i = 0; i < m_slopeAngles.Length; i++)
        {
            float angle = Mathf.Clamp(m_slopeAngles[i], 1f, 80f);
            float radian = angle * Mathf.Deg2Rad;

            float x = (-20f + 10f * i) * s;

            // 傾けた箱の上の面が、手前の端でちょうど地面（高さ 0）に接するように置く
            Vector3 forward = new Vector3(0f, Mathf.Sin(radian), Mathf.Cos(radian));
            Vector3 up = new Vector3(0f, Mathf.Cos(radian), -Mathf.Sin(radian));

            Vector3 start = new Vector3(x, 0f, startZ);
            Vector3 topCenter = start + forward * (m_slopeLength * 0.5f);
            Vector3 center = topCenter - up * (thickness * 0.5f);

            GameObject slope = CreateBlock(
                "Slope_" + angle + "deg", slopes, center,
                new Vector3(m_slopeWidth, thickness, m_slopeLength),
                slopeMaterial);

            slope.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
            slope.isStatic = true;

            // 坂を登りきった先の台
            float height = m_slopeLength * Mathf.Sin(radian);
            float endZ = startZ + m_slopeLength * Mathf.Cos(radian);
            float platformDepth = 3f;

            GameObject platform = CreateBlock(
                "Platform_" + angle + "deg", slopes,
                new Vector3(x, height * 0.5f, endZ + platformDepth * 0.5f),
                new Vector3(m_slopeWidth, height, platformDepth),
                platformMaterial);

            platform.isStatic = true;
        }
    }

    //==================================================
    // 移動時間を計るゲート
    //==================================================

    void BuildTimeGates(Transform root)
    {
        Transform gates = CreateGroup("TimeGates", root);

        float x = m_fieldSize * 0.5f - 3f;

        CreateGate("Gate_West", gates, new Vector3(-x, 1.5f, 0f));
        CreateGate("Gate_East", gates, new Vector3(x, 1.5f, 0f));
    }

    void CreateGate(string name, Transform parent, Vector3 position)
    {
        GameObject gate = new GameObject(name);
        gate.transform.SetParent(parent, false);
        gate.transform.localPosition = position;

        BoxCollider box = gate.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 3f, m_fieldSize);

        gate.AddComponent<FIELD_TIME_GATE>();
    }

    //==================================================
    // 出現地点
    //==================================================

    void BuildSpawnPoints(Transform root)
    {
        Transform spawnRoot = CreateGroup("SpawnPoints", root);
        float s = m_fieldSize / BASE_SIZE;

        // プレイヤー
        Transform players = CreateGroup("Player", spawnRoot);
        Material playerMaterial = GetMaterial("Marker_Player", Color.white);

        for (int i = 0; i < PLAYER_POINTS.Length; i++)
        {
            Transform point = CreatePoint("Player_" + (i + 1), players, PLAYER_POINTS[i] * s);

            CreateMarker("Marker", PrimitiveType.Capsule, point,
                new Vector3(0f, 1f, 0f), new Vector3(0.6f, 1f, 0.6f), playerMaterial);
        }

        // クエスト（グループごと）
        Transform quests = CreateGroup("Quest", spawnRoot);

        for (int group = 0; group < QUEST_POINTS.Length; group++)
        {
            Transform groupRoot = CreateGroup("Group" + GROUP_NAMES[group], quests);
            Material material = GetMaterial("Marker_Group" + GROUP_NAMES[group], GROUP_COLORS[group]);

            for (int i = 0; i < QUEST_POINTS[group].Length; i++)
            {
                string pointName = "Quest_" + GROUP_NAMES[group] + "_" + (i + 1);
                Transform point = CreatePoint(pointName, groupRoot, QUEST_POINTS[group][i] * s);

                AddCustomSpawnPoint(point, CustomSpawnPoint.PointType.Quest, (GroupType)group, pointName);

                CreateMarker("Marker", PrimitiveType.Cube, point,
                    new Vector3(0f, 0.5f, 0f), Vector3.one, material);
            }
        }

        // 部品（グループごとに 4 か所。中心のまわりに円状に散らす）
        Transform parts = CreateGroup("Part", spawnRoot);

        for (int group = 0; group < GROUP_NAMES.Length; group++)
        {
            Transform groupRoot = CreateGroup("Group" + GROUP_NAMES[group], parts);
            Material material = GetMaterial("Marker_Group" + GROUP_NAMES[group], GROUP_COLORS[group]);

            for (int i = 0; i < 4; i++)
            {
                int index = group * 4 + i;
                float angle = Mathf.PI * 2f * index / 20f + 0.15f;
                float radius = (i % 2 == 0 ? 11f : 21f) * s;

                Vector2 position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

                string pointName = "Part_" + GROUP_NAMES[group] + "_" + (i + 1);
                Transform point = CreatePoint(pointName, groupRoot, position);

                AddCustomSpawnPoint(point, CustomSpawnPoint.PointType.Part, (GroupType)group, pointName);

                CreateMarker("Marker", PrimitiveType.Sphere, point,
                    new Vector3(0f, 0.5f, 0f), Vector3.one * 0.6f, material);
            }
        }

        // NPC
        Transform npcs = CreateGroup("NPC", spawnRoot);
        Material npcMaterial = GetMaterial("Marker_NPC", new Color(0.4f, 0.4f, 0.4f));

        for (int i = 0; i < NPC_POINTS.Length; i++)
        {
            Transform point = CreatePoint("NPC_" + (i + 1), npcs, NPC_POINTS[i] * s);

            CreateMarker("Marker", PrimitiveType.Cylinder, point,
                new Vector3(0f, 1f, 0f), new Vector3(0.5f, 1f, 0.5f), npcMaterial);
        }
    }

    static void AddCustomSpawnPoint(Transform point, CustomSpawnPoint.PointType type, GroupType group, string pointName)
    {
        CustomSpawnPoint spawnPoint = point.gameObject.AddComponent<CustomSpawnPoint>();

        spawnPoint.pointType = type;
        spawnPoint.groupType = group;
        spawnPoint.pointName = pointName;
    }

    //==================================================
    // 作成の補助
    //==================================================

    static Transform CreateGroup(string name, Transform parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    // 出現地点（空のオブジェクト。高さ 0 の地面の上）
    static Transform CreatePoint(string name, Transform parent, Vector2 position)
    {
        GameObject point = new GameObject(name);
        point.transform.SetParent(parent, false);
        point.transform.localPosition = new Vector3(position.x, 0f, position.y);
        return point.transform;
    }

    // 当たり判定のある箱（地面・壁・坂）
    static GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.localPosition = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }

    // 見た目だけの目印（当たり判定を外す。プレイヤーや NPC がぶつからないように）
    static GameObject CreateMarker(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject marker = GameObject.CreatePrimitive(type);
        marker.name = name;

        Collider collider = marker.GetComponent<Collider>();

        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = position;
        marker.transform.localScale = scale;

        Renderer renderer = marker.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return marker;
    }

    //==================================================
    // マテリアル・物理マテリアル（既にあれば使い回す）
    //==================================================

    void PrepareAssets()
    {
        EnsureFolder(MATERIAL_FOLDER);

        string physicsPath = MATERIAL_FOLDER + "/Field_NoFriction.asset";

        m_noFrictionMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(physicsPath);

        if (m_noFrictionMaterial == null)
        {
            m_noFrictionMaterial = new PhysicsMaterial("Field_NoFriction");
            m_noFrictionMaterial.dynamicFriction = 0f;
            m_noFrictionMaterial.staticFriction = 0f;
            m_noFrictionMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;

            AssetDatabase.CreateAsset(m_noFrictionMaterial, physicsPath);
        }
    }

    Material GetMaterial(string name, Color color)
    {
        Material material;

        if (m_materials.TryGetValue(name, out material))
        {
            return material;
        }

        string path = MATERIAL_FOLDER + "/" + name + ".mat";

        material = AssetDatabase.LoadAssetAtPath<Material>(path);

        // 既にあるものは色を上書きしない（手で変えた色を残す）
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader);
            material.name = name;

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;

            AssetDatabase.CreateAsset(material, path);
        }

        m_materials[name] = material;

        return material;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folder);

        EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
