using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaLayout : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void OnEnable()
    {
        ApplySafeArea();
    }

    private void Update()
    {
        Vector2Int screenSize =
            new(Screen.width, Screen.height);

        if (Screen.safeArea == lastSafeArea &&
            screenSize == lastScreenSize)
        {
            return;
        }

        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        Rect safeArea = Screen.safeArea;
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax =
            safeArea.position + safeArea.size;
        anchorMin.x /= width;
        anchorMin.y /= height;
        anchorMax.x /= width;
        anchorMax.y /= height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(
            Screen.width,
            Screen.height
        );
    }
}
