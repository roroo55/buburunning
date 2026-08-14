using UnityEngine;

[DisallowMultipleComponent]
public class PatrollingSoldierVisual2D : MonoBehaviour
{
    public SpriteRenderer visualRenderer;
    public Sprite movingUpSprite;
    public Sprite movingDownSprite;
    public bool hideRootRenderer = true;
    public float movementEpsilon = 0.0001f;

    SpriteRenderer rootRenderer;
    float previousY;
    int lastVerticalDirection = -1;
    Vector2 forcedDirection;
    bool hasForcedDirection;

    void Awake()
    {
        CacheRenderers();
        previousY = transform.position.y;
        ApplyCurrentDirection();
    }

    void OnEnable()
    {
        CacheRenderers();
        previousY = transform.position.y;
        ApplyCurrentDirection();
    }

    void LateUpdate()
    {
        if (hasForcedDirection)
        {
            ApplyForcedDirection();
            previousY = transform.position.y;
            return;
        }

        float currentY = transform.position.y;
        float deltaY = currentY - previousY;

        if (deltaY > movementEpsilon)
        {
            lastVerticalDirection = 1;
        }
        else if (deltaY < -movementEpsilon)
        {
            lastVerticalDirection = -1;
        }

        previousY = currentY;
        ApplyCurrentDirection();
    }

    public void SetMovementDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= movementEpsilon * movementEpsilon)
        {
            return;
        }

        forcedDirection = direction.normalized;
        hasForcedDirection = true;
        ApplyForcedDirection();
    }

    void ApplyForcedDirection()
    {
        CacheRenderers();
        if (visualRenderer == null)
        {
            return;
        }

        if (Mathf.Abs(forcedDirection.y) >= Mathf.Abs(forcedDirection.x))
        {
            lastVerticalDirection = forcedDirection.y >= 0f ? 1 : -1;
            visualRenderer.flipX = false;
        }
        else
        {
            lastVerticalDirection = -1;
            visualRenderer.flipX = forcedDirection.x < 0f;
        }

        ApplyCurrentDirection();
    }

    void OnValidate()
    {
        movementEpsilon = Mathf.Max(0f, movementEpsilon);
        CacheRenderers();
        ApplyCurrentDirection();
    }

    public void SetInitialDirection(bool movingUp)
    {
        lastVerticalDirection = movingUp ? 1 : -1;
        ApplyCurrentDirection();
    }

    void ApplyCurrentDirection()
    {
        CacheRenderers();

        if (hideRootRenderer && rootRenderer != null && rootRenderer != visualRenderer)
        {
            rootRenderer.enabled = false;
        }

        if (visualRenderer == null)
        {
            return;
        }

        Sprite targetSprite = lastVerticalDirection > 0 ? movingUpSprite : movingDownSprite;
        if (targetSprite != null && visualRenderer.sprite != targetSprite)
        {
            visualRenderer.sprite = targetSprite;
        }

        visualRenderer.color = Color.white;
    }

    void CacheRenderers()
    {
        if (rootRenderer == null)
        {
            rootRenderer = GetComponent<SpriteRenderer>();
        }

        if (visualRenderer == null)
        {
            Transform visualTransform = transform.Find("Yinbing Visual");
            if (visualTransform != null)
            {
                visualRenderer = visualTransform.GetComponent<SpriteRenderer>();
            }
        }
    }
}
