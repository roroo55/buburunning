using UnityEngine;

[DisallowMultipleComponent]
public class ScreenOcclusionSorter2D : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Collider2D playerCollider;
    public SpriteRenderer screenRenderer;

    [Header("Front / Behind Boundary")]
    [Tooltip("World-space offset from the bottom edge of the screen sprite.")]
    public float boundaryYOffset;

    [Tooltip("Additional offset applied to the player's feet position.")]
    public float playerFeetYOffset;

    [Header("Sorting Orders")]
    [Tooltip("Used when the player is above/behind the screen boundary.")]
    public int screenOrderWhenPlayerBehind = 6;

    [Tooltip("Used when the player is below/in front of the screen boundary.")]
    public int screenOrderWhenPlayerInFront = 4;

    [Header("Scene Debug")]
    public bool showDebugBoundary = true;
    public Color debugBoundaryColor = Color.cyan;
    [Min(0.1f)] public float debugBoundaryWidth = 4f;

    void Awake()
    {
        CacheMissingReferences();
        UpdateSortingOrder();
    }

    void LateUpdate()
    {
        CacheMissingReferences();
        UpdateSortingOrder();
    }

    void CacheMissingReferences()
    {
        if (screenRenderer == null)
        {
            screenRenderer = GetComponent<SpriteRenderer>();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.Find(BubuRunningGame.PlayerRootName);
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (playerCollider == null && player != null)
        {
            playerCollider = player.GetComponentInChildren<Collider2D>();
        }
    }

    void UpdateSortingOrder()
    {
        if (player == null || screenRenderer == null)
        {
            return;
        }

        float boundaryY = GetBoundaryY();
        float playerFeetY = playerCollider != null
            ? playerCollider.bounds.min.y
            : player.position.y;
        playerFeetY += playerFeetYOffset;

        bool playerIsBehind = playerFeetY > boundaryY;
        screenRenderer.sortingOrder = playerIsBehind
            ? screenOrderWhenPlayerBehind
            : screenOrderWhenPlayerInFront;
    }

    float GetBoundaryY()
    {
        return (screenRenderer != null ? screenRenderer.bounds.min.y : transform.position.y)
            + boundaryYOffset;
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebugBoundary)
        {
            return;
        }

        if (screenRenderer == null)
        {
            screenRenderer = GetComponent<SpriteRenderer>();
        }

        float boundaryY = GetBoundaryY();
        float halfWidth = Mathf.Max(0.1f, debugBoundaryWidth) * 0.5f;
        Gizmos.color = debugBoundaryColor;
        Gizmos.DrawLine(
            new Vector3(transform.position.x - halfWidth, boundaryY, transform.position.z),
            new Vector3(transform.position.x + halfWidth, boundaryY, transform.position.z));
    }
}
