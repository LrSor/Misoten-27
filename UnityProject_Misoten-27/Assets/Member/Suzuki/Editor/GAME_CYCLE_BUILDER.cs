using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

//==================================================
// 今開いているシーンに「ゲームの進行」を追加するメニュー
//   Suzuki/ゲームサイクルを今のシーンに追加
//
// 作るもの
//   ・GameCycle        … GAME_MANAGER / GAME_NETWORK_BRIDGE / GAME_DEBUG_TOOL
//   ・GameCycleCanvas  … 残り時間・カウントダウン・TIME UP の表示、ロビー画面
//   ・GameSync         … Host の進行を全員に合わせる（GAME_SYNC）
//   ・QuestBoard       … ミニゲームゾーンにクエストを出す（GAME_QUEST_BOARD）
//   ・GameCycle に GAME_LOBBY（ロビー画面の操作）も付ける
//
// 澤田さんの Game_xx をコピーしたシーンに使う想定
// （通信のプレイヤーが出てくるまでカウントダウンを待つ設定にする）
// もう一度押すと、前に作ったものを消して作り直す
//==================================================
public static class GAME_CYCLE_BUILDER
{
    const string ROOT_NAME = "GameCycle";
    const string CANVAS_NAME = "GameCycleCanvas";
    const string SYNC_NAME = "GameSync";
    const string QUEST_NAME = "QuestBoard";
    const string RESULT_SCENE = "SuzukiResult";

