using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatDistanceDebugVisualizer : MonoBehaviour
{
    [Header("Cercles")]
    [SerializeField] private bool visible = true;
    [Range(24, 128)]
    [SerializeField] private int circleSegments = 72;
    [Min(0.005f)]
    [SerializeField] private float lineWidth = 0.035f;
    [SerializeField] private float groundLocalY = -0.98f;
    [SerializeField] private Color closeColor =
        new(0.25f, 0.9f, 0.38f, 0.42f);
    [SerializeField] private Color midColor =
        new(0.25f, 0.65f, 1f, 0.38f);
    [SerializeField] private Color longColor =
        new(1f, 0.72f, 0.18f, 0.32f);

    [Header("Direction prototype")]
    [Min(0.05f)]
    [SerializeField] private float facingMarkerLength = 0.65f;
    [Min(0.005f)]
    [SerializeField] private float facingMarkerWidth = 0.06f;

    private CombatSpatialController spatialController;
    private FighterCombat player;
    private FighterCombat opponent;
    private readonly LineRenderer[] circles =
        new LineRenderer[3];
    private LineRenderer playerFacing;
    private LineRenderer opponentFacing;
    private Material lineMaterial;
    private bool initialized;

    public bool IsVisible => visible;

    public void Initialize(
        CombatSpatialController spatialAuthority,
        FighterCombat playerFighter,
        FighterCombat opponentFighter)
    {
        spatialController = spatialAuthority;
        player = playerFighter;
        opponent = opponentFighter;
        if (spatialController == null ||
            player == null ||
            opponent == null)
        {
            enabled = false;
            return;
        }

        lineMaterial = new Material(
            Shader.Find("Sprites/Default")
        )
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        circles[0] = CreateCircle(
            "Close Range Debug Circle",
            DistanceLevel.CloseRange,
            closeColor
        );
        circles[1] = CreateCircle(
            "Mid Range Debug Circle",
            DistanceLevel.MidRange,
            midColor
        );
        circles[2] = CreateCircle(
            "Long Range Debug Circle",
            DistanceLevel.LongRange,
            longColor
        );
        playerFacing = CreateFacingMarker(
            player.transform,
            "Player Facing Debug",
            new Color(0.25f, 0.9f, 1f, 0.9f)
        );
        opponentFacing = CreateFacingMarker(
            opponent.transform,
            "Opponent Facing Debug",
            new Color(1f, 0.35f, 0.25f, 0.9f)
        );

        spatialController.OnSnapshotChanged +=
            HandleSnapshotChanged;
        initialized = true;
        ApplyVisibility();
        RefreshHighlight(spatialController.Snapshot);
    }

    private void OnDestroy()
    {
        if (spatialController != null)
        {
            spatialController.OnSnapshotChanged -=
                HandleSnapshotChanged;
        }

        if (lineMaterial != null)
            Destroy(lineMaterial);
    }

    public void SetVisible(bool show)
    {
        visible = show;
        ApplyVisibility();
    }

    public void ToggleVisible()
    {
        SetVisible(!visible);
    }

    public void ResetForReplay()
    {
        if (!initialized)
            return;

        ApplyVisibility();
        RefreshHighlight(spatialController.Snapshot);
    }

    private LineRenderer CreateCircle(
        string objectName,
        DistanceLevel level,
        Color color)
    {
        GameObject circleObject = new(objectName);
        circleObject.transform.SetParent(
            opponent.transform,
            false
        );
        circleObject.transform.localPosition =
            new Vector3(0f, groundLocalY, 0f);
        circleObject.transform.localRotation =
            Quaternion.identity;

        LineRenderer line =
            circleObject.AddComponent<LineRenderer>();
        ConfigureLine(line, color, lineWidth);
        line.loop = true;
        line.positionCount = Mathf.Max(24, circleSegments);
        float radius = spatialController.GetDistance(level);
        for (int index = 0;
             index < line.positionCount;
             index++)
        {
            float angle =
                index / (float)line.positionCount *
                Mathf.PI * 2f;
            line.SetPosition(
                index,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                )
            );
        }

        return line;
    }

    private LineRenderer CreateFacingMarker(
        Transform fighter,
        string objectName,
        Color color)
    {
        GameObject markerObject = new(objectName);
        markerObject.transform.SetParent(fighter, false);
        markerObject.transform.localPosition =
            new Vector3(0f, groundLocalY + 0.03f, 0f);
        markerObject.transform.localRotation =
            Quaternion.identity;

        LineRenderer line =
            markerObject.AddComponent<LineRenderer>();
        ConfigureLine(line, color, facingMarkerWidth);
        line.positionCount = 2;
        line.SetPosition(0, Vector3.zero);
        line.SetPosition(
            1,
            Vector3.forward * facingMarkerLength
        );
        return line;
    }

    private void ConfigureLine(
        LineRenderer line,
        Color color,
        float width)
    {
        line.useWorldSpace = false;
        line.material = lineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.generateLightingData = false;
    }

    private void HandleSnapshotChanged(
        CombatSpatialSnapshot snapshot)
    {
        RefreshHighlight(snapshot);
    }

    private void RefreshHighlight(
        CombatSpatialSnapshot snapshot)
    {
        for (int index = 0; index < circles.Length; index++)
        {
            LineRenderer circle = circles[index];
            if (circle == null)
                continue;

            bool current = index == (int)snapshot.Distance;
            float width = current
                ? lineWidth * 2.1f
                : lineWidth;
            circle.startWidth = width;
            circle.endWidth = width;
        }
    }

    private void ApplyVisibility()
    {
        for (int index = 0; index < circles.Length; index++)
        {
            if (circles[index] != null)
                circles[index].gameObject.SetActive(visible);
        }

        if (playerFacing != null)
            playerFacing.gameObject.SetActive(visible);
        if (opponentFacing != null)
            opponentFacing.gameObject.SetActive(visible);
    }
}
