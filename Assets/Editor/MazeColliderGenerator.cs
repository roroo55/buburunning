using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MazeColliderGenerator
{
    const string RequestPath = "Assets/Editor/CodexMazeColliderGenerationRequest.txt";
    const string MazeObjectName = "maze";
    const string ColliderRootName = "Maze Colliders";
    const float MinimumRed = 0.2f;
    const float RedDominance = 1.25f;
    const float MinimumRedDifference = 0.06f;
    const float AlphaThreshold = 0.1f;
    const int CellSizePixels = 8;
    const float CellWallCoverage = 0.22f;
    const int MinimumColliderCells = 1;

    [InitializeOnLoadMethod]
    static void RunRequestedGenerationAfterReload()
    {
        EditorApplication.delayCall += ProcessRequest;
    }

    static void ProcessRequest()
    {
        if (AssetDatabase.LoadAssetAtPath<TextAsset>(RequestPath) == null)
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += ProcessRequest;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ProcessRequest;
            return;
        }

        GenerateForActiveScene();
        AssetDatabase.DeleteAsset(RequestPath);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Bubu Running/Generate Maze Colliders")]
    public static void GenerateForActiveScene()
    {
        GameObject maze = GameObject.Find(MazeObjectName);
        if (maze == null)
        {
            Debug.LogError("Maze collider generation failed: could not find GameObject named 'maze'.");
            return;
        }

        SpriteRenderer spriteRenderer = maze.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogError("Maze collider generation failed: 'maze' needs a SpriteRenderer with a Sprite.");
            return;
        }

        Texture2D readableTexture = LoadReadableTexture(spriteRenderer.sprite);
        if (readableTexture == null)
        {
            Debug.LogError("Maze collider generation failed: could not read the maze texture.");
            return;
        }

        List<PixelRect> wallRects = BuildWallRects(spriteRenderer.sprite, readableTexture);
        Transform colliderRoot = RecreateColliderRoot(maze.transform);
        CreateColliders(spriteRenderer.sprite, colliderRoot, wallRects);

        Object.DestroyImmediate(readableTexture);
        EditorUtility.SetDirty(maze);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("Generated " + wallRects.Count + " maze wall colliders under '" + MazeObjectName + "'.");
    }

    static Texture2D LoadReadableTexture(Sprite sprite)
    {
        string assetPath = AssetDatabase.GetAssetPath(sprite.texture);
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(absolutePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes))
        {
            Object.DestroyImmediate(texture);
            return null;
        }

        return texture;
    }

    static List<PixelRect> BuildWallRects(Sprite sprite, Texture2D texture)
    {
        Rect spriteRect = sprite.rect;
        int width = Mathf.RoundToInt(spriteRect.width);
        int height = Mathf.RoundToInt(spriteRect.height);
        int textureX = Mathf.RoundToInt(spriteRect.x);
        int textureY = Mathf.RoundToInt(spriteRect.y);
        int columns = Mathf.CeilToInt(width / (float)CellSizePixels);
        int rows = Mathf.CeilToInt(height / (float)CellSizePixels);
        bool[,] wallMask = new bool[columns, rows];

        for (int cellY = 0; cellY < rows; cellY++)
        {
            for (int cellX = 0; cellX < columns; cellX++)
            {
                wallMask[cellX, cellY] = IsWallCell(texture, textureX, textureY, width, height, cellX, cellY);
            }
        }

        List<PixelRect> cellRects = MergeWallPixelsIntoRects(wallMask, columns, rows);
        return ConvertCellRectsToPixelRects(cellRects, width, height);
    }

    static bool IsRedWall(Color color)
    {
        if (color.a < AlphaThreshold || color.r < MinimumRed)
        {
            return false;
        }

        float strongestNonRed = Mathf.Max(color.g, color.b);
        return color.r > color.g * RedDominance
            && color.r > color.b * RedDominance
            && color.r - strongestNonRed >= MinimumRedDifference;
    }

    static bool IsWallCell(Texture2D texture, int textureX, int textureY, int spriteWidth, int spriteHeight, int cellX, int cellY)
    {
        int startX = cellX * CellSizePixels;
        int startY = cellY * CellSizePixels;
        int endX = Mathf.Min(startX + CellSizePixels, spriteWidth);
        int endY = Mathf.Min(startY + CellSizePixels, spriteHeight);
        int redPixels = 0;
        int totalPixels = 0;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                totalPixels++;
                if (IsRedWall(texture.GetPixel(textureX + x, textureY + y)))
                {
                    redPixels++;
                }
            }
        }

        return totalPixels > 0 && redPixels / (float)totalPixels >= CellWallCoverage;
    }

    static List<PixelRect> ConvertCellRectsToPixelRects(List<PixelRect> cellRects, int spriteWidth, int spriteHeight)
    {
        List<PixelRect> pixelRects = new List<PixelRect>();
        foreach (PixelRect cellRect in cellRects)
        {
            int x = cellRect.x * CellSizePixels;
            int y = cellRect.y * CellSizePixels;
            int maxX = Mathf.Min((cellRect.x + cellRect.width) * CellSizePixels, spriteWidth);
            int maxY = Mathf.Min((cellRect.y + cellRect.height) * CellSizePixels, spriteHeight);
            pixelRects.Add(new PixelRect(x, y, maxX - x, maxY - y));
        }

        return pixelRects;
    }

    static List<PixelRect> MergeWallPixelsIntoRects(bool[,] wallMask, int width, int height)
    {
        List<PixelRect> completedRects = new List<PixelRect>();
        Dictionary<RowSpan, PixelRect> activeRects = new Dictionary<RowSpan, PixelRect>();

        for (int y = 0; y < height; y++)
        {
            List<RowSpan> spans = GetRowSpans(wallMask, width, y);
            Dictionary<RowSpan, PixelRect> nextActiveRects = new Dictionary<RowSpan, PixelRect>();

            foreach (RowSpan span in spans)
            {
                if (activeRects.TryGetValue(span, out PixelRect existingRect))
                {
                    existingRect.height += 1;
                    nextActiveRects[span] = existingRect;
                    activeRects.Remove(span);
                }
                else
                {
                    nextActiveRects[span] = new PixelRect(span.x, y, span.width, 1);
                }
            }

            foreach (PixelRect finishedRect in activeRects.Values)
            {
                AddIfLargeEnough(completedRects, finishedRect);
            }

            activeRects = nextActiveRects;
        }

        foreach (PixelRect finishedRect in activeRects.Values)
        {
            AddIfLargeEnough(completedRects, finishedRect);
        }

        return completedRects;
    }

    static List<RowSpan> GetRowSpans(bool[,] wallMask, int width, int y)
    {
        List<RowSpan> spans = new List<RowSpan>();
        int x = 0;
        while (x < width)
        {
            while (x < width && !wallMask[x, y])
            {
                x++;
            }

            int startX = x;
            while (x < width && wallMask[x, y])
            {
                x++;
            }

            int spanWidth = x - startX;
            if (spanWidth >= MinimumColliderCells)
            {
                spans.Add(new RowSpan(startX, spanWidth));
            }
        }

        return spans;
    }

    static void AddIfLargeEnough(List<PixelRect> rects, PixelRect rect)
    {
        if (rect.width < MinimumColliderCells && rect.height < MinimumColliderCells)
        {
            return;
        }

        rects.Add(rect);
    }

    static Transform RecreateColliderRoot(Transform mazeTransform)
    {
        Transform existingRoot = mazeTransform.Find(ColliderRootName);
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot.gameObject);
        }

        GameObject root = new GameObject(ColliderRootName);
        root.transform.SetParent(mazeTransform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        return root.transform;
    }

    static void CreateColliders(Sprite sprite, Transform colliderRoot, List<PixelRect> rects)
    {
        float pixelsPerUnit = sprite.pixelsPerUnit;
        Vector2 pivot = sprite.pivot;

        for (int i = 0; i < rects.Count; i++)
        {
            PixelRect rect = rects[i];
            GameObject colliderObject = new GameObject("Wall Collider " + (i + 1).ToString("000"));
            colliderObject.transform.SetParent(colliderRoot, false);
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;

            float centerX = (rect.x + rect.width * 0.5f - pivot.x) / pixelsPerUnit;
            float centerY = (rect.y + rect.height * 0.5f - pivot.y) / pixelsPerUnit;
            colliderObject.transform.localPosition = new Vector3(centerX, centerY, 0f);

            BoxCollider2D boxCollider = colliderObject.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = false;
            boxCollider.offset = Vector2.zero;
            boxCollider.size = new Vector2(rect.width / pixelsPerUnit, rect.height / pixelsPerUnit);
        }
    }

    struct RowSpan
    {
        public readonly int x;
        public readonly int width;

        public RowSpan(int x, int width)
        {
            this.x = x;
            this.width = width;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (x * 397) ^ width;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is not RowSpan other)
            {
                return false;
            }

            return x == other.x && width == other.width;
        }
    }

    class PixelRect
    {
        public readonly int x;
        public readonly int y;
        public readonly int width;
        public int height;

        public PixelRect(int x, int y, int width, int height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }
}