    [MenuItem("Suzuki/ゲームサイクルを今のシーンに追加")]
    static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // 前に作ったものは消す
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == ROOT_NAME || root.name == CANVAS_NAME || root.name == SYNC_NAME || root.name == QUEST_NAME)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        if (Object.FindFirstObjectByType<GAME_MANAGER>() != null)
        {
            EditorUtility.DisplayDialog(
                "ゲームサイクル",
                "シーンに別の GAME_MANAGER があります。先に消してください。",
                "OK");
            return;
        }

        //------------------------------
        // UI
        //------------------------------
        var canvasObject = new GameObject(CANVAS_NAME, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(canvasObject, "GameCycle");

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ロビーのボタンを押せるように
        canvasObject.AddComponent<GraphicRaycaster>();

        // 残り時間（上の真ん中）
        var timerBack = CreateImage(canvasObject.transform, "TimerBack",
            new Color(0f, 0f, 0f, 0.55f));
        SetRect(timerBack.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(260f, 100f));

        var timerText = CreateText(timerBack.transform, "TimerText", "3:00", 72f);
        Stretch(timerText.rectTransform);

        // カウントダウン（真ん中）
        var countdownText = CreateText(canvasObject.transform, "CountdownText", "3", 260f);
        SetRect(countdownText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 400f));
        countdownText.color = new Color(1f, 0.9f, 0.3f);
        countdownText.outlineWidth = 0.2f;
        countdownText.outlineColor = Color.black;

        // TIME UP
        var timeUpBack = CreateImage(canvasObject.transform, "TimeUp",
            new Color(0f, 0f, 0f, 0.5f));
        Stretch(timeUpBack.rectTransform);

        var timeUpText = CreateText(timeUpBack.transform, "TimeUpText", "TIME UP!", 200f);
        Stretch(timeUpText.rectTransform);
        timeUpText.color = new Color(1f, 0.35f, 0.25f);
        timeUpText.outlineWidth = 0.2f;
        timeUpText.outlineColor = Color.black;

        timeUpBack.gameObject.SetActive(false);
        countdownText.gameObject.SetActive(false);

        //------------------------------
        // 進行役
        //------------------------------
        var rootObject = new GameObject(ROOT_NAME);
        Undo.RegisterCreatedObjectUndo(rootObject, "GameCycle");

        var manager = rootObject.AddComponent<GAME_MANAGER>();
        var managerSo = new SerializedObject(manager);
        managerSo.FindProperty("m_resultSceneName").stringValue = RESULT_SCENE;
        managerSo.FindProperty("m_waitForPlayer").boolValue = true;
        managerSo.FindProperty("m_disablePlayerInput").boolValue = true;
        managerSo.FindProperty("m_timerText").objectReferenceValue = timerText;
        managerSo.FindProperty("m_countdownText").objectReferenceValue = countdownText;
        managerSo.FindProperty("m_timeUpObject").objectReferenceValue = timeUpBack.gameObject;
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        rootObject.AddComponent<GAME_NETWORK_BRIDGE>();

        // F1～F4 加点、F5 残り5秒（画面表示は澤田さんの通信画面と重なるので消しておく）
        var debugTool = rootObject.AddComponent<GAME_DEBUG_TOOL>();
        var debugSo = new SerializedObject(debugTool);
        debugSo.FindProperty("m_showHud").boolValue = false;
        debugSo.ApplyModifiedPropertiesWithoutUndo();

        //------------------------------
        // 同期用（子を持たせない）
        //------------------------------
        var syncObject = new GameObject(SYNC_NAME);
        Undo.RegisterCreatedObjectUndo(syncObject, "GameCycle");
        var sync = syncObject.AddComponent<GAME_SYNC>();

        //------------------------------
        // クエスト（子を持たせない）
        //------------------------------
        var questObject = new GameObject(QUEST_NAME);
        Undo.RegisterCreatedObjectUndo(questObject, "GameCycle");
        var board = questObject.AddComponent<GAME_QUEST_BOARD>();
        SetupQuests(board);

        //------------------------------
        // ロビー
        //------------------------------
        var lobby = rootObject.AddComponent<GAME_LOBBY>();
        BuildLobby(canvasObject.transform, lobby, sync);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = rootObject;

        Debug.Log("ゲームサイクルを追加しました（" + scene.name + "）。シーンを保存してください。");
    }

    //==================================================
    // ロビー画面（日本語を出すので、標準の UI（Text）で作る）
    //==================================================

    static Font s_font;

    static void BuildLobby(Transform canvas, GAME_LOBBY lobby, GAME_SYNC sync)
    {
        s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Color accent = new Color(1f, 0.75f, 0.2f);
        Color blue = new Color(0.22f, 0.42f, 0.82f);
        Color dark = new Color(0.16f, 0.19f, 0.27f);

        // 後ろを少し暗くする（全画面）
        var dim = CreateImage(canvas, "Lobby", new Color(0f, 0f, 0f, 0.45f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // 左上は澤田さんの通信画面があるので、少し右に寄せる
        var frame = CreateImage(dim.transform, "Frame", accent);
        SetRect(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(230f, 0f), new Vector2(1012f, 912f));
        SetRounded(frame);

        var panel = CreateImage(frame.transform, "Panel", new Color(0.06f, 0.08f, 0.13f, 0.97f));
        Place(panel.rectTransform, 0f, 0f, 1000f, 900f);
        SetRounded(panel);

        // 見出し
        var header = CreateImage(panel.transform, "Header", dark);
        Place(header.rectTransform, 0f, 385f, 1000f, 130f);

        var title = CreateLegacyText(header.transform, "Title", "ロビー", 66, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        title.color = accent;
        Place(title.rectTransform, 0f, 15f, 900f, 80f);
        AddShadow(title);

        var subtitle = CreateLegacyText(header.transform, "Subtitle", "ルームを作るか、コードを入れて参加しよう（最大 4 人）", 26, TextAnchor.MiddleCenter);
        subtitle.color = new Color(0.8f, 0.85f, 0.95f);
        Place(subtitle.rectTransform, 0f, -38f, 900f, 40f);

        // 接続方法の切り替え
        var internetTab = CreateButton(panel.transform, "InternetTab", "インターネット", 32, blue, out _);
        Place((RectTransform)internetTab.transform, -210f, 270f, 400f, 64f);
        var lanTab = CreateButton(panel.transform, "LanTab", "LAN（同じネットワーク）", 32, blue, out _);
        Place((RectTransform)lanTab.transform, 210f, 270f, 400f, 64f);

        // ルーム作成・参加
        var entry = CreateGroup(panel.transform, "Entry");

        var create = CreateButton(entry, "CreateButton", "ルームを作る（Host になる）", 40, blue, out _);
        Place((RectTransform)create.transform, 0f, 170f, 820f, 90f);

        var orText = CreateLegacyText(entry, "Or", "―――― または ――――", 26, TextAnchor.MiddleCenter);
        orText.color = new Color(0.6f, 0.65f, 0.75f);
        Place(orText.rectTransform, 0f, 100f, 820f, 40f);

        var input = CreateInput(entry, "CodeInput");
        Place((RectTransform)input.transform, -110f, 35f, 600f, 80f);

        var join = CreateButton(entry, "JoinButton", "参加", 40, blue, out _);
        Place((RectTransform)join.transform, 310f, 35f, 200f, 80f);

        // 接続後（コードの表示）
        var room = CreateGroup(panel.transform, "Room");

        var codeBack = CreateImage(room, "CodeBack", new Color(1f, 1f, 1f, 0.06f));
        Place(codeBack.rectTransform, 0f, 110f, 820f, 190f);
        SetRounded(codeBack);

        var codeText = CreateLegacyText(codeBack.transform, "CodeText", "", 34, TextAnchor.MiddleCenter);
        codeText.supportRichText = true;
        Place(codeText.rectTransform, 0f, 0f, 800f, 180f);

        var copy = CreateButton(codeBack.transform, "CopyButton", "コピー", 26, dark, out _);
        SetRect((RectTransform)copy.transform, new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(140f, 50f));

        var nextIp = CreateButton(codeBack.transform, "NextIpButton", "次のIP", 26, dark, out _);
        SetRect((RectTransform)nextIp.transform, new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(140f, 50f));

        // プレイヤー一覧（いつも出す）
        var playersTitle = CreateLegacyText(panel.transform, "PlayersTitle", "プレイヤー", 30, TextAnchor.MiddleLeft);
        playersTitle.fontStyle = FontStyle.Bold;
        Place(playersTitle.rectTransform, -200f, -40f, 420f, 44f);

        var players = CreateLegacyText(panel.transform, "PlayersText", "", 28, TextAnchor.MiddleRight);
        players.color = accent;
        Place(players.rectTransform, 200f, -40f, 420f, 44f);

        var slotsSo = new GAME_LOBBY.SLOT[4];

        for (int i = 0; i < 4; i++)
        {
            float x = -345f + i * 230f;

            var back = CreateImage(panel.transform, "Slot_P" + (i + 1), new Color(1f, 1f, 1f, 0.06f));
            Place(back.rectTransform, x, -145f, 215f, 150f);
            SetRounded(back);

            var strip = CreateImage(back.transform, "Strip", Color.white);
            SetRect(strip.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(215f, 12f));

            var name = CreateLegacyText(back.transform, "Name", "P" + (i + 1), 52, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            Place(name.rectTransform, 0f, 12f, 200f, 70f);
            AddShadow(name);

            var role = CreateLegacyText(back.transform, "Role", "募集中…", 24, TextAnchor.MiddleCenter);
            Place(role.rectTransform, 0f, -42f, 200f, 36f);

            var badge = CreateImage(back.transform, "You", accent);
            SetRect(badge.rectTransform, new Vector2(1f, 1f), new Vector2(8f, 12f), new Vector2(96f, 38f));
            SetRounded(badge);
            var badgeText = CreateLegacyText(badge.transform, "Text", "あなた", 22, TextAnchor.MiddleCenter);
            badgeText.color = new Color(0.1f, 0.1f, 0.1f);
            badgeText.fontStyle = FontStyle.Bold;
            Stretch(badgeText.rectTransform);
            badge.gameObject.SetActive(false);

            slotsSo[i] = new GAME_LOBBY.SLOT
            {
                m_back = back,
                m_strip = strip,
                m_nameText = name,
                m_roleText = role,
                m_youBadge = badge.gameObject
            };
        }

        // 開始（Host） / 待機（Client）
        var start = CreateButton(room, "StartButton", "ゲーム開始！", 48, accent, out Text startText);
        startText.color = new Color(0.12f, 0.1f, 0.05f);
        startText.fontStyle = FontStyle.Bold;
        Place((RectTransform)start.transform, 0f, -295f, 560f, 96f);

        var wait = CreateLegacyText(room, "WaitText", "Host の開始を待っています…", 36, TextAnchor.MiddleCenter);
        wait.color = accent;
        Place(wait.rectTransform, 0f, -295f, 820f, 96f);

        var leave = CreateButton(room, "LeaveButton", "キャンセル", 26, dark, out Text leaveText);
        Place((RectTransform)leave.transform, 0f, -375f, 300f, 52f);

        // 状態（澤田さんの通信のメッセージ。英語）
        var status = CreateLegacyText(panel.transform, "StatusText", "", 20, TextAnchor.MiddleCenter);
        status.color = new Color(0.6f, 0.65f, 0.75f);
        Place(status.rectTransform, 0f, -425f, 960f, 40f);

        room.gameObject.SetActive(false);

        var so = new SerializedObject(lobby);
        so.FindProperty("m_sync").objectReferenceValue = sync;
        so.FindProperty("m_panel").objectReferenceValue = dim.gameObject;
        so.FindProperty("m_internetTab").objectReferenceValue = internetTab;
        so.FindProperty("m_lanTab").objectReferenceValue = lanTab;
        so.FindProperty("m_entryGroup").objectReferenceValue = entry.gameObject;
        so.FindProperty("m_createButton").objectReferenceValue = create;
        so.FindProperty("m_codeInput").objectReferenceValue = input;
        so.FindProperty("m_joinButton").objectReferenceValue = join;
        so.FindProperty("m_roomGroup").objectReferenceValue = room.gameObject;
        so.FindProperty("m_codeText").objectReferenceValue = codeText;
        so.FindProperty("m_copyButton").objectReferenceValue = copy;
        so.FindProperty("m_nextIpButton").objectReferenceValue = nextIp;
        so.FindProperty("m_playersText").objectReferenceValue = players;
        so.FindProperty("m_startButton").objectReferenceValue = start;
        so.FindProperty("m_waitText").objectReferenceValue = wait;
        so.FindProperty("m_leaveButton").objectReferenceValue = leave;
        so.FindProperty("m_leaveText").objectReferenceValue = leaveText;
        so.FindProperty("m_statusText").objectReferenceValue = status;

        var slotsProp = so.FindProperty("m_slots");
        slotsProp.arraySize = 4;

        for (int i = 0; i < 4; i++)
        {
            var element = slotsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("m_back").objectReferenceValue = slotsSo[i].m_back;
            element.FindPropertyRelative("m_strip").objectReferenceValue = slotsSo[i].m_strip;
            element.FindPropertyRelative("m_nameText").objectReferenceValue = slotsSo[i].m_nameText;
            element.FindPropertyRelative("m_roleText").objectReferenceValue = slotsSo[i].m_roleText;
            element.FindPropertyRelative("m_youBadge").objectReferenceValue = slotsSo[i].m_youBadge;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    //==================================================
    // クエストの名前（シーンにあるミニゲームごと）
    //==================================================

    static void SetupQuests(GAME_QUEST_BOARD board)
    {
        var systems = new System.Collections.Generic.List<MiniGameSystem>();

        foreach (var zone in Object.FindObjectsByType<MiniGameZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (zone.System != null && !systems.Contains(zone.System))
            {
                systems.Add(zone.System);
            }
        }

        var so = new SerializedObject(board);
        var list = so.FindProperty("m_quests");
        list.arraySize = systems.Count;

        for (int i = 0; i < systems.Count; i++)
        {
            GetQuestText(systems[i].name, out string title, out string gameName, out Color color);

            var element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("m_system").objectReferenceValue = systems[i];
            element.FindPropertyRelative("m_title").stringValue = title;
            element.FindPropertyRelative("m_gameName").stringValue = gameName;
            element.FindPropertyRelative("m_color").colorValue = color;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("クエストに使うミニゲーム：" + systems.Count + " 種類（ゾーンから自動で取得）");
    }

    // ミニゲームの名前から、クエストの文章を決める（後から Inspector で変えてよい）
    static void GetQuestText(string systemName, out string title, out string gameName, out Color color)
    {
        string n = systemName;

        if (n.Contains("WoodChopping")) { title = "薪を割ってほしい！"; gameName = "薪割り"; color = new Color(1f, 0.6f, 0.2f); return; }
        if (n.Contains("Delivery")) { title = "急ぎの荷物を届けて！"; gameName = "特急デリバリー"; color = new Color(0.3f, 0.7f, 1f); return; }
        if (n.Contains("Cooking")) { title = "料理を手伝って！"; gameName = "神速クッキング"; color = new Color(1f, 0.4f, 0.4f); return; }
        if (n.Contains("Graffiti")) { title = "落書きを消して！"; gameName = "落書きクリーンアップ"; color = new Color(0.5f, 1f, 0.5f); return; }
        if (n.Contains("Salvage")) { title = "流された物を拾って！"; gameName = "漂流サルベージ"; color = new Color(0.3f, 0.9f, 0.9f); return; }
        if (n.Contains("Balloon")) { title = "風船を捕まえて！"; gameName = "突風バルーンキャッチ"; color = new Color(1f, 0.5f, 0.9f); return; }

        title = "クエスト";
        gameName = n.Replace("MiniGameSystem_", "");
        color = new Color(1f, 0.8f, 0.2f);
    }

    // 角を丸くする（Unity 標準の UI 画像を使う）
    static void SetRounded(Image image)
    {
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
    }

    static void AddShadow(Text text)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(2f, -2f);
    }

    static RectTransform CreateGroup(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        Stretch(rect);
        return rect;
    }

    static Text CreateLegacyText(Transform parent, string name, string text, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<Text>();
        label.font = s_font;
        label.text = text;
        label.fontSize = size;
        label.alignment = anchor;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    static Button CreateButton(Transform parent, string name, string text, int size, Color color, out Text label)
    {
        var image = CreateImage(parent, name, color);
        image.raycastTarget = true;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;

        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        // 押せるときは明るく、押せないときは暗く
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        button.colors = colors;

        label = CreateLegacyText(image.transform, "Text", text, size, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        return button;
    }

    static InputField CreateInput(Transform parent, string name)
    {
        var image = CreateImage(parent, name, Color.white);
        image.raycastTarget = true;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
        image.type = Image.Type.Sliced;

        var placeholder = CreateLegacyText(image.transform, "Placeholder", "ルームコードを入力", 30, TextAnchor.MiddleLeft);
        placeholder.color = new Color(0.45f, 0.45f, 0.45f);
        placeholder.fontStyle = FontStyle.Italic;
        Stretch(placeholder.rectTransform);
        placeholder.rectTransform.offsetMin = new Vector2(20f, 0f);
        placeholder.rectTransform.offsetMax = new Vector2(-20f, 0f);

        var text = CreateLegacyText(image.transform, "Text", "", 36, TextAnchor.MiddleLeft);
        text.color = Color.black;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(20f, 0f);
        text.rectTransform.offsetMax = new Vector2(-20f, 0f);

        var input = image.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = InputField.LineType.SingleLine;
        return input;
    }

    // 親の真ん中を基準に置く
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height));
    }

    //==================================================
    // 補助
    //==================================================

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
