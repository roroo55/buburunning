using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class MazePuzzleSceneSetup
{
    const string SetupVersion = "key-lock-maze-20260730a";
    const string RequestPath = "Assets/Editor/CodexMazePuzzleSetupRequest.txt";
    const string ControllerName = "Maze Puzzle Controller";
    const string MazeName = "maze";
    const string KeyForMazeName = "key for maze";
    const string HiddenKeyName = "key";
    const string KeySpritePath = "Assets/Art/puzzles/door key.png";
    const string LockName = "lock";
    const string LockSpritePath = "Assets/Art/puzzles/lock.png";
    const string LockVisualName = "Lock Visual";
    const string DoorName = "Door";
    const string PenzaiSearchControllerName = "Penzai Search Controller";
    const string MazeCollidersName = "Maze Colliders";
    const string KeyWallRootName = "KeyWalls";
    const string TilemapWallPrefabPath = "Assets/TileMap/colliders.prefab";
    const string TilemapWallRootName = "Maze Tilemap Colliders";
    const string MazeKeyStartName = "Maze Key Start";
    const string MazeExitName = "Maze Exit";
    const string MazeKeyVisualName = "Maze Key Visual";
    const string MazeKeyColliderName = "Maze Key 2D Collider";
    const string MazeKeyControlPointName = "Maze Key Control Point";
    const string MazeKeyLayerName = "MazeKey";
    const string MazeKeyWallLayerName = "MazeKeyWall";
    const float ControlPointWorldRadius = 0.045f;

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Maze Puzzle Visibility")]
    public static void SetupMazePuzzleVisibilityFromMenu()
    {
        SetupActiveScene();
    }

    static void ProcessRequestedSetup()
    {
        if (!SetupRequested())
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ProcessRequestedSetup;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessRequestedSetup;
            return;
        }

        try
        {
            SetupActiveScene();
            DeleteSetupRequest();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.delayCall += ProcessRequestedSetup;
        }
    }

    static bool SetupRequested()
    {
        return File.Exists(GetProjectRelativeAbsolutePath(RequestPath))
            || AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) != null;
    }

    static void DeleteSetupRequest()
    {
        bool deletedByAssetDatabase = AssetDatabase.DeleteAsset(RequestPath);
        if (deletedByAssetDatabase)
        {
            return;
        }

        string requestPath = GetProjectRelativeAbsolutePath(RequestPath);
        if (File.Exists(requestPath))
        {
            File.Delete(requestPath);
        }

        string metaPath = requestPath + ".meta";
        if (File.Exists(metaPath))
        {
            File.Delete(metaPath);
        }
    }

    static string GetProjectRelativeAbsolutePath(string projectRelativePath)
    {
        return Path.Combine(Directory.GetCurrentDirectory(), projectRelativePath);
    }

    static void SetupActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogWarning("Maze puzzle visibility setup skipped because there is no valid active scene.");
            return;
        }

        GameObject controllerObject = FindSceneObjectByExactName(ControllerName);
        if (controllerObject == null)
        {
            controllerObject = new GameObject(ControllerName);
        }

        MazePuzzleController controller = controllerObject.GetComponent<MazePuzzleController>();
        if (controller == null)
        {
            controller = controllerObject.AddComponent<MazePuzzleController>();
        }

        GameObject mazeObject = FindSceneObjectByExactName(MazeName);
        GameObject keyObject = FindSceneObjectByExactName(KeyForMazeName);
        EnsureKeySprite(FindSceneObjectByExactName(HiddenKeyName), mazeObject);

        controller.mazeRoot = mazeObject;
        controller.keyForMaze = keyObject;
        controller.showMazeOnStart = false;
        controller.prerequisiteMet = false;
        controller.restoreKeyComponentsWhenShown = true;
        controller.resetKeyToStartWhenShown = false;
        controller.unlockDoorWhenSolved = true;
        controller.hideMazeWhenSolved = true;
        controller.mazeSolved = false;

        EnsureMazeDragMiniGame(controller, mazeObject, keyObject);
        HideAllSceneTilemapRenderers();
        DisableAllSceneTilemapColliders();
        SetActiveAndDirty(mazeObject, false);
        SetActiveAndDirty(keyObject, false);
        EnsureDoorMazePuzzleTrigger(controller);

        EditorUtility.SetDirty(controllerObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("Configured maze puzzle visibility. Maze starts hidden until MazePuzzleController.ShowMaze() is called.");
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }

    static void EnsureDoorMazePuzzleTrigger(MazePuzzleController mazeController)
    {
        GameObject doorObject = FindSceneObjectByExactName(DoorName);
        if (doorObject == null)
        {
            Debug.LogWarning("Door maze puzzle trigger setup skipped because Door was not found.");
            return;
        }

        DoorMazePuzzleTrigger2D trigger = doorObject.GetComponent<DoorMazePuzzleTrigger2D>();
        if (trigger == null)
        {
            trigger = doorObject.AddComponent<DoorMazePuzzleTrigger2D>();
        }

        GameObject playerObject = FindSceneObjectByExactName(BubuRunningGame.PlayerRootName);
        GameObject searchControllerObject = FindSceneObjectByExactName(PenzaiSearchControllerName);

        trigger.playerObjectName = BubuRunningGame.PlayerRootName;
        trigger.requiredItemName = "key";
        trigger.interactionPadding = Mathf.Max(trigger.interactionPadding, 0.12f);
        trigger.fallbackInteractionDistance = Mathf.Max(trigger.fallbackInteractionDistance, 1.5f);
        trigger.requireKey = true;
        trigger.player = playerObject != null ? playerObject.transform : null;
        trigger.doorCollider = doorObject.GetComponent<Collider2D>();
        trigger.playerCollider = playerObject != null ? playerObject.GetComponentInChildren<Collider2D>(true) : null;
        trigger.searchController = searchControllerObject != null ? searchControllerObject.GetComponent<PenzaiSearchController2D>() : null;
        trigger.mazePuzzleController = mazeController;

        EditorUtility.SetDirty(doorObject);
        EditorUtility.SetDirty(trigger);
    }

    static void EnsureMazeDragMiniGame(MazePuzzleController controller, GameObject mazeObject, GameObject keyObject)
    {
        if (mazeObject == null || keyObject == null)
        {
            Debug.LogWarning("Maze drag setup skipped because maze or key for maze was not found.");
            return;
        }

        Transform generatedWallRoot = FindChildByExactName(mazeObject.transform, MazeCollidersName);
        if (generatedWallRoot == null)
        {
            GameObject wallRootObject = FindSceneObjectByExactName(MazeCollidersName);
            generatedWallRoot = wallRootObject != null ? wallRootObject.transform : null;
        }

        Transform keyWallRoot = FindChildByExactName(mazeObject.transform, KeyWallRootName);
        if (keyWallRoot == null)
        {
            GameObject keyWallObject = FindSceneObjectByExactName(KeyWallRootName);
            keyWallRoot = keyWallObject != null ? keyWallObject.transform : null;
        }

        if (keyWallRoot == null)
        {
            Debug.LogWarning("Maze drag setup did not find KeyWalls. Key wall collisions need a KeyWalls object with Collider2D children.");
        }
        else
        {
            ConfigureDedicatedCollisionLayers(keyObject.transform, keyWallRoot);
            ConfigureKeyWallSurface(keyWallRoot);
        }

        if (generatedWallRoot != null && generatedWallRoot != keyWallRoot)
        {
            generatedWallRoot.gameObject.SetActive(true);
            ConfigureDedicatedCollisionLayers(
                keyObject.transform,
                generatedWallRoot);
            ConfigureKeyWallSurface(generatedWallRoot);
            EditorUtility.SetDirty(generatedWallRoot.gameObject);
        }

        Bounds mazeBounds = GetMazeWorldBounds(mazeObject);
        Vector3 oldExitPosition = GetExistingMarkerWorldPosition(mazeObject.transform, MazeExitName, GetDefaultLockPosition(mazeBounds, mazeObject.transform.position.z - 0.2f));
        DeleteOldMazeMarker(mazeObject.transform, MazeKeyStartName);
        DeleteOldMazeMarker(mazeObject.transform, MazeExitName);

        Sprite keySprite = EnsureKeySprite(keyObject, mazeObject);
        Collider2D keyCollider = EnsureKeyCollisionCollider(keyObject, keySprite);
        CircleCollider2D controlPointCollider = EnsureKeyControlPoint(keyObject);
        GameObject lockObject = EnsureLockObject(mazeObject, oldExitPosition);
        Collider2D lockCollider = lockObject != null ? lockObject.GetComponent<Collider2D>() : null;

        if (keyCollider == null || controlPointCollider == null)
        {
            Debug.LogWarning("Maze drag setup skipped because key for maze is missing a collision or control point Collider2D.");
            return;
        }

        DisableLegacyKeyColliders(keyObject, keyCollider, controlPointCollider);

        MazeKeyDrag2D keyDrag = keyObject.GetComponent<MazeKeyDrag2D>();
        if (keyDrag == null)
        {
            keyDrag = keyObject.AddComponent<MazeKeyDrag2D>();
        }

        Rigidbody2D keyBody = ConfigureKeyPhysics(keyObject, out TargetJoint2D dragJoint);

        keyDrag.worldCamera = Camera.main;
        keyDrag.keyBody = keyBody;
        keyDrag.dragJoint = dragJoint;
        keyDrag.keyCollider = keyCollider;
        keyDrag.controlPoint = controlPointCollider.transform;
        keyDrag.controlPointCollider = controlPointCollider;
        keyDrag.keyStartPoint = null;
        keyDrag.wallRoot = keyWallRoot != null ? keyWallRoot : generatedWallRoot;
        keyDrag.mazeBoundsRoot = mazeObject.transform;
        keyDrag.mazeBoundsCollider = null;
        keyDrag.lockObject = lockObject != null ? lockObject.transform : null;
        keyDrag.lockCollider = lockCollider;
        keyDrag.exitCollider = lockCollider;
        keyDrag.exitZone = null;
        keyDrag.mazePuzzleController = controller;
        keyDrag.constrainToMazeBounds = true;
        keyDrag.mazeBoundsExtraReach = new Vector2(
            Mathf.Max(keyDrag.mazeBoundsExtraReach.x, 0.35f),
            Mathf.Max(keyDrag.mazeBoundsExtraReach.y, 0.35f));
        keyDrag.pickFallbackRadius = Mathf.Max(keyDrag.pickFallbackRadius, 0.35f);
        keyDrag.lockReachPadding = Mathf.Max(keyDrag.lockReachPadding, 0.08f);
        keyDrag.useCollisionPointForLockCheck = true;
        keyDrag.alsoCheckDragControlForLock = true;
        keyDrag.maxFollowSpeed = Mathf.Max(keyDrag.maxFollowSpeed, 5f);
        keyDrag.jointMaxForce = Mathf.Max(keyDrag.jointMaxForce, 80f);
        keyDrag.jointFrequency = Mathf.Max(keyDrag.jointFrequency, 12f);
        keyDrag.jointDampingRatio = Mathf.Clamp(keyDrag.jointDampingRatio, 0.85f, 1f);
        keyDrag.bodyLinearDamping = Mathf.Max(keyDrag.bodyLinearDamping, 1.5f);
        keyDrag.stopImmediatelyOnRelease = true;
        keyDrag.mergeWallCollidersAtRuntime = true;
        keyDrag.useLowFrictionMaterial = true;
        keyDrag.resetToStartOnEnable = false;

        controller.keyStartPoint = null;
        controller.mazeKeyDrag = keyDrag;
        controller.doorGate = FindDoorGate();

        if (lockObject != null)
        {
            EditorUtility.SetDirty(lockObject);
        }

        if (lockCollider != null)
        {
            EditorUtility.SetDirty(lockCollider);
        }

        EditorUtility.SetDirty(keyObject);
        EditorUtility.SetDirty(keyCollider.gameObject);
        EditorUtility.SetDirty(keyCollider);
        EditorUtility.SetDirty(controlPointCollider.gameObject);
        EditorUtility.SetDirty(controlPointCollider);
        EditorUtility.SetDirty(keyBody);
        EditorUtility.SetDirty(dragJoint);
        EditorUtility.SetDirty(keyDrag);
    }

    static Transform EnsureTilemapWallRoot(GameObject mazeObject)
    {
        if (mazeObject == null)
        {
            return null;
        }

        Transform tilemapRoot = FindLargestSceneTilemapRoot();

        if (tilemapRoot == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TilemapWallPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("Tilemap maze wall setup skipped because Assets/TileMap/colliders.prefab was not found.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mazeObject.scene);
            tilemapRoot = instance.transform;
            tilemapRoot.name = TilemapWallRootName;
            tilemapRoot.SetParent(mazeObject.transform, false);
            tilemapRoot.localPosition = Vector3.zero;
            tilemapRoot.localRotation = Quaternion.identity;
            tilemapRoot.localScale = Vector3.one;
            Debug.Log("Created Maze Tilemap Colliders from Assets/TileMap/colliders.prefab.");
        }
        else if (tilemapRoot.parent != mazeObject.transform)
        {
            tilemapRoot.SetParent(mazeObject.transform, true);
        }

        tilemapRoot.gameObject.SetActive(true);
        ConfigureTilemapWallColliders(tilemapRoot, mazeObject);
        EditorUtility.SetDirty(tilemapRoot.gameObject);
        return tilemapRoot;
    }

    static Transform FindLargestSceneTilemapRoot()
    {
        Tilemap bestTilemap = null;
        int bestTileCount = -1;

        GameObject[] sceneObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject == null)
            {
                continue;
            }

            Tilemap tilemap = sceneObject.GetComponent<Tilemap>();
            if (tilemap == null)
            {
                continue;
            }

            int tileCount = CountTiles(tilemap);
            if (tileCount > bestTileCount)
            {
                bestTileCount = tileCount;
                bestTilemap = tilemap;
            }
        }

        if (bestTilemap == null)
        {
            return null;
        }

        Grid parentGrid = bestTilemap.GetComponentInParent<Grid>();
        return parentGrid != null ? parentGrid.transform : bestTilemap.transform;
    }

    static int CountTiles(Tilemap tilemap)
    {
        if (tilemap == null)
        {
            return 0;
        }

        int count = 0;
        BoundsInt cellBounds = tilemap.cellBounds;
        foreach (Vector3Int cellPosition in cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cellPosition))
            {
                count++;
            }
        }

        return count;
    }

    static void ConfigureTilemapWallColliders(Transform tilemapRoot, GameObject mazeObject)
    {
        if (tilemapRoot == null)
        {
            return;
        }

        Tilemap[] tilemaps = tilemapRoot.GetComponentsInChildren<Tilemap>(true);
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null)
            {
                continue;
            }

            tilemap.CompressBounds();

            TilemapRenderer tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
            if (tilemapRenderer != null)
            {
                tilemapRenderer.enabled = false;
                EditorUtility.SetDirty(tilemapRenderer);
            }

            TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
            if (tilemapCollider == null)
            {
                tilemapCollider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
            }

            tilemapCollider.isTrigger = false;
            tilemapCollider.enabled = true;
            EditorUtility.SetDirty(tilemap);
            EditorUtility.SetDirty(tilemapCollider);
        }

        if (tilemaps.Length == 0)
        {
            Debug.LogWarning("Tilemap maze wall setup found no Tilemap component under " + tilemapRoot.name + ".");
        }
    }

    static void HideAllSceneTilemapRenderers()
    {
        TilemapRenderer[] renderers = Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TilemapRenderer tilemapRenderer in renderers)
        {
            if (tilemapRenderer == null)
            {
                continue;
            }

            tilemapRenderer.enabled = false;
            EditorUtility.SetDirty(tilemapRenderer);
        }
    }

    static void DisableAllSceneTilemapColliders()
    {
        TilemapCollider2D[] tilemapColliders = Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TilemapCollider2D tilemapCollider in tilemapColliders)
        {
            if (tilemapCollider == null)
            {
                continue;
            }

            tilemapCollider.enabled = false;
            EditorUtility.SetDirty(tilemapCollider);
        }
    }

    static void ConfigureKeyWallSurface(Transform keyWallRoot)
    {
        if (keyWallRoot == null)
        {
            return;
        }

        Rigidbody2D wallBody = keyWallRoot.GetComponent<Rigidbody2D>();
        if (wallBody == null)
        {
            wallBody = keyWallRoot.gameObject.AddComponent<Rigidbody2D>();
        }

        wallBody.bodyType = RigidbodyType2D.Static;
        wallBody.simulated = true;

        CompositeCollider2D compositeCollider = keyWallRoot.GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
        {
            compositeCollider = keyWallRoot.gameObject.AddComponent<CompositeCollider2D>();
        }

        compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
        compositeCollider.generationType = CompositeCollider2D.GenerationType.Synchronous;
        compositeCollider.isTrigger = false;

        Collider2D[] sourceColliders = keyWallRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D sourceCollider in sourceColliders)
        {
            if (sourceCollider == null || sourceCollider == compositeCollider)
            {
                continue;
            }

            sourceCollider.enabled = true;
            sourceCollider.isTrigger = false;
            sourceCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            EditorUtility.SetDirty(sourceCollider);
        }

        compositeCollider.GenerateGeometry();
        EditorUtility.SetDirty(keyWallRoot.gameObject);
        EditorUtility.SetDirty(wallBody);
        EditorUtility.SetDirty(compositeCollider);
    }

    static void ConfigureDedicatedCollisionLayers(Transform keyRoot, Transform keyWallRoot)
    {
        int keyLayer = EnsurePhysicsLayer(MazeKeyLayerName);
        int wallLayer = EnsurePhysicsLayer(MazeKeyWallLayerName);
        if (keyLayer < 0 || wallLayer < 0)
        {
            Debug.LogError("Maze key collision setup needs two available user layers.");
            return;
        }

        SetLayerRecursively(keyRoot, keyLayer);
        SetLayerRecursively(keyWallRoot, wallLayer);

        for (int layer = 0; layer < 32; layer++)
        {
            Physics2D.IgnoreLayerCollision(keyLayer, layer, layer != wallLayer);
        }
    }

    static int EnsurePhysicsLayer(string layerName)
    {
        int existingLayer = LayerMask.NameToLayer(layerName);
        if (existingLayer >= 0)
        {
            return existingLayer;
        }

        Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets == null || tagManagerAssets.Length == 0)
        {
            return -1;
        }

        SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int layer = 8; layer < layers.arraySize; layer++)
        {
            SerializedProperty layerProperty = layers.GetArrayElementAtIndex(layer);
            if (!string.IsNullOrEmpty(layerProperty.stringValue))
            {
                continue;
            }

            layerProperty.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            return layer;
        }

        return -1;
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null || layer < 0)
        {
            return;
        }

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            descendant.gameObject.layer = layer;
            EditorUtility.SetDirty(descendant.gameObject);
        }
    }

    static Rigidbody2D ConfigureKeyPhysics(GameObject keyObject, out TargetJoint2D dragJoint)
    {
        RemoveLegacy3DPhysics(keyObject);

        Rigidbody2D keyBody = keyObject.GetComponent<Rigidbody2D>();
        if (keyBody == null)
        {
            keyBody = keyObject.AddComponent<Rigidbody2D>();
        }

        if (keyBody == null)
        {
            dragJoint = null;
            Debug.LogError("Maze key physics setup could not add Rigidbody2D to " + keyObject.name + ".");
            return null;
        }

        keyBody.bodyType = RigidbodyType2D.Dynamic;
        keyBody.simulated = true;
        keyBody.gravityScale = 0f;
        keyBody.mass = Mathf.Max(0.01f, keyBody.mass);
        keyBody.linearDamping = 1.5f;
        keyBody.angularDamping = 0f;
        keyBody.freezeRotation = true;
        keyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        keyBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        keyBody.sleepMode = RigidbodySleepMode2D.NeverSleep;

        dragJoint = keyObject.GetComponent<TargetJoint2D>();
        if (dragJoint == null)
        {
            dragJoint = keyObject.AddComponent<TargetJoint2D>();
        }

        dragJoint.autoConfigureTarget = false;
        dragJoint.maxForce = 80f;
        dragJoint.frequency = 12f;
        dragJoint.dampingRatio = 1f;
        dragJoint.enabled = false;

        EditorUtility.SetDirty(keyObject);
        EditorUtility.SetDirty(keyBody);
        EditorUtility.SetDirty(dragJoint);
        return keyBody;
    }

    static Tilemap FindLargestTilemapUnder(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Tilemap bestTilemap = null;
        int bestTileCount = -1;
        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(true);
        foreach (Tilemap tilemap in tilemaps)
        {
            int tileCount = CountTiles(tilemap);
            if (tileCount > bestTileCount)
            {
                bestTileCount = tileCount;
                bestTilemap = tilemap;
            }
        }

        return bestTilemap;
    }

    static void EnsureMarkerIsOnOpenTile(Tilemap wallTilemap, Transform marker, bool preferUpperLeft)
    {
        if (wallTilemap == null || marker == null)
        {
            return;
        }

        Vector3Int markerCell = wallTilemap.WorldToCell(marker.position);
        if (IsCellInsideBounds(wallTilemap.cellBounds, markerCell) && !wallTilemap.HasTile(markerCell))
        {
            return;
        }

        if (!TryFindOpenTileWorldPosition(wallTilemap, preferUpperLeft, out Vector3 openPosition))
        {
            return;
        }

        marker.position = new Vector3(openPosition.x, openPosition.y, marker.position.z);
        EditorUtility.SetDirty(marker);
    }

    static void EnsureExitMarkerIsAwayFromStart(Transform marker, Vector3 startPosition, Bounds mazeBounds)
    {
        if (marker == null)
        {
            return;
        }

        if (Vector2.Distance(marker.position, startPosition) >= 1f)
        {
            return;
        }

        marker.position = new Vector3(
            mazeBounds.max.x - mazeBounds.size.x * 0.1f,
            mazeBounds.min.y + mazeBounds.size.y * 0.12f,
            marker.position.z);
        EditorUtility.SetDirty(marker);
    }

    static bool TryFindOpenTileWorldPosition(Tilemap wallTilemap, bool preferUpperLeft, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (wallTilemap == null)
        {
            return false;
        }

        BoundsInt bounds = wallTilemap.cellBounds;
        int yStart = preferUpperLeft ? bounds.yMax - 1 : bounds.yMin;
        int yEnd = preferUpperLeft ? bounds.yMin - 1 : bounds.yMax;
        int yStep = preferUpperLeft ? -1 : 1;
        int xStart = preferUpperLeft ? bounds.xMin : bounds.xMax - 1;
        int xEnd = preferUpperLeft ? bounds.xMax : bounds.xMin - 1;
        int xStep = preferUpperLeft ? 1 : -1;

        for (int y = yStart; y != yEnd; y += yStep)
        {
            for (int x = xStart; x != xEnd; x += xStep)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (wallTilemap.HasTile(cell))
                {
                    continue;
                }

                worldPosition = wallTilemap.GetCellCenterWorld(cell);
                return true;
            }
        }

        return false;
    }

    static bool TryFindFarthestOpenTileWorldPosition(Tilemap wallTilemap, Vector3 startPosition, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;
        if (wallTilemap == null)
        {
            return false;
        }

        bool found = false;
        float bestDistanceSqr = -1f;
        BoundsInt bounds = wallTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (wallTilemap.HasTile(cell))
            {
                continue;
            }

            Vector3 candidatePosition = wallTilemap.GetCellCenterWorld(cell);
            float distanceSqr = (candidatePosition - startPosition).sqrMagnitude;
            if (distanceSqr <= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            worldPosition = candidatePosition;
            found = true;
        }

        return found;
    }

    static bool IsCellInsideBounds(BoundsInt bounds, Vector3Int cell)
    {
        return cell.x >= bounds.xMin
            && cell.x < bounds.xMax
            && cell.y >= bounds.yMin
            && cell.y < bounds.yMax
            && cell.z >= bounds.zMin
            && cell.z < bounds.zMax;
    }

    static Collider2D FindFirstEnabledCollider2D(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D collider2D in colliders)
        {
            if (collider2D != null && collider2D.enabled)
            {
                return collider2D;
            }
        }

        return null;
    }

    static void DisableGeneratedMazeColliders(Transform generatedWallRoot, Transform activeTilemapWallRoot)
    {
        if (generatedWallRoot == null || generatedWallRoot == activeTilemapWallRoot)
        {
            return;
        }

        Collider2D[] generatedColliders = generatedWallRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D collider2D in generatedColliders)
        {
            if (collider2D == null)
            {
                continue;
            }

            collider2D.enabled = false;
            EditorUtility.SetDirty(collider2D);
        }

        generatedWallRoot.gameObject.SetActive(false);
        EditorUtility.SetDirty(generatedWallRoot.gameObject);
    }

    static Vector3 GetExistingMarkerWorldPosition(Transform parent, string markerName, Vector3 fallbackPosition)
    {
        Transform marker = FindChildByExactName(parent, markerName);
        return marker != null ? marker.position : fallbackPosition;
    }

    static Vector3 GetDefaultLockPosition(Bounds mazeBounds, float zPosition)
    {
        return new Vector3(
            mazeBounds.max.x - mazeBounds.size.x * 0.1f,
            mazeBounds.min.y + mazeBounds.size.y * 0.12f,
            zPosition);
    }

    static void DeleteOldMazeMarker(Transform parent, string markerName)
    {
        Transform marker = FindChildByExactName(parent, markerName);
        if (marker == null)
        {
            return;
        }

        Object.DestroyImmediate(marker.gameObject);
        if (parent != null)
        {
            EditorUtility.SetDirty(parent.gameObject);
        }
    }

    static GameObject EnsureLockObject(GameObject mazeObject, Vector3 defaultWorldPosition)
    {
        if (mazeObject == null)
        {
            return null;
        }

        GameObject lockObject = FindSceneObjectByExactName(LockName);
        bool createdLock = false;
        if (lockObject == null)
        {
            lockObject = new GameObject(LockName);
            lockObject.transform.position = defaultWorldPosition;
            lockObject.transform.localScale = Vector3.one * 0.08f;
            createdLock = true;
        }

        if (lockObject.transform.parent != mazeObject.transform)
        {
            lockObject.transform.SetParent(mazeObject.transform, true);
        }

        if (createdLock)
        {
            lockObject.transform.position = defaultWorldPosition;
        }

        Sprite lockSprite = LoadLargestSpriteAtPath(LockSpritePath);
        if (lockSprite == null)
        {
            Debug.LogWarning("Lock sprite setup skipped because Assets/Art/puzzles/lock.png was not found.");
        }
        else
        {
            GameObject visualObject = EnsureChildObject(lockObject.transform, LockVisualName, out bool createdVisual);
            if (createdVisual)
            {
                visualObject.transform.localRotation = Quaternion.identity;
                visualObject.transform.localScale = Vector3.one;
            }

            visualObject.transform.localPosition = -lockSprite.bounds.center;

            SpriteRenderer lockRenderer = visualObject.GetComponent<SpriteRenderer>();
            if (lockRenderer == null)
            {
                lockRenderer = visualObject.AddComponent<SpriteRenderer>();
            }

            lockRenderer.sprite = lockSprite;

            SpriteRenderer mazeRenderer = mazeObject.GetComponent<SpriteRenderer>();
            if (mazeRenderer != null)
            {
                lockRenderer.sortingLayerID = mazeRenderer.sortingLayerID;
                lockRenderer.sortingOrder = mazeRenderer.sortingOrder + 2;
            }

            EditorUtility.SetDirty(visualObject);
            EditorUtility.SetDirty(lockRenderer);
        }

        BoxCollider2D lockCollider = lockObject.GetComponent<BoxCollider2D>();
        if (lockCollider == null)
        {
            lockCollider = lockObject.AddComponent<BoxCollider2D>();
        }

        lockCollider.isTrigger = true;
        lockCollider.offset = Vector2.zero;
        if (lockSprite != null && createdLock)
        {
            lockCollider.size = lockSprite.bounds.size;
        }
        else if (lockCollider.size == Vector2.zero)
        {
            lockCollider.size = Vector2.one;
        }

        EditorUtility.SetDirty(lockObject);
        EditorUtility.SetDirty(lockCollider);
        return lockObject;
    }

    static Sprite EnsureKeySprite(GameObject keyObject, GameObject mazeObject)
    {
        if (keyObject == null)
        {
            return null;
        }

        Sprite keySprite = LoadLargestSpriteAtPath(KeySpritePath);
        if (keySprite == null)
        {
            Debug.LogWarning("Key sprite setup skipped because Assets/Art/puzzles/door key.png was not found.");
            return null;
        }

        GameObject visualObject = EnsureChildObject(keyObject.transform, MazeKeyVisualName, out bool createdVisualObject);
        if (createdVisualObject)
        {
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = Vector3.one;
        }

        visualObject.transform.localPosition = -keySprite.bounds.center;

        SpriteRenderer keyRenderer = visualObject.GetComponent<SpriteRenderer>();
        if (keyRenderer == null)
        {
            keyRenderer = visualObject.AddComponent<SpriteRenderer>();
        }

        keyRenderer.sprite = keySprite;

        SpriteRenderer mazeRenderer = mazeObject != null ? mazeObject.GetComponent<SpriteRenderer>() : null;
        if (mazeRenderer != null)
        {
            keyRenderer.sortingLayerID = mazeRenderer.sortingLayerID;
            keyRenderer.sortingOrder = mazeRenderer.sortingOrder + 2;
        }
        else
        {
            keyRenderer.sortingOrder = Mathf.Max(keyRenderer.sortingOrder, 1);
        }

        DisableLegacy3DKeyComponents(keyObject);

        EditorUtility.SetDirty(visualObject);
        EditorUtility.SetDirty(keyRenderer);
        return keySprite;
    }

    static Sprite LoadLargestSpriteAtPath(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Sprite largestSprite = null;
        float largestArea = -1f;

        foreach (Object asset in assets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite == null)
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

    static Collider2D EnsureKeyCollisionCollider(GameObject keyObject, Sprite keySprite)
    {
        if (keyObject == null)
        {
            return null;
        }

        GameObject colliderObject = EnsureChildObject(keyObject.transform, MazeKeyColliderName, out bool createdColliderObject);
        if (createdColliderObject)
        {
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;
        }

        Collider2D keyCollider = colliderObject.GetComponent<Collider2D>();
        bool createdCollider = false;
        if (keyCollider == null)
        {
            keyCollider = colliderObject.AddComponent<BoxCollider2D>();
            createdCollider = true;
        }

        keyCollider.enabled = true;
        keyCollider.isTrigger = false;

        if (createdCollider && keySprite != null)
        {
            FitKeyColliderToSprite(keyCollider, keySprite);
        }
        else if (createdCollider && keyCollider is BoxCollider2D boxCollider && boxCollider.size == Vector2.zero)
        {
            boxCollider.size = Vector2.one;
        }

        EditorUtility.SetDirty(colliderObject);
        EditorUtility.SetDirty(keyCollider);
        return keyCollider;
    }

    static void FitKeyColliderToSprite(Collider2D keyCollider, Sprite keySprite)
    {
        if (keyCollider == null || keySprite == null)
        {
            return;
        }

        BoxCollider2D boxCollider = keyCollider as BoxCollider2D;
        if (boxCollider == null)
        {
            return;
        }

        boxCollider.transform.localPosition = Vector3.zero;
        boxCollider.transform.localRotation = Quaternion.identity;
        boxCollider.transform.localScale = Vector3.one;
        boxCollider.size = keySprite.bounds.size;
        boxCollider.offset = Vector2.zero;
        EditorUtility.SetDirty(boxCollider);
    }

    static CircleCollider2D EnsureKeyControlPoint(GameObject keyObject)
    {
        if (keyObject == null)
        {
            return null;
        }

        GameObject controlPointObject = EnsureChildObject(keyObject.transform, MazeKeyControlPointName, out bool createdControlPoint);
        if (createdControlPoint)
        {
            controlPointObject.transform.localPosition = Vector3.zero;
            controlPointObject.transform.localRotation = Quaternion.identity;
            controlPointObject.transform.localScale = Vector3.one;
        }

        CircleCollider2D controlPointCollider = controlPointObject.GetComponent<CircleCollider2D>();
        bool createdCollider = false;
        if (controlPointCollider == null)
        {
            controlPointCollider = controlPointObject.AddComponent<CircleCollider2D>();
            createdCollider = true;
        }

        controlPointCollider.isTrigger = true;
        controlPointCollider.enabled = true;

        if (createdControlPoint || createdCollider)
        {
            controlPointCollider.offset = Vector2.zero;
            controlPointCollider.radius = GetLocalRadiusForWorldSize(controlPointObject.transform, ControlPointWorldRadius);
        }

        if (createdControlPoint)
        {
            Debug.Log("Created Maze Key Control Point. Move this child object or adjust its CircleCollider2D radius to tune key dragging.");
        }

        EditorUtility.SetDirty(controlPointObject);
        EditorUtility.SetDirty(controlPointCollider);
        return controlPointCollider;
    }

    static float GetLocalRadiusForWorldSize(Transform transform, float desiredWorldRadius)
    {
        Vector3 lossyScale = transform != null ? transform.lossyScale : Vector3.one;
        float largestScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
        if (largestScale <= 0.0001f)
        {
            largestScale = 1f;
        }

        return Mathf.Max(0.01f, desiredWorldRadius / largestScale);
    }

    static void DisableLegacyKeyColliders(GameObject keyObject, Collider2D activeKeyCollider, Collider2D activeControlPointCollider)
    {
        if (keyObject == null)
        {
            return;
        }

        Collider2D[] colliders = keyObject.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D collider2D in colliders)
        {
            if (collider2D == null)
            {
                continue;
            }

            collider2D.enabled = collider2D == activeKeyCollider || collider2D == activeControlPointCollider;
            EditorUtility.SetDirty(collider2D);
        }
    }

    static void DisableLegacy3DKeyComponents(GameObject keyObject)
    {
        MeshRenderer[] meshRenderers = keyObject.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer meshRenderer in meshRenderers)
        {
            if (meshRenderer == null)
            {
                continue;
            }

            meshRenderer.enabled = false;
            EditorUtility.SetDirty(meshRenderer);
        }

        Collider[] colliders3D = keyObject.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider3D in colliders3D)
        {
            if (collider3D == null)
            {
                continue;
            }

            Undo.DestroyObjectImmediate(collider3D);
        }

        Rigidbody[] rigidbodies3D = keyObject.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rigidbody3D in rigidbodies3D)
        {
            if (rigidbody3D != null)
            {
                Undo.DestroyObjectImmediate(rigidbody3D);
            }
        }
    }

    static void RemoveLegacy3DPhysics(GameObject keyObject)
    {
        Collider[] colliders3D = keyObject.GetComponents<Collider>();
        foreach (Collider collider3D in colliders3D)
        {
            if (collider3D != null)
            {
                Undo.DestroyObjectImmediate(collider3D);
            }
        }

        Rigidbody rigidbody3D = keyObject.GetComponent<Rigidbody>();
        if (rigidbody3D != null)
        {
            Undo.DestroyObjectImmediate(rigidbody3D);
        }
    }

    static DoorProgressGate2D FindDoorGate()
    {
        GameObject doorObject = FindSceneObjectByExactName(DoorName);
        return doorObject != null ? doorObject.GetComponent<DoorProgressGate2D>() : null;
    }

    static void SetWallCollidersAsTriggers(Transform wallRoot)
    {
        if (wallRoot == null)
        {
            return;
        }

        Collider2D[] wallColliders = wallRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D wallCollider in wallColliders)
        {
            if (wallCollider == null)
            {
                continue;
            }

            wallCollider.isTrigger = true;
            EditorUtility.SetDirty(wallCollider);
        }
    }

    static GameObject EnsureChildObject(Transform parent, string objectName, out bool created)
    {
        created = false;
        Transform child = FindChildByExactName(parent, objectName);
        if (child != null)
        {
            return child.gameObject;
        }

        GameObject childObject = new GameObject(objectName);
        childObject.transform.SetParent(parent, false);
        created = true;
        return childObject;
    }

    static Transform FindChildByExactName(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    static Bounds GetMazeWorldBounds(GameObject mazeObject)
    {
        SpriteRenderer spriteRenderer = mazeObject.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds localBounds = spriteRenderer.sprite.bounds;
            Vector3[] corners =
            {
                new Vector3(localBounds.min.x, localBounds.min.y, 0f),
                new Vector3(localBounds.min.x, localBounds.max.y, 0f),
                new Vector3(localBounds.max.x, localBounds.min.y, 0f),
                new Vector3(localBounds.max.x, localBounds.max.y, 0f),
            };

            Bounds worldBounds = new Bounds(spriteRenderer.transform.TransformPoint(corners[0]), Vector3.zero);
            for (int index = 1; index < corners.Length; index++)
            {
                worldBounds.Encapsulate(spriteRenderer.transform.TransformPoint(corners[index]));
            }

            return worldBounds;
        }

        Renderer renderer = mazeObject.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            return renderer.bounds;
        }

        return new Bounds(mazeObject.transform.position, new Vector3(6f, 4f, 0f));
    }

    static void SetActiveAndDirty(GameObject sceneObject, bool isActive)
    {
        if (sceneObject == null)
        {
            return;
        }

        sceneObject.SetActive(isActive);
        EditorUtility.SetDirty(sceneObject);
    }
}
