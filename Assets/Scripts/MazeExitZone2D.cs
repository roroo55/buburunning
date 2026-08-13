using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class MazeExitZone2D : MonoBehaviour
{
    public MazePuzzleController mazePuzzleController;
    public Color gizmoColor = new Color(0.2f, 1f, 0.35f, 0.65f);
    public bool drawGizmo = true;

    Collider2D exitCollider;

    public Collider2D ExitCollider
    {
        get
        {
            CacheCollider();
            return exitCollider;
        }
    }

    void Awake()
    {
        CacheCollider();
    }

    void OnValidate()
    {
        CacheCollider();
    }

    public void MarkSolvedByKey(MazeKeyDrag2D keyDrag)
    {
        if (mazePuzzleController != null)
        {
            mazePuzzleController.NotifyMazeSolved();
        }
    }

    void CacheCollider()
    {
        if (exitCollider == null)
        {
            exitCollider = GetComponent<Collider2D>();
        }

        if (exitCollider != null)
        {
            exitCollider.isTrigger = true;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmo)
        {
            return;
        }

        Collider2D zoneCollider = GetComponent<Collider2D>();
        Gizmos.color = gizmoColor;
        if (zoneCollider != null)
        {
            Gizmos.DrawWireCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
    }
}
