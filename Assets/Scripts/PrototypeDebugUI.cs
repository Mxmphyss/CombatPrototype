using UnityEngine;
using UnityEngine.UI;

public sealed class PrototypeDebugUI : MonoBehaviour
{
    private static readonly Color PanelColor =
        new(0.025f, 0.035f, 0.055f, 0.88f);
    private static readonly Color EnabledColor =
        new(0.18f, 0.55f, 0.32f, 0.96f);
    private static readonly Color PausedColor =
        new(0.72f, 0.35f, 0.14f, 0.96f);

    private EnemyAutoCombat enemyAI;
    private GestureDebugDisplay gestureDisplay;
    private Button aiToggleButton;
    private Image aiToggleImage;
    private Text aiToggleLabel;

    public static PrototypeDebugUI Create(
        Transform parent,
        EnemyAutoCombat enemyAutoCombat,
        CombatGestureGrid gestureGrid)
    {
        GameObject panelObject =
            new("Prototype Combat Debug UI");
        panelObject.transform.SetParent(parent, false);

        RectTransform rect =
            panelObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 760f);
        rect.sizeDelta = new Vector2(960f, 280f);

        Image background =
            panelObject.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = false;

        Outline outline =
            panelObject.AddComponent<Outline>();
        outline.effectColor =
            new Color(0.55f, 0.72f, 0.9f, 0.32f);
        outline.effectDistance = new Vector2(2f, -2f);

        PrototypeDebugUI debugUI =
            panelObject.AddComponent<PrototypeDebugUI>();
        debugUI.Initialize(enemyAutoCombat, gestureGrid);
        return debugUI;
    }

    private void Initialize(
        EnemyAutoCombat enemyAutoCombat,
        CombatGestureGrid gestureGrid)
    {
        enemyAI = enemyAutoCombat;
        BuildTitle();
        BuildAIToggle();

        gestureDisplay =
            gameObject.AddComponent<GestureDebugDisplay>();
        gestureDisplay.Initialize(gestureGrid);

        if (enemyAI != null)
        {
            enemyAI.OnAIEnabledChanged +=
                HandleAIEnabledChanged;
        }

        RefreshAIToggle();
    }

    private void OnDestroy()
    {
        if (enemyAI != null)
        {
            enemyAI.OnAIEnabledChanged -=
                HandleAIEnabledChanged;
        }
    }

    public void ResetForReplay()
    {
        gestureDisplay?.Clear();
        RefreshAIToggle();
    }

    private void BuildTitle()
    {
        GameObject titleObject = new("Prototype Label");
        titleObject.transform.SetParent(transform, false);

        RectTransform rect =
            titleObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -16f);
        rect.sizeDelta = new Vector2(560f, 38f);

        Text title = titleObject.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf"
        );
        title.fontSize = 24;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(0.62f, 0.82f, 1f, 1f);
        title.text = "OUTILS PROTOTYPE · GESTURE PAD";
        title.raycastTarget = false;
    }

    private void BuildAIToggle()
    {
        GameObject buttonObject =
            new("Prototype Enemy AI Toggle");
        buttonObject.transform.SetParent(transform, false);

        RectTransform rect =
            buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -14f);
        rect.sizeDelta = new Vector2(330f, 48f);

        aiToggleImage = buttonObject.AddComponent<Image>();
        aiToggleImage.sprite = null;

        aiToggleButton =
            buttonObject.AddComponent<Button>();
        aiToggleButton.onClick.AddListener(ToggleEnemyAI);

        GameObject labelObject = new("Label");
        labelObject.transform.SetParent(
            buttonObject.transform,
            false
        );

        RectTransform labelRect =
            labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        aiToggleLabel = labelObject.AddComponent<Text>();
        aiToggleLabel.font =
            Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf"
            );
        aiToggleLabel.fontSize = 21;
        aiToggleLabel.fontStyle = FontStyle.Bold;
        aiToggleLabel.alignment =
            TextAnchor.MiddleCenter;
        aiToggleLabel.color = Color.white;
        aiToggleLabel.raycastTarget = false;
    }

    private void ToggleEnemyAI()
    {
        if (enemyAI == null)
            return;

        enemyAI.SetAIEnabled(!enemyAI.EnemyAIEnabled);
    }

    private void HandleAIEnabledChanged(bool enabled)
    {
        RefreshAIToggle();
    }

    private void RefreshAIToggle()
    {
        bool available = enemyAI != null;
        bool enabled =
            available && enemyAI.EnemyAIEnabled;

        if (aiToggleButton != null)
            aiToggleButton.interactable = available;

        if (aiToggleImage != null)
        {
            aiToggleImage.color = enabled
                ? EnabledColor
                : PausedColor;
        }

        if (aiToggleLabel != null)
        {
            aiToggleLabel.text = !available
                ? "PROTO · IA INDISPONIBLE"
                : enabled
                    ? "PROTO · IA ACTIVE"
                    : "PROTO · IA EN PAUSE";
        }
    }
}
