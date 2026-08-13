using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LevelSegmentTransitionSceneSetup
{
    const string RequestPath = "Assets/Editor/CodexLevelSegmentTransitionSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string ControllerName = "Level Transition Controller";
    const string SecondLevelStartName = "Second Level Start";
    const string TransitionCanvasName = "Level Transition Canvas";
    const string FadeObjectName = "Level Transition Fade";
    const string FirstLevelBackgroundName = "level1Background";
    const string SecondLevelBackgroundName = "level2Background";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += ProcessRequestedSetup;
        }
    }

    [MenuItem("Bubu Running/Apply Level Transition Scene Setup")]
    static void ApplySceneSetupFromMenu()
    {
        ProcessRequestedSetup();
    }

    static void ProcessRequestedSetup()
    {
        if (!SetupRequested())
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessRequestedSetup;
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScenePath)
        {
            scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        ConfigureScene(scene);
        DeleteSetupRequest();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Configured level segment transition: level one puzzle and patrol, black fade, level two chase.");
    }

    static bool SetupRequested()
    {
        return File.Exists(GetProjectRelativeAbsolutePath(RequestPath))
            || AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) != null;
    }

    static void ConfigureScene(Scene scene)
    {
        GameObject player = FindSceneObject(scene, BubuRunningGame.PlayerRootName);
        GameObject cameraObject = FindSceneObject(scene, "Main Camera");
        GameObject backgroundRoot = FindSceneObject(scene, BubuRunningGame.BackgroundGroupName);
        GameObject firstBackground =
            FindSceneObject(scene, FirstLevelBackgroundName)
            ?? FindSceneObject(scene, BubuRunningGame.Background1Name);
        GameObject secondBackground =
            FindSceneObject(scene, SecondLevelBackgroundName)
            ?? FindSceneObject(scene, BubuRunningGame.BackgroundName);
        GameObject door = FindSceneObject(scene, "Door");
        GameObject soldierGroup = FindSceneObject(scene, BubuRunningGame.SoldierGroupName);
        GameObject huajiao = FindSceneObject(scene, "huajiao");

        GameObject controllerObject = FindOrCreateRootObject(scene, ControllerName);
        LevelSegmentTransition2D transition = controllerObject.GetComponent<LevelSegmentTransition2D>();
        if (transition == null)
        {
            transition = controllerObject.AddComponent<LevelSegmentTransition2D>();
            transition.fadeOutDuration = 0.65f;
            transition.holdBlackDuration = 0.2f;
            transition.fadeInDuration = 0.65f;
            transition.triggerPadding = 0.05f;
        }

        GameObject secondStart =
            EnsureSecondLevelStart(
                scene,
                secondBackground,
                cameraObject,
                player);
        Image fadeImage = EnsureFadeCanvas(scene);
        GameObject fadeObject = fadeImage != null ? fadeImage.gameObject : null;

        Camera gameplayCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
        EdgeScrollCamera2D cameraController = cameraObject != null ? cameraObject.GetComponent<EdgeScrollCamera2D>() : null;
        if (cameraObject != null && cameraController == null)
        {
            cameraController = cameraObject.AddComponent<EdgeScrollCamera2D>();
        }

        SecondLevelCameraPreview2D cameraPreview =
            cameraObject != null
                ? cameraObject.GetComponent<SecondLevelCameraPreview2D>()
                : null;
        if (cameraObject != null && cameraPreview == null)
        {
            cameraPreview = cameraObject.AddComponent<SecondLevelCameraPreview2D>();
        }

        GameplayMessageUI2D messageUI =
            Object.FindAnyObjectByType<GameplayMessageUI2D>(
                FindObjectsInactive.Include);
        if (cameraPreview != null)
        {
            cameraPreview.gameplayCamera = gameplayCamera;
            cameraPreview.cameraController = cameraController;
            cameraPreview.player = player != null ? player.transform : cameraPreview.player;
            cameraPreview.secondLevelBackground =
                secondBackground != null
                    ? secondBackground.transform
                    : cameraPreview.secondLevelBackground;
            cameraPreview.messageUI = messageUI;
            cameraPreview.leftHoldDuration = 0.35f;
            cameraPreview.moveToRightDuration = 2.5f;
            cameraPreview.rightHoldDuration = 0.75f;
            cameraPreview.returnToPlayerDuration = 1.75f;
        cameraPreview.instructionText =
            "Move the camera with the mouse cursor.";
            cameraPreview.freezeTimeDuringPreview = true;
            cameraPreview.showInstructionAfterPreview = true;
            cameraPreview.logPreviewState = true;
            EditorUtility.SetDirty(cameraPreview);
        }

        if (cameraController != null)
        {
            cameraController.player = player != null ? player.transform : cameraController.player;
            cameraController.background = backgroundRoot != null ? backgroundRoot.transform : cameraController.background;
            cameraController.door = door != null ? door.transform : cameraController.door;
            cameraController.limitCameraUntilDoorPassed = true;
            cameraController.limitToFirstLevelBackgroundUntilDoorPassed = true;
            cameraController.firstLevelBackground = firstBackground != null ? firstBackground.transform : cameraController.firstLevelBackground;
            cameraController.limitCameraLeftEdge = true;
            cameraController.lockedCameraMaxPositionX = 20f;
        }

        transition.player = player != null ? player.transform : transition.player;
        transition.playerCollider = player != null ? player.GetComponentInChildren<Collider2D>(true) : transition.playerCollider;
        transition.gameplayCamera = gameplayCamera;
        transition.backgroundRoot = backgroundRoot != null ? backgroundRoot.transform : transition.backgroundRoot;
        transition.cameraController = cameraController;
        transition.secondLevelCameraPreview = cameraPreview;
        transition.firstLevelDoorGate = door != null ? door.GetComponent<DoorProgressGate2D>() : transition.firstLevelDoorGate;
        transition.firstLevelDoorCollider = door != null ? door.GetComponentInChildren<Collider2D>(true) : transition.firstLevelDoorCollider;
        transition.secondLevelStartPoint = secondStart != null ? secondStart.transform : transition.secondLevelStartPoint;
        transition.fadeImage = fadeImage;
        transition.fadeScreenRoot = fadeObject;
        transition.disableSecondLevelContentOnStart = true;
        transition.requireDoorSolved = true;
        transition.transitionWhenPlayerPassesDoor = true;
        transition.freezeTimeDuringTransition = true;
        transition.unlockCameraForSecondLevel = true;
        transition.avoidSoldierSpawnOverlap = true;
        transition.soldierSpawnSafetyPadding = 0.35f;
        transition.objectsEnabledInSecondLevel =
            BuildObjectArray(huajiao);

        if (soldierGroup != null)
        {
            soldierGroup.SetActive(true);
            EditorUtility.SetDirty(soldierGroup);
        }

        if (huajiao != null)
        {
            huajiao.SetActive(false);
            EditorUtility.SetDirty(huajiao);
        }

        if (fadeObject != null)
        {
            fadeObject.SetActive(false);
        }

        EditorUtility.SetDirty(controllerObject);
        if (cameraObject != null)
        {
            EditorUtility.SetDirty(cameraObject);
        }
    }

    static GameObject EnsureSecondLevelStart(
        Scene scene,
        GameObject secondBackground,
        GameObject cameraObject,
        GameObject player)
    {
        GameObject start = FindOrCreateRootObject(scene, SecondLevelStartName);
        // Keep the spawn marker in world space so background transforms cannot move it.
        start.transform.SetParent(null, true);
        Bounds secondBounds;
        if (secondBackground != null
            && TryGetRendererBounds(secondBackground.transform, out secondBounds))
        {
            Camera camera =
                cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            float aspect = camera != null ? camera.aspect : 16f / 9f;
            float halfHeight =
                secondBounds.size.y > 0f ? secondBounds.size.y * 0.5f : 4.5f;
            float halfWidth = halfHeight * aspect;
            float startX =
                secondBounds.size.x <= halfWidth * 2f
                    ? secondBounds.center.x
                    : Mathf.Clamp(
                        secondBounds.min.x + halfWidth + 0.5f,
                        secondBounds.min.x,
                        secondBounds.max.x);
            startX =
                GetSafeSpawnXForSoldiers(
                    scene,
                    startX,
                    secondBounds.center.y,
                    player);
            start.transform.position =
                new Vector3(startX, secondBounds.center.y, 0f);
        }

        EditorUtility.SetDirty(start);
        return start;
    }

    static float GetSafeSpawnXForSoldiers(
        Scene scene,
        float requestedX,
        float requestedY,
        GameObject player)
    {
        float playerHalfWidth = BubuRunningGame.PlayerWidth * 0.5f;
        float playerHalfHeight = BubuRunningGame.PlayerHeight * 0.5f;
        Collider2D playerCollider =
            player != null ? player.GetComponentInChildren<Collider2D>(true) : null;
        if (playerCollider != null)
        {
            Bounds playerBounds = playerCollider.bounds;
            playerHalfWidth =
                playerBounds.extents.x > 0f
                    ? playerBounds.extents.x
                    : playerHalfWidth;
            playerHalfHeight =
                playerBounds.extents.y > 0f
                    ? playerBounds.extents.y
                    : playerHalfHeight;
        }

        float safeX = requestedX;
        PatrollingSoldier2D[] soldiers =
            Resources.FindObjectsOfTypeAll<PatrollingSoldier2D>();
        foreach (PatrollingSoldier2D soldier in soldiers)
        {
            if (soldier == null
                || !soldier.gameObject.scene.IsValid()
                || soldier.gameObject.scene != scene)
            {
                continue;
            }

            BoxCollider2D bodyCollider = soldier.GetComponent<BoxCollider2D>();
            Vector2 bodySize =
                bodyCollider != null ? bodyCollider.size : Vector2.one;
            Vector2 bodyOffset =
                bodyCollider != null ? bodyCollider.offset : Vector2.zero;
            Vector3 scale = soldier.transform.lossyScale;
            float scaleX = Mathf.Abs(scale.x);
            float scaleY = Mathf.Abs(scale.y);
            float failureHalfWidth =
                (bodySize.x + soldier.failureRangePadding.x * 2f)
                * scaleX
                * 0.5f;
            float failureHalfHeight =
                (bodySize.y + soldier.failureRangePadding.y * 2f)
                * scaleY
                * 0.5f;
            float startingOffsetY =
                soldier.startMovingUp
                    ? Mathf.Min(
                        soldier.patrolLowestOffsetY,
                        soldier.patrolHighestOffsetY)
                    : Mathf.Max(
                        soldier.patrolLowestOffsetY,
                        soldier.patrolHighestOffsetY);
            float failureCenterX =
                soldier.transform.position.x
                + (bodyOffset.x + soldier.failureRangeOffset.x) * scaleX;
            float failureCenterY =
                soldier.transform.position.y
                + startingOffsetY
                + (bodyOffset.y + soldier.failureRangeOffset.y) * scaleY;

            float verticalSafeDistance =
                failureHalfHeight
                + playerHalfHeight
                + 0.35f;
            if (Mathf.Abs(requestedY - failureCenterY)
                > verticalSafeDistance)
            {
                continue;
            }

            float horizontalSafeDistance =
                failureHalfWidth
                + playerHalfWidth
                + 0.35f;
            float safeLeftX = failureCenterX - horizontalSafeDistance;
            float safeRightX = failureCenterX + horizontalSafeDistance;
            if (safeX > safeLeftX && safeX < safeRightX)
            {
                safeX = safeLeftX;
            }
        }

        return safeX;
    }

    static Image EnsureFadeCanvas(Scene scene)
    {
        GameObject canvasObject = FindOrCreateRootObject(scene, TransitionCanvasName);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = canvasObject.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 7000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvasObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject fadeObject = FindChild(canvasObject.transform, FadeObjectName);
        if (fadeObject == null)
        {
            fadeObject = new GameObject(FadeObjectName, typeof(RectTransform));
            fadeObject.transform.SetParent(canvasObject.transform, false);
        }

        RectTransform rectTransform = fadeObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        Image image = fadeObject.GetComponent<Image>();
        if (image == null)
        {
            image = fadeObject.AddComponent<Image>();
        }

        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        EditorUtility.SetDirty(canvasObject);
        EditorUtility.SetDirty(fadeObject);
        return image;
    }

    static GameObject[] BuildObjectArray(params GameObject[] candidates)
    {
        List<GameObject> objects = new List<GameObject>();
        foreach (GameObject candidate in candidates)
        {
            if (candidate != null && !objects.Contains(candidate))
            {
                objects.Add(candidate);
            }
        }

        return objects.ToArray();
    }

    static GameObject FindOrCreateRootObject(Scene scene, string objectName)
    {
        GameObject existing = FindSceneObject(scene, objectName);
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject(objectName);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    static GameObject FindSceneObject(Scene scene, string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject sceneObject in objects)
        {
            if (sceneObject == null || sceneObject.name != objectName)
            {
                continue;
            }

            if (sceneObject.scene.IsValid() && sceneObject.scene == scene)
            {
                return sceneObject;
            }
        }

        return null;
    }

    static GameObject FindChild(Transform parent, string objectName)
    {
        foreach (Transform child in parent)
        {
            if (child != null && child.name == objectName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;
        bounds = new Bounds(root.position, Vector3.zero);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    static void DeleteSetupRequest()
    {
        if (AssetDatabase.DeleteAsset(RequestPath))
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
}
