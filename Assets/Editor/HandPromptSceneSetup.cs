using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HandPromptSceneSetup
{
    const string RequestPath = "Assets/Editor/CodexHandPromptSetupRequest.txt";
    const string TargetPrefix = "penzai";
    const string HandPrefix = "hand";
    const string HandSpritePath = "Assets/Art/ui/hand.png";
    const string PlayerName = BubuRunningGame.PlayerRootName;
    const float DefaultTriggerRadius = 0.6f;
    const float DefaultHandWorldHeight = 0.45f;
    static readonly Vector3 DefaultHandOffset = new Vector3(0f, 0.75f, -0.1f);

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Setup Hand Prompts")]
    public static void SetupFromMenu()
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
        catch (Exception exception)
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
            Debug.LogWarning("Hand prompt setup skipped because there is no valid active scene.");
            return;
        }

        Sprite handSprite = LoadMainSprite(HandSpritePath);
        GameObject playerObject = FindSceneObjectByExactName(PlayerName);
        List<GameObject> targets = FindPromptTargets(TargetPrefix);

        foreach (GameObject target in targets)
        {
            string handName = GetMatchingHandName(target.name);
            GameObject handObject = EnsureHandObject(handName, target, handSprite);
            HandPromptTrigger2D prompt = target.GetComponent<HandPromptTrigger2D>();
            if (prompt == null)
            {
                prompt = target.AddComponent<HandPromptTrigger2D>();
            }

            prompt.playerObjectName = PlayerName;
            prompt.player = playerObject != null ? playerObject.transform : null;
            prompt.handObject = handObject;
            prompt.playerCollider = playerObject != null ? playerObject.GetComponentInChildren<Collider2D>(true) : null;
            prompt.triggerRadius = 0f;
            prompt.hideHandOnStart = true;
            prompt.useObjectBounds = true;
            prompt.requirePlayerTouch = true;
            prompt.SetHandVisible(false);

            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(prompt);
            EditorUtility.SetDirty(handObject);
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("Configured hand prompts for " + targets.Count + " prompt target(s).");
    }

    static GameObject EnsureHandObject(string handName, GameObject target, Sprite handSprite)
    {
        GameObject handObject = FindSceneObjectByExactName(handName);
        if (handObject == null)
        {
            handObject = new GameObject(handName);
            handObject.transform.position = target.transform.position + DefaultHandOffset;
        }

        if (handObject.transform.parent != target.transform)
        {
            handObject.transform.SetParent(target.transform, true);
        }

        if (handObject.transform.localPosition == Vector3.zero)
        {
            handObject.transform.localPosition = DefaultHandOffset;
        }
        else
        {
            handObject.transform.localPosition = new Vector3(
                handObject.transform.localPosition.x,
                handObject.transform.localPosition.y,
                DefaultHandOffset.z);
        }

        SpriteRenderer renderer = handObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = handObject.AddComponent<SpriteRenderer>();
        }

        if (handSprite != null)
        {
            renderer.sprite = handSprite;
            ScaleSpriteToWorldHeight(handObject.transform, handSprite, DefaultHandWorldHeight);
        }

        renderer.sortingOrder = GetFrontSortingOrder(target);
        handObject.SetActive(false);

        EditorUtility.SetDirty(renderer);
        return handObject;
    }

    static List<GameObject> FindPromptTargets(string prefix)
    {
        List<GameObject> targets = new List<GameObject>();
        GameObject[] sceneObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject sceneObject in sceneObjects)
        {
            if (sceneObject != null && IsSeriesName(sceneObject.name, prefix))
            {
                targets.Add(sceneObject);
            }
        }

        targets.Sort((first, second) => CompareSeriesNames(first.name, second.name, prefix));
        return targets;
    }

    static Sprite LoadMainSprite(string assetPath)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            return sprite;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite loadedSprite)
            {
                return loadedSprite;
            }
        }

        Debug.LogWarning("Hand prompt sprite was not found at " + assetPath + ".");
        return null;
    }

    static void ScaleSpriteToWorldHeight(Transform handTransform, Sprite sprite, float worldHeight)
    {
        if (sprite == null || sprite.bounds.size.y <= 0f)
        {
            return;
        }

        float scale = worldHeight / sprite.bounds.size.y;
        handTransform.localScale = new Vector3(scale, scale, 1f);
    }

    static int GetFrontSortingOrder(GameObject target)
    {
        int order = 100;
        Renderer renderer = target.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            order = renderer.sortingOrder + 20;
        }

        return order;
    }

    static string GetMatchingHandName(string targetName)
    {
        if (string.Equals(targetName, TargetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return HandPrefix;
        }

        string suffix = targetName.Substring(TargetPrefix.Length);
        return HandPrefix + suffix;
    }

    static bool IsSeriesName(string objectName, string prefix)
    {
        if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(prefix))
        {
            return false;
        }

        return string.Equals(objectName, prefix, StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith(prefix + " (", StringComparison.OrdinalIgnoreCase);
    }

    static int CompareSeriesNames(string firstName, string secondName, string prefix)
    {
        int firstIndex = GetSeriesIndex(firstName, prefix);
        int secondIndex = GetSeriesIndex(secondName, prefix);
        int indexCompare = firstIndex.CompareTo(secondIndex);
        if (indexCompare != 0)
        {
            return indexCompare;
        }

        return string.Compare(firstName, secondName, StringComparison.OrdinalIgnoreCase);
    }

    static int GetSeriesIndex(string objectName, string prefix)
    {
        if (string.Equals(objectName, prefix, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        int start = objectName.IndexOf('(');
        int end = objectName.IndexOf(')');
        if (start >= 0 && end > start)
        {
            string numberText = objectName.Substring(start + 1, end - start - 1);
            if (int.TryParse(numberText, out int index))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    static GameObject FindSceneObjectByExactName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject[] sceneObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
