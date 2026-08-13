using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InteractionHandPromptSceneSetup
{
    const string RequestPath =
        "Assets/Editor/CodexInteractionHandPromptSetupRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string HandSpritePath = "Assets/Art/ui/hand.png";
    const float HandWorldScale = 0.054f;

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Interaction Hand Prompts")]
    public static void SetupFromMenu()
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
                "Interaction hand prompt setup is waiting for "
                + TargetScenePath
                + ".");
            return false;
        }

        GameObject player =
            FindSceneObjectByExactName(BubuRunningGame.PlayerRootName);
        GameObject incense = FindSceneObjectByExactName("xianglu");
        GameObject door = FindSceneObjectByExactName("Door");
        GameObject gate = FindSceneObjectByExactName("Gate");
        Sprite handSprite =
            AssetDatabase.LoadAssetAtPath<Sprite>(HandSpritePath);
        if (player == null
            || incense == null
            || door == null
            || gate == null
            || handSprite == null)
        {
            Debug.LogError(
                "Interaction hand prompt setup requires Bubu Player, "
                + "xianglu, Door, Gate and hand.png.");
            return false;
        }

        Collider2D playerCollider =
            player.GetComponentInChildren<Collider2D>(true);
        ConfigurePrompt(
            incense,
            "hand xianglu",
            handSprite,
            player.transform,
            playerCollider,
            new Vector3(0f, 1.25f, -0.1f),
            2.2f,
            false);
        ConfigurePrompt(
            door,
            "hand door",
            handSprite,
            player.transform,
            playerCollider,
            new Vector3(-0.32f, 1.23f, -0.1f),
            0.8f,
            true);
        ConfigurePrompt(
            gate,
            "hand gate",
            handSprite,
            player.transform,
            playerCollider,
            new Vector3(-1.25f, 1.45f, -0.1f),
            1.3f,
            true);

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log(
            "Configured pot-style hand prompts for xianglu, "
            + "the first-level Door and the second-level Gate.");
        return true;
    }

    static void ConfigurePrompt(
        GameObject target,
        string handName,
        Sprite handSprite,
        Transform player,
        Collider2D playerCollider,
        Vector3 worldOffset,
        float triggerRadius,
        bool useObjectBounds)
    {
        GameObject hand = FindSceneObjectByExactName(handName);
        if (hand == null)
        {
            hand = new GameObject(handName);
        }

        hand.transform.SetParent(target.transform, true);
        hand.transform.position =
            target.transform.position + worldOffset;
        hand.transform.rotation = Quaternion.identity;
        SetWorldScale(
            hand.transform,
            new Vector3(
                HandWorldScale,
                HandWorldScale,
                HandWorldScale));

        SpriteRenderer handRenderer =
            GetOrAddComponent<SpriteRenderer>(hand);
        handRenderer.sprite = handSprite;
        handRenderer.sortingOrder = GetFrontSortingOrder(target);

        HandPromptTrigger2D prompt =
            GetOrAddComponent<HandPromptTrigger2D>(target);
        prompt.playerObjectName = BubuRunningGame.PlayerRootName;
        prompt.player = player;
        prompt.playerCollider = playerCollider;
        prompt.handObject = hand;
        prompt.triggerRadius = triggerRadius;
        prompt.triggerCenterOffset = Vector2.zero;
        prompt.hideHandOnStart = true;
        prompt.useObjectBounds = useObjectBounds;
        prompt.requirePlayerTouch = false;
        prompt.SetHandVisible(false);

        EditorUtility.SetDirty(target);
        EditorUtility.SetDirty(hand);
        EditorUtility.SetDirty(hand.transform);
        EditorUtility.SetDirty(handRenderer);
        EditorUtility.SetDirty(prompt);
    }

    static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (parent == null)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale =
            new Vector3(
                SafeDivide(worldScale.x, parentScale.x),
                SafeDivide(worldScale.y, parentScale.y),
                SafeDivide(worldScale.z, parentScale.z));
    }

    static float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f)
            ? value
            : worldSign(divisor) * value / Mathf.Abs(divisor);
    }

    static float worldSign(float value)
    {
        return value < 0f ? -1f : 1f;
    }

    static int GetFrontSortingOrder(GameObject target)
    {
        Renderer renderer =
            target.GetComponentInChildren<Renderer>(true);
        return renderer != null ? renderer.sortingOrder + 20 : 100;
    }

    static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null
            ? component
            : target.AddComponent<T>();
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        GameObject[] sceneObjects =
            UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null
                && string.Equals(
                    sceneObject.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return sceneObject;
            }
        }

        return null;
    }
}
