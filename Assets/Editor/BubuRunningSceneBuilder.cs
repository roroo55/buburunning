using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BubuRunningSceneBuilder
{
    const string PrimaryScenePath = "Assets/Scenes/关卡1.unity";
    const string SceneSetupRequestPath = "Assets/Editor/CodexSceneSetupRequest.txt";
    const string Background1Path = "Assets/Art/background/background1.jpg";
    const string BackgroundPath = "Assets/Art/background/background2.jpg";
    const string PlayerPath = "Assets/Art/charactor/bubu.png";
    const string HuajiaoPath = "Assets/Art/charactor/huajiao.png";
    const string DoorPath = "Assets/Art/puzzles/door.png";
    const string HuajiaoName = "huajiao";
    const string HuajiaoVisualName = "huajiao visual";
    const string HuajiaoFailureRangeName = "Huajiao Failure Range";
    const string DoorName = "Door";
    const float HuajiaoVisualHeight = 2.8f;
    const float DoorStopPadding = 0.02f;

    static readonly string[] ScenePaths =
    {
        PrimaryScenePath,
    };

    [InitializeOnLoadMethod]
    static void RunRequestedSceneSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSceneSetup;
    }

    static void ProcessRequestedSceneSetup()
    {
        if (!SceneSetupRequested())
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ProcessRequestedSceneSetup;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ProcessRequestedSceneSetup;
            return;
        }

        EnsureSimplifiedPrototypeScenes();
        AssetDatabase.DeleteAsset(SceneSetupRequestPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Applied requested Bubu Running scene setup.");
    }

    static bool SceneSetupRequested()
    {
        return AssetDatabase.LoadAssetAtPath<TextAsset>(SceneSetupRequestPath) != null;
    }

    [MenuItem("Bubu Running/Ensure Simplified Prototype Scene")]
    public static void EnsureSimplifiedPrototypeScene()
    {
        EnsureSimplifiedPrototypeScenes();
    }

    public static void EnsureSimplifiedPrototypeScenes()
    {
        foreach (string scenePath in ScenePaths)
        {
            if (SceneAssetExists(scenePath))
            {
                EnsureSceneAtPath(scenePath);
            }
        }

        if (SceneAssetExists(PrimaryScenePath))
        {
            EditorSceneManager.OpenScene(PrimaryScenePath, OpenSceneMode.Single);
        }
    }

    public static void BuildFromCommandLine()
    {
        EnsureSimplifiedPrototypeScenes();
    }

    static bool SceneAssetExists(string scenePath)
    {
        return !string.IsNullOrEmpty(scenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;
    }

    static void EnsureSceneAtPath(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        RemoveLegacyMovementBounds();
        GameObject background = EnsureSceneBackground();
        EnsureMainCamera();
        GameObject player = EnsureScenePlayer(background);
        EnsureSceneSoldier(background);
        EnsureSceneHuajiao(background);
        EnsureWallColliders();
        EnsureSceneDoor();
        EnsureCameraController(background, player);
        EnsurePrototypeBootstrap();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Ensured simplified Bubu Running prototype scene at " + scenePath);
    }

    static void EnsureMainCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        AudioListener mainListener = mainCamera.GetComponent<AudioListener>();
        if (mainListener == null)
        {
            mainListener = mainCamera.gameObject.AddComponent<AudioListener>();
        }

        RemoveExtraAudioListeners(mainListener);

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = 4.5f;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
        mainCamera.transform.position = new Vector3(0f, 0f, -10f);
    }

    static void RemoveExtraAudioListeners(AudioListener listenerToKeep)
    {
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioListener listener in listeners)
        {
            if (listener == null || listener == listenerToKeep)
            {
                continue;
            }

            Object.DestroyImmediate(listener);
        }
    }

    static void EnsureCameraController(GameObject background, GameObject player)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        EdgeScrollCamera2D cameraController = mainCamera.GetComponent<EdgeScrollCamera2D>();
        if (cameraController == null)
        {
            cameraController = mainCamera.gameObject.AddComponent<EdgeScrollCamera2D>();
        }

        cameraController.background = background != null ? background.transform : null;
        cameraController.player = player != null ? player.transform : null;
        cameraController.mouseEdgeModeStartLeftEdgeX = 20f;
        cameraController.limitCameraUntilDoorPassed = true;
        cameraController.lockedCameraMaxPositionX = 20f;

        GameObject door = GameObject.Find(DoorName);
        cameraController.door = door != null ? door.transform : null;
        FitCameraToBackground(mainCamera, background);
    }

    static void FitCameraToBackground(Camera camera, GameObject background)
    {
        if (camera == null || background == null)
        {
            return;
        }

        Bounds bounds = GetCombinedSpriteBounds(background);
        if (bounds.size.y <= 0f)
        {
            return;
        }

        camera.orthographic = true;
        camera.orthographicSize = bounds.size.y * 0.5f;

        float halfWidth = camera.orthographicSize * camera.aspect;
        float cameraX = bounds.size.x <= halfWidth * 2f ? bounds.center.x : bounds.min.x + halfWidth;
        camera.transform.position = new Vector3(cameraX, bounds.center.y, -10f);
    }

    static GameObject EnsureSceneBackground()
    {
        Sprite background1Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Background1Path);
        Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        GameObject backgroundGroup = FindSceneObject(BubuRunningGame.BackgroundGroupName);

        if (backgroundGroup == null)
        {
            backgroundGroup = new GameObject(BubuRunningGame.BackgroundGroupName);
        }

        backgroundGroup.name = BubuRunningGame.BackgroundGroupName;
        backgroundGroup.transform.SetParent(null);
        backgroundGroup.transform.position = Vector3.zero;
        backgroundGroup.transform.localScale = Vector3.one;

        SpriteRenderer background1Renderer = EnsureBackgroundSegment(
            backgroundGroup.transform,
            BubuRunningGame.Background1Name,
            background1Sprite,
            -101);

        SpriteRenderer backgroundRenderer = EnsureBackgroundSegment(
            backgroundGroup.transform,
            BubuRunningGame.BackgroundName,
            backgroundSprite,
            -100);

        ArrangeBackgroundSegments(background1Renderer, backgroundRenderer);
        return backgroundGroup;
    }

    static SpriteRenderer EnsureBackgroundSegment(Transform parent, string segmentName, Sprite sprite, int sortingOrder)
    {
        GameObject segment = FindSceneObject(segmentName);
        if (segment == null)
        {
            segment = new GameObject(segmentName);
        }

        segment.name = segmentName;
        segment.transform.SetParent(parent, true);
        segment.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer = segment.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = segment.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = sortingOrder;
        return spriteRenderer;
    }

    static float PositionBackgroundSegment(SpriteRenderer spriteRenderer, float minX)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return minX;
        }

        Bounds spriteBounds = spriteRenderer.sprite.bounds;
        spriteRenderer.transform.position = new Vector3(
            minX - spriteBounds.min.x,
            -spriteBounds.center.y,
            1f);

        return minX + spriteBounds.size.x;
    }

    static void ArrangeBackgroundSegments(SpriteRenderer first, SpriteRenderer second)
    {
        float cursorX = 0f;
        cursorX = PositionBackgroundSegment(first, cursorX);
        PositionBackgroundSegment(second, cursorX);
    }

    static GameObject EnsureScenePlayer(GameObject background)
    {
        GameObject player = GameObject.Find(BubuRunningGame.PlayerRootName);
        if (player == null)
        {
            player = GameObject.Find(BubuRunningGame.LegacyPlayerRootName);
        }

        if (player == null)
        {
            player = new GameObject(BubuRunningGame.PlayerRootName);
        }

        player.name = BubuRunningGame.PlayerRootName;
        player.transform.SetParent(null);
        player.transform.localScale = Vector3.one;
        player.transform.position = GetScenePlayerStartPosition(background);

        RemoveRootPlayerComponents(player);
        ConfigureBubuVisual(player);
        ConfigurePlayerPhysics(player);
        return player;
    }

    static Vector3 GetScenePlayerStartPosition(GameObject background)
    {
        if (background == null)
        {
            return Vector3.zero;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return Vector3.zero;
        }

        Bounds bounds = GetCombinedSpriteBounds(background);
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
        {
            return Vector3.zero;
        }

        float startX = bounds.min.x + BubuRunningGame.PlayerWidth * 0.5f;
        startX = Mathf.Clamp(startX, bounds.min.x + BubuRunningGame.PlayerWidth * 0.5f, bounds.max.x - BubuRunningGame.PlayerWidth * 0.5f);
        float startY = Mathf.Clamp(bounds.center.y, bounds.min.y + BubuRunningGame.PlayerHeight * 0.5f, bounds.max.y - BubuRunningGame.PlayerHeight * 0.5f);
        return new Vector3(startX, startY, 0f);
    }

    static void RemoveRootPlayerComponents(GameObject player)
    {
        SpriteRenderer rootRenderer = player.GetComponent<SpriteRenderer>();
        if (rootRenderer != null)
        {
            Object.DestroyImmediate(rootRenderer);
        }

        BoxCollider2D rootCollider = player.GetComponent<BoxCollider2D>();
        if (rootCollider != null)
        {
            Object.DestroyImmediate(rootCollider);
        }
    }

    static void ConfigureBubuVisual(GameObject player)
    {
        Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerPath);
        Transform visualTransform = player.transform.Find(BubuRunningGame.PlayerVisualName);
        GameObject visual = visualTransform != null ? visualTransform.gameObject : new GameObject(BubuRunningGame.PlayerVisualName);
        visual.transform.SetParent(player.transform, false);

        SpriteRenderer spriteRenderer = visual.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = playerSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 10;

        BoxCollider2D boxCollider = visual.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = visual.AddComponent<BoxCollider2D>();
        }

        boxCollider.isTrigger = false;

        if (playerSprite == null)
        {
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(BubuRunningGame.PlayerWidth, BubuRunningGame.PlayerHeight, 1f);
            boxCollider.size = Vector2.one;
            boxCollider.offset = Vector2.zero;
            return;
        }

        boxCollider.size = playerSprite.bounds.size;
        boxCollider.offset = playerSprite.bounds.center;

        float spriteHeight = Mathf.Max(playerSprite.bounds.size.y, 0.001f);
        float visualScale = BubuRunningGame.PlayerHeight / spriteHeight;
        visual.transform.localScale = Vector3.one * visualScale;
        visual.transform.localPosition = -playerSprite.bounds.center * visualScale;
    }

    static void ConfigurePlayerPhysics(GameObject player)
    {
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody == null)
        {
            playerBody = player.AddComponent<Rigidbody2D>();
        }

        playerBody.bodyType = RigidbodyType2D.Dynamic;
        playerBody.simulated = true;
        playerBody.gravityScale = 0f;
        playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    static void EnsureWallColliders()
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in objects)
        {
            if (!sceneObject.name.ToLowerInvariant().Contains("wall"))
            {
                continue;
            }

            SpriteRenderer spriteRenderer = sceneObject.GetComponent<SpriteRenderer>();
            BoxCollider2D boxCollider = sceneObject.GetComponent<BoxCollider2D>();
            if (spriteRenderer == null || boxCollider != null)
            {
                continue;
            }

            boxCollider = sceneObject.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = false;
            boxCollider.offset = Vector2.zero;
            boxCollider.size = spriteRenderer.size;
        }
    }

    static GameObject EnsureSceneDoor()
    {
        Sprite doorSprite = LoadLargestSpriteAtPath(DoorPath);
        GameObject door = GameObject.Find(DoorName);
        if (door == null)
        {
            return null;
        }

        ConfigureDoorVisual(door, doorSprite);
        ConfigureDoorCollider(door, doorSprite);
        ConfigureDoorProgressGate(door);
        return door;
    }

    static void ConfigureDoorVisual(GameObject door, Sprite doorSprite)
    {
        SpriteRenderer spriteRenderer = door.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = door.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = doorSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 8;
    }

    static void ConfigureDoorCollider(GameObject door, Sprite doorSprite)
    {
        BoxCollider2D boxCollider = door.GetComponent<BoxCollider2D>();
        bool createdCollider = boxCollider == null;
        if (boxCollider == null)
        {
            boxCollider = door.AddComponent<BoxCollider2D>();
        }

        boxCollider.isTrigger = false;
        if (createdCollider)
        {
            if (doorSprite != null)
            {
                boxCollider.size = doorSprite.bounds.size;
                boxCollider.offset = doorSprite.bounds.center;
            }
            else
            {
                boxCollider.size = Vector2.one;
                boxCollider.offset = Vector2.zero;
            }
        }
    }

    static void ConfigureDoorProgressGate(GameObject door)
    {
        DoorProgressGate2D gate = door.GetComponent<DoorProgressGate2D>();
        if (gate == null)
        {
            gate = door.AddComponent<DoorProgressGate2D>();
            gate.blocksPlayerProgress = true;
            gate.puzzleSolved = false;
            gate.disableColliderWhenSolved = true;
            gate.stopPadding = DoorStopPadding;
        }
    }

    static GameObject EnsureSceneSoldier(GameObject background)
    {
        GameObject soldier = GameObject.Find(BubuRunningGame.SoldierRootName);
        if (soldier == null)
        {
            soldier = GameObject.Find(BubuRunningGame.LegacySoldierRootName);
        }

        if (soldier == null)
        {
            soldier = new GameObject(BubuRunningGame.SoldierRootName);
            soldier.transform.position = GetSceneSoldierStartPosition(background);
            soldier.transform.localScale = new Vector3(BubuRunningGame.SoldierWidth, BubuRunningGame.SoldierHeight, 1f);
        }

        soldier.name = BubuRunningGame.SoldierRootName;
        soldier.transform.SetParent(EnsureSoldierGroup().transform, true);
        ConfigureSoldierVisual(soldier);
        ConfigureSoldierCollider(soldier);
        ConfigureSoldierPatrol(soldier);
        GroupExistingPatrollingSoldiers();
        return soldier;
    }

    static Vector3 GetSceneSoldierStartPosition(GameObject background)
    {
        if (background == null)
        {
            return new Vector3(15f, 0f, 0f);
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return new Vector3(15f, 0f, 0f);
        }

        Bounds bounds = GetCombinedSpriteBounds(background);
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
        {
            return new Vector3(15f, 0f, 0f);
        }

        float halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        float x = Mathf.Clamp(bounds.min.x + halfWidth + 5f, bounds.min.x + BubuRunningGame.SoldierWidth, bounds.max.x - BubuRunningGame.SoldierWidth);
        return new Vector3(x, bounds.center.y, 0f);
    }

    static void ConfigureSoldierVisual(GameObject soldier)
    {
        SpriteRenderer spriteRenderer = soldier.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = soldier.AddComponent<SpriteRenderer>();
        }

        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = FindWallSprite();
        }

        spriteRenderer.color = new Color(0.15f, 0.18f, 0.22f);
        spriteRenderer.sortingOrder = 11;
    }

    static Sprite FindWallSprite()
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in objects)
        {
            if (!sceneObject.name.ToLowerInvariant().Contains("wall"))
            {
                continue;
            }

            SpriteRenderer spriteRenderer = sceneObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer.sprite;
            }
        }

        return null;
    }

    static void ConfigureSoldierCollider(GameObject soldier)
    {
        BoxCollider2D boxCollider = soldier.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = soldier.AddComponent<BoxCollider2D>();
        }

        boxCollider.isTrigger = false;
        boxCollider.offset = Vector2.zero;
        boxCollider.size = Vector2.one;
    }

    static void ConfigureSoldierPatrol(GameObject soldier)
    {
        PatrollingSoldier2D patrol = soldier.GetComponent<PatrollingSoldier2D>();
        bool createdPatrol = patrol == null;
        if (patrol == null)
        {
            patrol = soldier.AddComponent<PatrollingSoldier2D>();
        }

        if (createdPatrol)
        {
            patrol.patrolSpeed = BubuRunningGame.SoldierPatrolSpeed;
            patrol.patrolHighestOffsetY = 2.3f;
            patrol.patrolLowestOffsetY = -2.3f;
            patrol.startMovingUp = false;
        }

        if (patrol.failureRangePadding == Vector2.zero)
        {
            patrol.failureRangePadding = new Vector2(0.8f, 0.8f);
        }

        ConfigureSoldierFailureRange(soldier, patrol);
    }

    static void ConfigureSoldierFailureRange(GameObject soldier, PatrollingSoldier2D patrol)
    {
        const string failureRangeName = "Soldier Failure Range";
        Transform rangeTransform = soldier.transform.Find(failureRangeName);
        GameObject range = rangeTransform != null ? rangeTransform.gameObject : new GameObject(failureRangeName);
        range.transform.SetParent(soldier.transform, false);
        range.transform.localRotation = Quaternion.identity;
        range.transform.localPosition = Vector3.zero;
        range.transform.localScale = Vector3.one;

        BoxCollider2D soldierCollider = soldier.GetComponent<BoxCollider2D>();
        Vector2 bodySize = soldierCollider != null ? soldierCollider.size : Vector2.one;
        Vector2 bodyOffset = soldierCollider != null ? soldierCollider.offset : Vector2.zero;

        BoxCollider2D rangeCollider = range.GetComponent<BoxCollider2D>();
        if (rangeCollider == null)
        {
            rangeCollider = range.AddComponent<BoxCollider2D>();
        }

        rangeCollider.isTrigger = true;
        rangeCollider.offset = bodyOffset + patrol.failureRangeOffset;
        rangeCollider.size = bodySize + patrol.failureRangePadding * 2f;

        SoldierFailureRange2D failureRange = range.GetComponent<SoldierFailureRange2D>();
        if (failureRange == null)
        {
            failureRange = range.AddComponent<SoldierFailureRange2D>();
        }

        failureRange.owner = patrol;
    }

    static GameObject EnsureSoldierGroup()
    {
        GameObject group = GameObject.Find(BubuRunningGame.SoldierGroupName);
        if (group == null)
        {
            group = new GameObject(BubuRunningGame.SoldierGroupName);
        }

        group.name = BubuRunningGame.SoldierGroupName;
        group.transform.SetParent(null);
        group.transform.position = Vector3.zero;
        group.transform.localScale = Vector3.one;
        return group;
    }

    static void GroupExistingPatrollingSoldiers()
    {
        GameObject group = EnsureSoldierGroup();
        PatrollingSoldier2D[] patrols = Object.FindObjectsByType<PatrollingSoldier2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (PatrollingSoldier2D patrol in patrols)
        {
            if (patrol == null)
            {
                continue;
            }

            GameObject soldier = patrol.gameObject;
            ConfigureSoldierVisual(soldier);
            ConfigureSoldierCollider(soldier);
            ConfigureSoldierPatrol(soldier);
            soldier.transform.SetParent(group.transform, true);
        }
    }

    static GameObject EnsureSceneHuajiao(GameObject background)
    {
        GameObject huajiao = GameObject.Find(HuajiaoName);
        bool created = huajiao == null;
        if (created)
        {
            huajiao = new GameObject(HuajiaoName);
            huajiao.transform.position = GetSceneHuajiaoStartPosition(background);
        }

        huajiao.name = HuajiaoName;
        huajiao.transform.SetParent(null);
        huajiao.transform.localScale = Vector3.one;

        ConfigureHuajiaoVisual(huajiao);
        ConfigureHuajiaoFailureRange(huajiao);
        ConfigureHuajiaoMovement(huajiao);
        return huajiao;
    }

    static Vector3 GetSceneHuajiaoStartPosition(GameObject background)
    {
        Camera mainCamera = Camera.main;
        float y = -1.2f;
        if (background != null)
        {
            Bounds bounds = GetCombinedSpriteBounds(background);
            if (bounds.size.y > 0f)
            {
                y = Mathf.Clamp(bounds.center.y - 1.2f, bounds.min.y + 1.4f, bounds.max.y - 1.4f);
            }
        }

        if (mainCamera == null)
        {
            return new Vector3(-2.6f, y, 0f);
        }

        float halfWidth = mainCamera.orthographicSize * mainCamera.aspect;
        return new Vector3(mainCamera.transform.position.x - halfWidth - 2.6f, y, 0f);
    }

    static void ConfigureHuajiaoVisual(GameObject huajiao)
    {
        Sprite huajiaoSprite = LoadLargestSpriteAtPath(HuajiaoPath);
        Transform visualTransform = huajiao.transform.Find(HuajiaoVisualName);
        GameObject visual = visualTransform != null ? visualTransform.gameObject : new GameObject(HuajiaoVisualName);
        visual.transform.SetParent(huajiao.transform, false);

        SpriteRenderer spriteRenderer = visual.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = huajiaoSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = 9;

        if (huajiaoSprite == null)
        {
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = Vector3.one;
            return;
        }

        float spriteHeight = Mathf.Max(huajiaoSprite.bounds.size.y, 0.001f);
        float visualScale = HuajiaoVisualHeight / spriteHeight;
        visual.transform.localScale = Vector3.one * visualScale;
        visual.transform.localPosition = -huajiaoSprite.bounds.center * visualScale;
    }

    static void ConfigureHuajiaoFailureRange(GameObject huajiao)
    {
        Transform rangeTransform = huajiao.transform.Find(HuajiaoFailureRangeName);
        GameObject range = rangeTransform != null ? rangeTransform.gameObject : new GameObject(HuajiaoFailureRangeName);
        range.transform.SetParent(huajiao.transform, false);
        range.transform.localRotation = Quaternion.identity;
        range.transform.localScale = Vector3.one;
        range.transform.localPosition = new Vector3(2.6f, 0f, 0f);

        BoxCollider2D boxCollider = range.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = range.AddComponent<BoxCollider2D>();
        }

        boxCollider.isTrigger = true;
        boxCollider.offset = Vector2.zero;
        boxCollider.size = new Vector2(1.2f, 2.8f);
    }

    static void ConfigureHuajiaoMovement(GameObject huajiao)
    {
        HuajiaoMovement movement = huajiao.GetComponent<HuajiaoMovement>();
        if (movement == null)
        {
            movement = huajiao.AddComponent<HuajiaoMovement>();
        }

        movement.moveSpeed = 0.5f;
        movement.startOutsideLeftPadding = 0.5f;
        movement.failureRangeSize = new Vector2(1.2f, 2.8f);
        movement.failureRangeGap = 0.1f;
        movement.failureRangeOffsetY = 0f;
        movement.startOutsideCameraLeft = true;
        movement.playerObjectName = BubuRunningGame.PlayerRootName;
    }

    static Sprite LoadLargestSpriteAtPath(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Sprite largestSprite = null;
        float largestArea = -1f;

        foreach (Object asset in assets)
        {
            if (asset is not Sprite sprite)
            {
                continue;
            }

            float area = sprite.rect.width * sprite.rect.height;
            if (area > largestArea)
            {
                largestArea = area;
                largestSprite = sprite;
            }
        }

        return largestSprite;
    }

    static void EnsurePrototypeBootstrap()
    {
        BubuRunningGame existingPrototype = Object.FindFirstObjectByType<BubuRunningGame>();
        if (existingPrototype != null)
        {
            existingPrototype.gameObject.name = "Bubu Running Prototype";
            AssignPrototypeSprites(existingPrototype);
            return;
        }

        GameObject bootstrap = new GameObject("Bubu Running Prototype");
        AssignPrototypeSprites(bootstrap.AddComponent<BubuRunningGame>());
    }

    static void AssignPrototypeSprites(BubuRunningGame prototype)
    {
        Sprite background1Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Background1Path);
        Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        Sprite playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerPath);
        SerializedObject serializedPrototype = new SerializedObject(prototype);
        AssignSprite(serializedPrototype, "background1Sprite", background1Sprite);
        AssignSprite(serializedPrototype, "backgroundSprite", backgroundSprite);
        AssignSprite(serializedPrototype, "playerSprite", playerSprite);
        serializedPrototype.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssignSprite(SerializedObject serializedObject, string propertyName, Sprite sprite)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = sprite;
        }
    }

    static void RemoveLegacyMovementBounds()
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in objects)
        {
            if (IsLegacyMovementBoundsObject(sceneObject.name))
            {
                Object.DestroyImmediate(sceneObject);
            }
        }
    }

    static bool IsLegacyMovementBoundsObject(string objectName)
    {
        return objectName == "Movement Bounds"
            || objectName == "Visible Frame"
            || objectName == "Left Edge"
            || objectName == "Right Edge"
            || objectName == "Top Edge"
            || objectName == "Bottom Edge"
            || objectName == "Edge"
            || objectName == "Edge (1)";
    }

    static Bounds GetCombinedSpriteBounds(GameObject root)
    {
        if (root == null)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        SpriteRenderer[] spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = spriteRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(spriteRenderer.bounds);
            }
        }

        return bounds;
    }

    static GameObject FindSceneObject(string objectName)
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in objects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }
}
