using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MazeWallCollisionRestoreSceneSetup
{
    const string RequestPath =
        "Assets/Editor/CodexMazeWallCollisionRestoreRequest.txt";
    const string TargetScenePath = "Assets/Scenes/关卡1.unity";
    const string MazeName = "maze";
    const string MazeKeyName = "key for maze";
    const string GeneratedWallsName = "Maze Colliders";
    const string CustomWallsName = "KeyWalls";
    const string MazeKeyLayerName = "MazeKey";
    const string MazeWallLayerName = "MazeKeyWall";

    [InitializeOnLoadMethod]
    static void RunRequestedSetupAfterReload()
    {
        EditorApplication.delayCall += ProcessRequestedSetup;
    }

    [MenuItem("Bubu Running/Restore Both Maze Wall Collisions")]
    public static void SetupFromMenu()
    {
        RestoreActiveScene();
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

        if (!RestoreActiveScene())
        {
            return;
        }

        AssetDatabase.DeleteAsset(RequestPath);
        AssetDatabase.SaveAssets();
    }

    static bool RestoreActiveScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.path != TargetScenePath)
        {
            Debug.LogWarning(
                "Maze wall collision restore is waiting for "
                + TargetScenePath
                + ".");
            return false;
        }

        GameObject maze = FindSceneObjectByExactName(MazeName);
        GameObject mazeKey = FindSceneObjectByExactName(MazeKeyName);
        GameObject generatedWalls =
            FindSceneObjectByExactName(GeneratedWallsName);
        GameObject customWalls =
            FindSceneObjectByExactName(CustomWallsName);
        if (maze == null
            || mazeKey == null
            || generatedWalls == null
            || customWalls == null)
        {
            Debug.LogError(
                "Maze wall collision restore requires maze, key for maze, "
                + "Maze Colliders and KeyWalls.");
            return false;
        }

        int keyLayer = LayerMask.NameToLayer(MazeKeyLayerName);
        int wallLayer = LayerMask.NameToLayer(MazeWallLayerName);
        if (keyLayer < 0 || wallLayer < 0)
        {
            Debug.LogError(
                "Maze wall collision restore could not find MazeKey "
                + "or MazeKeyWall physics layers.");
            return false;
        }

        SetLayerRecursively(mazeKey.transform, keyLayer);
        ConfigureWallRoot(generatedWalls.transform, wallLayer);
        ConfigureWallRoot(customWalls.transform, wallLayer);

        Rigidbody2D keyBody =
            GetOrAddComponent<Rigidbody2D>(mazeKey);
        keyBody.bodyType = RigidbodyType2D.Dynamic;
        keyBody.simulated = true;
        keyBody.gravityScale = 0f;
        keyBody.freezeRotation = true;
        keyBody.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;
        keyBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        keyBody.sleepMode = RigidbodySleepMode2D.NeverSleep;

        MazeKeyDrag2D keyDrag = mazeKey.GetComponent<MazeKeyDrag2D>();
        Collider2D keyCollider =
            keyDrag != null
                ? keyDrag.CollisionCollider
                : mazeKey.GetComponentInChildren<Collider2D>(true);
        if (keyCollider == null)
        {
            Debug.LogError(
                "Maze wall collision restore could not find the maze key Collider2D.");
            return false;
        }

        keyCollider.enabled = true;
        keyCollider.isTrigger = false;
        Physics2D.IgnoreLayerCollision(keyLayer, wallLayer, false);

        EditorUtility.SetDirty(maze);
        EditorUtility.SetDirty(mazeKey);
        EditorUtility.SetDirty(keyBody);
        EditorUtility.SetDirty(keyCollider);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log(
            "Restored solid maze-key collision for both Maze Colliders "
            + "and the existing KeyWalls without deleting or moving KeyWalls.");
        return true;
    }

    static void ConfigureWallRoot(Transform wallRoot, int wallLayer)
    {
        wallRoot.gameObject.SetActive(true);
        SetLayerRecursively(wallRoot, wallLayer);

        Rigidbody2D wallBody =
            GetOrAddComponent<Rigidbody2D>(wallRoot.gameObject);
        wallBody.bodyType = RigidbodyType2D.Static;
        wallBody.simulated = true;
        wallBody.gravityScale = 0f;

        CompositeCollider2D composite =
            GetOrAddComponent<CompositeCollider2D>(wallRoot.gameObject);
        composite.enabled = true;
        composite.isTrigger = false;
        composite.geometryType =
            CompositeCollider2D.GeometryType.Polygons;
        composite.generationType =
            CompositeCollider2D.GenerationType.Synchronous;

        Collider2D[] colliders =
            wallRoot.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D wallCollider in colliders)
        {
            if (wallCollider == null || wallCollider == composite)
            {
                continue;
            }

            wallCollider.enabled = true;
            wallCollider.isTrigger = false;
            wallCollider.compositeOperation =
                Collider2D.CompositeOperation.Merge;
            EditorUtility.SetDirty(wallCollider);
        }

        composite.GenerateGeometry();
        EditorUtility.SetDirty(wallRoot.gameObject);
        EditorUtility.SetDirty(wallBody);
        EditorUtility.SetDirty(composite);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        Transform[] descendants =
            root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            descendant.gameObject.layer = layer;
            EditorUtility.SetDirty(descendant.gameObject);
        }
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
