using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class PenzaiRandomItemsSceneSetup
{
    const string RequestPath = "Assets/Editor/CodexPenzaiRandomItemsSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string ControllerName = "Penzai Search Controller";
    const string SpawnPointName = "Item Spawn Point";
    const string KeyName = "key";
    const string ShoeName = "xiuhuaxie";
    const string DollName = "doll";
    const string NeedleName = "yinzhen";
    const string ScissorsName = "scissors";
    const string KeySpritePath = "Assets/Art/puzzles/key.png";
    const string ShoeSpritePath = "Assets/Art/puzzles/xiuhuaxie.png";
    const string DollSpritePath = "Assets/Art/puzzles/doll.png";
    const string NeedleSpritePath = "Assets/Art/puzzles/yinzhen.png";
    const string ScissorsSpritePath = "Assets/Art/puzzles/scissors.png";
    const float PreviousDollTargetHeight = 0.8f;
    const float DefaultDollTargetHeight = 1.4f;

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Random Penzai Items")]
    public static void SetupRandomPenzaiItemsFromMenu()
    {
        SetupActiveScene();
    }

    static void ProcessRequestedSetup()
    {
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) == null)
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ProcessRequestedSetup;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling
            || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += ProcessRequestedSetup;
            return;
        }

        if (!SetupActiveScene())
        {
            return;
        }

        AssetDatabase.DeleteAsset(RequestPath);
        AssetDatabase.SaveAssets();
    }

    static bool SetupActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != TargetScenePath)
        {
            Debug.LogWarning(
                "Random penzai item setup is waiting for the active scene to be "
                + TargetScenePath
                + ".");
            return false;
        }

        GameplayMessageUI2D messageUI =
            UnityEngine.Object.FindAnyObjectByType<GameplayMessageUI2D>(
                FindObjectsInactive.Include);

        GameObject controllerObject = FindSceneObjectByExactName(ControllerName);
        if (controllerObject == null)
        {
            controllerObject = new GameObject(ControllerName);
        }

        PenzaiSearchController2D controller =
            GetOrAddComponent<PenzaiSearchController2D>(controllerObject);
        controller.playerObjectName = BubuRunningGame.PlayerRootName;
        controller.searchPointPrefix = "penzai";
        controller.randomizeItemsOnStart = true;
        controller.hideItemsOnStart = true;
        controller.refreshSearchPointsOnStart = true;
        controller.allowMultipleItemsPerSearchPoint = false;
        controller.messageUI = messageUI;

        List<GameObject> penzaiObjects = FindPenzaiObjects();
        foreach (GameObject penzaiObject in penzaiObjects)
        {
            ConfigurePenzaiFeedback(penzaiObject, messageUI);
        }

        controller.RefreshSearchPointsFromScene();

        Sprite keySprite = LoadLargestSprite(KeySpritePath);
        Sprite shoeSprite = LoadLargestSprite(ShoeSpritePath);
        Sprite dollSprite = LoadLargestSprite(DollSpritePath);
        Sprite needleSprite = LoadLargestSprite(NeedleSpritePath);
        Sprite scissorsSprite = LoadLargestSprite(ScissorsSpritePath);
        GameObject keyObject = GetOrCreateSceneItem(KeyName, keySprite, 0.8f);
        GameObject shoeObject = GetOrCreateSceneItem(ShoeName, shoeSprite, 0.8f);
        GameObject dollObject =
            GetOrCreateSceneItem(
                DollName,
                dollSprite,
                DefaultDollTargetHeight);
        GameObject needleObject =
            GetOrCreateSceneItem(NeedleName, needleSprite, 0.8f);
        GameObject scissorsObject =
            GetOrCreateSceneItem(ScissorsName, scissorsSprite, 0.8f);
        UpgradeUntouchedDollScale(dollObject, dollSprite);

        List<PenzaiSearchController2D.HiddenItem> definitions =
            controller.hiddenItems == null
                ? new List<PenzaiSearchController2D.HiddenItem>()
                : controller.hiddenItems.Where(item => item != null).ToList();

        definitions.RemoveAll(
            item =>
                string.Equals(
                    item.itemName,
                    KeyName,
                    StringComparison.OrdinalIgnoreCase));
        ConfigureItemDefinition(
            definitions,
            ShoeName,
                "You found the embroidered shoe.\nMovement speed reduced by 50%.",
            shoeObject,
            shoeSprite);
        ConfigureItemDefinition(
            definitions,
            DollName,
                "You found the doll.",
            dollObject,
            dollSprite);
        ConfigureItemDefinition(
            definitions,
            NeedleName,
                "You found the silver needles.",
            needleObject,
            needleSprite);
        ConfigureItemDefinition(
            definitions,
            ScissorsName,
                "You found the scissors.",
            scissorsObject,
            scissorsSprite);
        ConfigureShoeSlowdown(definitions);
        ConfigureDollFollower(definitions, dollObject);
        controller.hiddenItems = definitions.ToArray();

        HideItemObject(keyObject);
        HideItemObject(shoeObject);
        HideItemObject(dollObject);
        HideItemObject(needleObject);
        HideItemObject(scissorsObject);

        EditorUtility.SetDirty(controllerObject);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        Debug.Log(
            "Configured "
            + penzaiObjects.Count
            + " penzai search points with randomized xiuhuaxie, doll, yinzhen and scissors items; key is no longer generated by penzai.");
        return true;
    }

    static void ConfigurePenzaiFeedback(
        GameObject penzaiObject,
        GameplayMessageUI2D messageUI)
    {
        PenzaiSearchFeedback2D feedback =
            GetOrAddComponent<PenzaiSearchFeedback2D>(penzaiObject);

        Transform spawnPoint = penzaiObject.transform.Find(SpawnPointName);
        if (spawnPoint == null)
        {
            GameObject spawnPointObject = new GameObject(SpawnPointName);
            spawnPointObject.transform.SetParent(penzaiObject.transform, false);
            spawnPoint = spawnPointObject.transform;
        }

        feedback.itemSpawnPoint = spawnPoint;
        feedback.messageUI = messageUI;
        if (string.IsNullOrWhiteSpace(feedback.nothingFoundMessage))
        {
            feedback.nothingFoundMessage = "Nothing was found.";
        }

        EditorUtility.SetDirty(spawnPoint.gameObject);
        EditorUtility.SetDirty(feedback);
        EditorUtility.SetDirty(penzaiObject);
    }

    static void ConfigureItemDefinition(
        List<PenzaiSearchController2D.HiddenItem> definitions,
        string itemName,
        string defaultFoundMessage,
        GameObject itemObject,
        Sprite itemIcon)
    {
        PenzaiSearchController2D.HiddenItem definition =
            definitions.FirstOrDefault(
                item =>
                    string.Equals(
                        item.itemName,
                        itemName,
                        StringComparison.OrdinalIgnoreCase));

        if (definition == null)
        {
            definition = new PenzaiSearchController2D.HiddenItem();
            definitions.Add(definition);
        }

        definition.itemName = itemName;
        definition.itemObjectName = itemName;
        definition.itemObject = itemObject;
        definition.itemIcon = itemIcon;
        if (string.IsNullOrWhiteSpace(definition.foundMessage))
        {
            definition.foundMessage = defaultFoundMessage;
        }

        if (definition.onCollected == null)
        {
            definition.onCollected = new UnityEvent();
        }
    }

    static void ConfigureShoeSlowdown(
        List<PenzaiSearchController2D.HiddenItem> definitions)
    {
        GameObject playerObject =
            FindSceneObjectByExactName(BubuRunningGame.PlayerRootName);
        if (playerObject == null)
        {
            Debug.LogWarning("Shoe slowdown setup skipped because Bubu Player was not found.");
            return;
        }

        TemporaryMovementSpeedModifier2D speedModifier =
            GetOrAddComponent<TemporaryMovementSpeedModifier2D>(playerObject);
        speedModifier.speedMultiplier = 0.5f;
        speedModifier.minimumDuration = 10f;
        speedModifier.maximumDuration = 15f;
        speedModifier.refreshDurationOnRetrigger = true;
        speedModifier.useUnscaledTime = true;

        PenzaiSearchController2D.HiddenItem shoeDefinition =
            definitions.FirstOrDefault(
                item =>
                    string.Equals(
                        item.itemName,
                        ShoeName,
                        StringComparison.OrdinalIgnoreCase));
        if (shoeDefinition == null)
        {
            Debug.LogWarning("Shoe slowdown setup skipped because xiuhuaxie is not configured.");
            return;
        }

        shoeDefinition.foundMessage =
                "You found the embroidered shoe.\nMovement speed reduced by 50%.";

        if (shoeDefinition.onCollected == null)
        {
            shoeDefinition.onCollected = new UnityEvent();
        }

        bool hasSlowdownListener = false;
        int listenerCount = shoeDefinition.onCollected.GetPersistentEventCount();
        for (int index = 0; index < listenerCount; index++)
        {
            if (shoeDefinition.onCollected.GetPersistentTarget(index) == speedModifier
                && shoeDefinition.onCollected.GetPersistentMethodName(index)
                    == nameof(TemporaryMovementSpeedModifier2D.ApplyConfiguredSlowdown))
            {
                hasSlowdownListener = true;
                break;
            }
        }

        if (!hasSlowdownListener)
        {
            UnityEventTools.AddPersistentListener(
                shoeDefinition.onCollected,
                speedModifier.ApplyConfiguredSlowdown);
        }

        EditorUtility.SetDirty(speedModifier);
        EditorUtility.SetDirty(playerObject);
    }

    static void ConfigureDollFollower(
        List<PenzaiSearchController2D.HiddenItem> definitions,
        GameObject dollObject)
    {
        if (dollObject == null)
        {
            Debug.LogWarning("Doll follower setup skipped because doll was not found.");
            return;
        }

        GameObject playerObject =
            FindSceneObjectByExactName(BubuRunningGame.PlayerRootName);
        CollectedItemFollower2D follower =
            GetOrAddComponent<CollectedItemFollower2D>(dollObject);
        follower.targetObjectName = BubuRunningGame.PlayerRootName;
        follower.target = playerObject != null ? playerObject.transform : null;
        follower.followOffset = new Vector3(-1.1f, -0.15f, 0f);
        follower.smoothTime = 0.18f;
        follower.maximumFollowSpeed = 20f;
        follower.teleportSnapDistance = 5f;
        follower.detachFromSearchPointOnCollect = true;
        follower.enableRenderersOnCollect = true;
        follower.disableCollidersWhileFollowing = true;
        follower.overrideSortingOrder = true;
        follower.sortingOrderWhileFollowing = 9;

        PenzaiSearchController2D.HiddenItem dollDefinition =
            definitions.FirstOrDefault(
                item =>
                    string.Equals(
                        item.itemName,
                        DollName,
                        StringComparison.OrdinalIgnoreCase));
        if (dollDefinition == null)
        {
            Debug.LogWarning("Doll follower setup skipped because doll is not configured.");
            return;
        }

        if (dollDefinition.onCollected == null)
        {
            dollDefinition.onCollected = new UnityEvent();
        }

        bool hasFollowerListener = false;
        int listenerCount = dollDefinition.onCollected.GetPersistentEventCount();
        for (int index = 0; index < listenerCount; index++)
        {
            if (dollDefinition.onCollected.GetPersistentTarget(index) == follower
                && dollDefinition.onCollected.GetPersistentMethodName(index)
                    == nameof(CollectedItemFollower2D.BeginFollowing))
            {
                hasFollowerListener = true;
                break;
            }
        }

        if (!hasFollowerListener)
        {
            UnityEventTools.AddPersistentListener(
                dollDefinition.onCollected,
                follower.BeginFollowing);
        }

        EditorUtility.SetDirty(follower);
        EditorUtility.SetDirty(dollObject);
    }

    static List<GameObject> FindPenzaiObjects()
    {
        GameObject[] sceneObjects =
            UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        List<GameObject> penzaiObjects = new List<GameObject>();

        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject == null)
            {
                continue;
            }

            if (string.Equals(sceneObject.name, "penzai", StringComparison.OrdinalIgnoreCase)
                || sceneObject.name.StartsWith(
                    "penzai (",
                    StringComparison.OrdinalIgnoreCase))
            {
                penzaiObjects.Add(sceneObject);
            }
        }

        penzaiObjects.Sort(
            (first, second) =>
                string.Compare(first.name, second.name, StringComparison.OrdinalIgnoreCase));
        return penzaiObjects;
    }

    static GameObject GetOrCreateSceneItem(
        string objectName,
        Sprite sprite,
        float targetHeight)
    {
        GameObject itemObject = FindSceneObjectByExactName(objectName);
        bool createdItem = itemObject == null;
        if (itemObject == null)
        {
            itemObject = new GameObject(objectName);
        }

        SpriteRenderer spriteRenderer = GetOrAddComponent<SpriteRenderer>(itemObject);
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 20;

        if (createdItem && sprite != null && sprite.bounds.size.y > 0f)
        {
            float scale = targetHeight / sprite.bounds.size.y;
            itemObject.transform.localScale = new Vector3(scale, scale, scale);
        }

        EditorUtility.SetDirty(itemObject);
        EditorUtility.SetDirty(spriteRenderer);
        return itemObject;
    }

    static void UpgradeUntouchedDollScale(
        GameObject dollObject,
        Sprite dollSprite)
    {
        if (dollObject == null
            || dollSprite == null
            || dollSprite.bounds.size.y <= 0f)
        {
            return;
        }

        float previousScale =
            PreviousDollTargetHeight / dollSprite.bounds.size.y;
        Vector3 currentScale = dollObject.transform.localScale;
        bool stillUsesPreviousGeneratedScale =
            ApproximatelyScaleMagnitude(currentScale.x, previousScale)
            && ApproximatelyScaleMagnitude(currentScale.y, previousScale)
            && ApproximatelyScaleMagnitude(currentScale.z, previousScale);
        if (!stillUsesPreviousGeneratedScale)
        {
            return;
        }

        float upgradedScale =
            DefaultDollTargetHeight / dollSprite.bounds.size.y;
        dollObject.transform.localScale =
            new Vector3(
                Mathf.Sign(currentScale.x) * upgradedScale,
                Mathf.Sign(currentScale.y) * upgradedScale,
                Mathf.Sign(currentScale.z) * upgradedScale);
        EditorUtility.SetDirty(dollObject.transform);
        EditorUtility.SetDirty(dollObject);
    }

    static bool ApproximatelyScaleMagnitude(float value, float expected)
    {
        return Mathf.Abs(Mathf.Abs(value) - expected) <= 0.0001f;
    }

    static Sprite LoadLargestSprite(string assetPath)
    {
        EnsureSpriteImporter(assetPath);
        Sprite sprite = FindLargestSprite(assetPath);
        if (sprite == null)
        {
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            sprite = FindLargestSprite(assetPath);
        }

        if (sprite == null)
        {
            Debug.LogWarning("No sprite could be loaded from " + assetPath + ".");
        }

        return sprite;
    }

    static void EnsureSpriteImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null || importer.textureType == TextureImporterType.Sprite)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.SaveAndReimport();
    }

    static Sprite FindLargestSprite(string assetPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<Sprite>()
            .OrderByDescending(candidate => candidate.rect.width * candidate.rect.height)
            .FirstOrDefault();
    }

    static void HideItemObject(GameObject itemObject)
    {
        if (itemObject == null)
        {
            return;
        }

        foreach (Renderer itemRenderer in itemObject.GetComponentsInChildren<Renderer>(true))
        {
            itemRenderer.enabled = false;
            EditorUtility.SetDirty(itemRenderer);
        }

        foreach (Collider2D itemCollider in itemObject.GetComponentsInChildren<Collider2D>(true))
        {
            itemCollider.enabled = false;
            EditorUtility.SetDirty(itemCollider);
        }

        foreach (Collider itemCollider in itemObject.GetComponentsInChildren<Collider>(true))
        {
            itemCollider.enabled = false;
            EditorUtility.SetDirty(itemCollider);
        }
    }

    static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] sceneObjects =
            UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }
}
