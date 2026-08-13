using UnityEngine;

[DisallowMultipleComponent]
public class MazePuzzleController : MonoBehaviour
{
    public GameObject mazeRoot;
    public GameObject keyForMaze;
    public Transform keyStartPoint;
    public MazeKeyDrag2D mazeKeyDrag;
    public DoorProgressGate2D doorGate;
    public bool showMazeOnStart;
    public bool prerequisiteMet;
    public bool restoreKeyComponentsWhenShown = true;
    public bool resetKeyToStartWhenShown = true;
    public bool unlockDoorWhenSolved = true;
    public bool hideMazeWhenSolved = true;
    public bool mazeSolved;

    void Awake()
    {
        ApplyVisibility(showMazeOnStart || prerequisiteMet);
    }

    public void SetPrerequisiteMet(bool isMet)
    {
        prerequisiteMet = isMet;
        ApplyVisibility(prerequisiteMet);
    }

    public void ShowMaze()
    {
        prerequisiteMet = true;
        mazeSolved = false;
        ApplyVisibility(true);
        ResetMazeKeyToStart();
    }

    public void HideMaze()
    {
        ApplyVisibility(false);
    }

    public void NotifyMazeSolved()
    {
        if (mazeSolved)
        {
            return;
        }

        mazeSolved = true;

        if (unlockDoorWhenSolved && doorGate != null)
        {
            doorGate.SetPuzzleSolved(true);
        }

        if (hideMazeWhenSolved)
        {
            HideMaze();
        }
    }

    void ApplyVisibility(bool visible)
    {
        if (mazeRoot != null)
        {
            mazeRoot.SetActive(visible);
        }

        if (keyForMaze != null)
        {
            keyForMaze.SetActive(visible);
            if (visible && restoreKeyComponentsWhenShown)
            {
                SetChildComponentsEnabled(keyForMaze, true);
            }
        }
    }

    void ResetMazeKeyToStart()
    {
        if (!resetKeyToStartWhenShown)
        {
            return;
        }

        if (mazeKeyDrag != null)
        {
            mazeKeyDrag.ResetToStart();
            return;
        }

        if (keyForMaze != null && keyStartPoint != null)
        {
            keyForMaze.transform.position = keyStartPoint.position;
        }
    }

    static void SetChildComponentsEnabled(GameObject targetObject, bool enabled)
    {
        MazeKeyDrag2D keyDrag = targetObject.GetComponent<MazeKeyDrag2D>();
        Collider2D dragControlCollider = enabled && keyDrag != null ? keyDrag.DragControlCollider : null;
        Collider2D collisionCollider = enabled && keyDrag != null ? keyDrag.CollisionCollider : null;

        SpriteRenderer[] spriteRenderers = targetObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer targetRenderer in spriteRenderers)
        {
            targetRenderer.enabled = enabled;
        }

        Collider2D[] colliders2D = targetObject.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D targetCollider in colliders2D)
        {
            targetCollider.enabled = keyDrag == null
                ? enabled
                : enabled && (targetCollider == dragControlCollider || targetCollider == collisionCollider);
        }
    }
}
