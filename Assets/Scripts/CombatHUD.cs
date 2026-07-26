using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatHUD : MonoBehaviour
{
    private static readonly Color PanelColor =
        new(0.025f, 0.035f, 0.055f, 0.78f);
    private static readonly Color BorderColor =
        new(0.55f, 0.64f, 0.76f, 0.22f);
    private static readonly Color HealthColor =
        new(0.63f, 0.16f, 0.18f, 1f);
    private static readonly Color HealthLowColor =
        new(0.95f, 0.26f, 0.22f, 1f);
    private static readonly Color StaminaColor =
        new(0.18f, 0.58f, 0.55f, 1f);
    private static readonly Color StaminaLowColor =
        new(0.88f, 0.62f, 0.16f, 1f);
    private static readonly Color TextColor =
        new(0.92f, 0.94f, 0.98f, 1f);

    private FighterCombat playerCombat;
    private FighterCombat enemyCombat;
    private FighterStats playerStats;
    private FighterStats enemyStats;
    private EnemyAutoCombat enemyAI;
    private CombatGestureGrid gestureGrid;

    private StatBar playerHealthBar;
    private StatBar playerStaminaBar;
    private StatBar enemyHealthBar;
    private StatBar enemyStaminaBar;
    private Text enemyStatusText;
    private Text feedbackText;
    private CanvasGroup feedbackGroup;
    private Coroutine feedbackRoutine;
    private bool battleEnded;

    public bool BattleEnded => battleEnded;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForCombatScene()
    {
        if (FindFirstObjectByType<CombatHUD>() != null)
            return;

        FighterCombat[] fighters =
            FindObjectsByType<FighterCombat>(
                FindObjectsSortMode.None
            );

        FighterCombat player = null;
        FighterCombat enemy = null;

        foreach (FighterCombat fighter in fighters)
        {
            if (fighter.IsPlayerControlled)
                player = fighter;
            else if (enemy == null)
                enemy = fighter;
        }

        if (player == null || enemy == null)
            return;

        EnsureEventSystem();

        GameObject canvasObject = new("Combat UI Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler =
            canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(1080f, 1920f);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        CombatHUD hud = canvasObject.AddComponent<CombatHUD>();
        hud.Initialize(player, enemy);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<
            UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private void Initialize(
        FighterCombat player,
        FighterCombat enemy)
    {
        playerCombat = player;
        enemyCombat = enemy;
        playerStats = player.Stats;
        enemyStats = enemy.Stats;

        if (playerStats == null || enemyStats == null)
        {
            Debug.LogError(
                "CombatHUD requires FighterStats on both fighters."
            );
            enabled = false;
            return;
        }

        RectTransform safeRoot = CreateSafeAreaRoot();
        BuildEnemyPanel(safeRoot);
        BuildPlayerPanel(safeRoot);
        BuildFeedback(safeRoot);

        gestureGrid = CombatGestureGrid.Create(
            safeRoot,
            playerCombat,
            this
        );

        Subscribe();
        RefreshAll();
        AttachEnemyAI();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
    }

    public void ShowMessage(
        string message,
        Color color,
        float duration = 1.2f,
        bool persistent = false)
    {
        if (feedbackText == null ||
            (battleEnded && !persistent))
        {
            return;
        }

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackText.text = message;
        feedbackText.color = color;
        feedbackGroup.alpha = 1f;

        if (persistent)
        {
            feedbackRoutine = null;
            return;
        }

        feedbackRoutine = StartCoroutine(
            FadeMessageRoutine(duration)
        );
    }

    public void SetEnemyStatus(string status)
    {
        if (enemyStatusText != null)
            enemyStatusText.text = status;
    }

    private RectTransform CreateSafeAreaRoot()
    {
        GameObject rootObject = new("Combat Safe Area");
        rootObject.transform.SetParent(transform, false);

        RectTransform rect =
            rootObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        rootObject.AddComponent<SafeAreaLayout>();
        return rect;
    }

    private void BuildEnemyPanel(Transform parent)
    {
        RectTransform panel = CreatePanel(
            parent,
            "Enemy HUD",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -22f),
            new Vector2(920f, 176f)
        );

        Text enemyName = CreateText(
            panel,
            "Enemy Name",
            enemyCombat.gameObject.name,
            31,
            FontStyle.Bold,
            TextAnchor.MiddleCenter,
            new Vector2(0f, 66f),
            new Vector2(760f, 40f)
        );
        enemyName.color = TextColor;

        enemyHealthBar = CreateStatBar(
            panel,
            "Enemy Health",
            new Vector2(0f, 23f),
            new Vector2(760f, 34f),
            HealthColor,
            HealthLowColor,
            22
        );

        enemyStaminaBar = CreateStatBar(
            panel,
            "Enemy Stamina",
            new Vector2(0f, -17f),
            new Vector2(760f, 20f),
            StaminaColor,
            StaminaLowColor,
            18
        );

        enemyStatusText = CreateText(
            panel,
            "Enemy Status",
            string.Empty,
            21,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            new Vector2(0f, -57f),
            new Vector2(760f, 30f)
        );
        enemyStatusText.color =
            new Color(0.82f, 0.86f, 0.92f, 0.88f);
    }

    private void BuildPlayerPanel(Transform parent)
    {
        RectTransform panel = CreatePanel(
            parent,
            "Player HUD",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 18f),
            new Vector2(940f, 166f)
        );

        playerHealthBar = CreateStatBar(
            panel,
            "Player Health",
            new Vector2(0f, 34f),
            new Vector2(820f, 46f),
            HealthColor,
            HealthLowColor,
            25
        );

        playerStaminaBar = CreateStatBar(
            panel,
            "Player Stamina",
            new Vector2(0f, -31f),
            new Vector2(820f, 34f),
            StaminaColor,
            StaminaLowColor,
            22
        );
    }

    private void BuildFeedback(Transform parent)
    {
        GameObject feedbackObject = new("Combat Feedback");
        feedbackObject.transform.SetParent(parent, false);

        RectTransform rect =
            feedbackObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 135f);
        rect.sizeDelta = new Vector2(900f, 100f);

        feedbackGroup =
            feedbackObject.AddComponent<CanvasGroup>();
        feedbackGroup.alpha = 0f;
        feedbackGroup.blocksRaycasts = false;
        feedbackGroup.interactable = false;

        feedbackText = feedbackObject.AddComponent<Text>();
        feedbackText.font = GetFont();
        feedbackText.alignment = TextAnchor.MiddleCenter;
        feedbackText.fontStyle = FontStyle.Bold;
        feedbackText.fontSize = 38;
        feedbackText.color = TextColor;
        feedbackText.raycastTarget = false;
        feedbackText.horizontalOverflow =
            HorizontalWrapMode.Wrap;
        feedbackText.verticalOverflow =
            VerticalWrapMode.Overflow;

        Outline outline = feedbackObject.AddComponent<Outline>();
        outline.effectColor =
            new Color(0f, 0f, 0f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void Subscribe()
    {
        playerStats.OnHealthChanged += UpdatePlayerHealth;
        playerStats.OnStaminaChanged += UpdatePlayerStamina;
        playerStats.OnDeath += HandleFighterDeath;

        enemyStats.OnHealthChanged += UpdateEnemyHealth;
        enemyStats.OnStaminaChanged += UpdateEnemyStamina;
        enemyStats.OnDeath += HandleFighterDeath;

        playerCombat.OnAttackResolved += HandleAttackResolved;
        enemyCombat.OnAttackResolved += HandleAttackResolved;
        enemyCombat.OnStateChanged += HandleEnemyStateChanged;
    }

    private void Unsubscribe()
    {
        if (playerStats != null)
        {
            playerStats.OnHealthChanged -= UpdatePlayerHealth;
            playerStats.OnStaminaChanged -=
                UpdatePlayerStamina;
            playerStats.OnDeath -= HandleFighterDeath;
        }

        if (enemyStats != null)
        {
            enemyStats.OnHealthChanged -= UpdateEnemyHealth;
            enemyStats.OnStaminaChanged -=
                UpdateEnemyStamina;
            enemyStats.OnDeath -= HandleFighterDeath;
        }

        if (playerCombat != null)
            playerCombat.OnAttackResolved -=
                HandleAttackResolved;

        if (enemyCombat != null)
        {
            enemyCombat.OnAttackResolved -=
                HandleAttackResolved;
            enemyCombat.OnStateChanged -=
                HandleEnemyStateChanged;
        }
    }

    private void RefreshAll()
    {
        UpdatePlayerHealth(
            playerStats.CurrentHealth,
            playerStats.MaxHealth
        );
        UpdatePlayerStamina(
            playerStats.CurrentStamina,
            playerStats.MaxStamina
        );
        UpdateEnemyHealth(
            enemyStats.CurrentHealth,
            enemyStats.MaxHealth
        );
        UpdateEnemyStamina(
            enemyStats.CurrentStamina,
            enemyStats.MaxStamina
        );
    }

    private void AttachEnemyAI()
    {
        enemyAI = enemyCombat.GetComponent<EnemyAutoCombat>();
        if (enemyAI == null)
            enemyAI =
                enemyCombat.gameObject.AddComponent<
                    EnemyAutoCombat>();

        enemyAI.Initialize(enemyCombat, playerCombat, this);
    }

    private void UpdatePlayerHealth(float current, float maximum)
    {
        playerHealthBar?.SetValue(current, maximum);
    }

    private void UpdatePlayerStamina(
        float current,
        float maximum)
    {
        playerStaminaBar?.SetValue(current, maximum);
    }

    private void UpdateEnemyHealth(float current, float maximum)
    {
        enemyHealthBar?.SetValue(current, maximum);
    }

    private void UpdateEnemyStamina(
        float current,
        float maximum)
    {
        enemyStaminaBar?.SetValue(current, maximum);
    }

    private void HandleEnemyStateChanged(
        FighterCombat fighter,
        FighterCombatState state)
    {
        if (battleEnded)
            return;

        string label = state switch
        {
            FighterCombatState.Attacking => "Attaque",
            FighterCombatState.Defending => "Garde",
            FighterCombatState.HoldingGuard =>
                "Garde maintenue",
            FighterCombatState.Charging => "Recharge",
            FighterCombatState.Dodging => "Esquive",
            FighterCombatState.Dead => "Vaincu",
            _ => string.Empty
        };

        SetEnemyStatus(label);
    }

    private void HandleAttackResolved(
        FighterCombat target,
        CombatHitResult result)
    {
        if (battleEnded || target != playerCombat)
            return;

        if (result == CombatHitResult.Blocked)
        {
            ShowMessage(
                "Garde reussie",
                new Color(0.35f, 0.72f, 0.94f),
                1.1f
            );
        }
        else if (result == CombatHitResult.Dodged)
        {
            ShowMessage(
                "Esquive reussie",
                new Color(0.92f, 0.78f, 0.3f),
                1.1f
            );
        }
    }

    private void HandleFighterDeath(FighterStats deadFighter)
    {
        if (battleEnded)
            return;

        battleEnded = true;
        bool playerWon = deadFighter == enemyStats;

        enemyAI?.StopAI();
        gestureGrid?.SetInputEnabled(false);
        playerCombat.SetCombatEnabled(false);
        enemyCombat.SetCombatEnabled(false);

        SetEnemyStatus(playerWon ? "Vaincu" : string.Empty);
        ShowMessage(
            playerWon ? "Victoire" : "Defaite",
            playerWon
                ? new Color(0.93f, 0.77f, 0.3f)
                : new Color(0.9f, 0.27f, 0.25f),
            0f,
            true
        );
    }

    private IEnumerator FadeMessageRoutine(float duration)
    {
        float visibleDuration = Mathf.Max(0.15f, duration);
        float elapsed = 0f;

        while (elapsed < visibleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        const float fadeDuration = 0.28f;
        elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            feedbackGroup.alpha =
                1f - Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        feedbackGroup.alpha = 0f;
        feedbackText.text = string.Empty;
        feedbackRoutine = null;
    }

    private static RectTransform CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject panelObject = new(name);
        panelObject.transform.SetParent(parent, false);

        RectTransform rect =
            panelObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = panelObject.AddComponent<Image>();
        image.color = PanelColor;
        Sprite background =
            Resources.GetBuiltinResource<Sprite>(
                "UI/Skin/Background.psd"
            );
        if (background != null)
        {
            image.sprite = background;
            image.type = Image.Type.Sliced;
        }
        image.raycastTarget = false;

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        return rect;
    }

    private static StatBar CreateStatBar(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        Color normalColor,
        Color lowColor,
        int fontSize)
    {
        GameObject barObject = new(name);
        barObject.transform.SetParent(parent, false);

        RectTransform rect =
            barObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image background = barObject.AddComponent<Image>();
        background.color =
            new Color(0.01f, 0.015f, 0.025f, 0.88f);
        background.raycastTarget = false;

        Outline outline = barObject.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject fillObject = new("Fill");
        fillObject.transform.SetParent(barObject.transform, false);

        RectTransform fillRect =
            fillObject.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        Image fill = fillObject.AddComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 1f;
        fill.color = normalColor;
        fill.raycastTarget = false;

        Text value = CreateText(
            barObject.transform,
            "Value",
            string.Empty,
            fontSize,
            FontStyle.Normal,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            size
        );
        value.color = TextColor;

        return new StatBar(
            fill,
            value,
            normalColor,
            lowColor
        );
    }

    private static Text CreateText(
        Transform parent,
        string name,
        string content,
        int fontSize,
        FontStyle style,
        TextAnchor alignment,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject textObject = new(name);
        textObject.transform.SetParent(parent, false);

        RectTransform rect =
            textObject.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax =
            new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    private static Font GetFont()
    {
        return Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf"
        );
    }

    private sealed class StatBar
    {
        private readonly Image fill;
        private readonly Text value;
        private readonly Color normalColor;
        private readonly Color lowColor;

        public StatBar(
            Image fillImage,
            Text valueText,
            Color normal,
            Color low)
        {
            fill = fillImage;
            value = valueText;
            normalColor = normal;
            lowColor = low;
        }

        public void SetValue(float current, float maximum)
        {
            float safeMaximum = Mathf.Max(1f, maximum);
            float normalized =
                Mathf.Clamp01(current / safeMaximum);

            fill.fillAmount = normalized;
            fill.color =
                normalized <= 0.25f
                    ? lowColor
                    : normalColor;
            value.text =
                $"{Mathf.CeilToInt(current)} / " +
                $"{Mathf.CeilToInt(maximum)}";
        }
    }
}
