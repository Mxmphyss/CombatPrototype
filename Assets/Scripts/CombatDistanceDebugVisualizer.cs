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
    [Range(0.02f, 0.8f)]
    [SerializeField] private float fillOpacity = 0.2f;
    [Range(0.02f, 0.9f)]
    [SerializeField] private float activeFillOpacity = 0.34f;
    [Min(0f)]
    [SerializeField] private float fillLocalYOffset = 0.008f;
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
    private readonly MeshRenderer[] rangeFills =
        new MeshRenderer[3];
    private readonly Mesh[] rangeMeshes =
        new Mesh[3];
    private readonly Material[] rangeMaterials =
        new Material[3];
    private readonly Color[] rangeColors =
        new Color[3];
    private Transform distanceRoot;
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

        GameObject rootObject =
            new("Combat Distance Debug Root");
        rootObject.transform.SetParent(transform, false);
        distanceRoot = rootObject.transform;
        UpdateDistanceRoot(spatialController.Snapshot);

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
        rangeColors[0] = closeColor;
        rangeColors[1] = midColor;
        rangeColors[2] = longColor;
        float closeRadius =
            spatialController.GetDistance(
                DistanceLevel.CloseRange
            );
        float midRadius =
            spatialController.GetDistance(
                DistanceLevel.MidRange
            );
        float longRadius =
            spatialController.GetDistance(
                DistanceLevel.LongRange
            );
        rangeFills[0] = CreateRangeFill(
            "Close Range Debug Fill",
            0f,
            closeRadius,
            closeColor,
            0
        );
        rangeFills[1] = CreateRangeFill(
            "Mid Range Debug Fill",
            closeRadius,
            midRadius,
            midColor,
            1
        );
        rangeFills[2] = CreateRangeFill(
            "Long Range Debug Fill",
            midRadius,
            longRadius,
            longColor,
            2
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

        for (int index = 0;
             index < rangeMaterials.Length;
             index++)
        {
            if (rangeMaterials[index] != null)
                Destroy(rangeMaterials[index]);
            if (rangeMeshes[index] != null)
                Destroy(rangeMeshes[index]);
        }
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

        UpdateDistanceRoot(spatialController.Snapshot);
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
            distanceRoot,
            false
        );
        circleObject.transform.localPosition = Vector3.zero;
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

    private MeshRenderer CreateRangeFill(
        string objectName,
        float innerRadius,
        float outerRadius,
        Color color,
        int index)
    {
        GameObject fillObject = new(objectName);
        fillObject.transform.SetParent(
            distanceRoot,
            false
        );
        fillObject.transform.localPosition =
            new Vector3(
                0f,
                fillLocalYOffset +
                index * 0.001f,
                0f
            );
        fillObject.transform.localRotation =
            Quaternion.identity;

        Mesh mesh = CreateRangeMesh(
            innerRadius,
            outerRadius
        );
        rangeMeshes[index] = mesh;
        MeshFilter filter =
            fillObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        Material material = new(
            Shader.Find("Sprites/Default")
        )
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        Color fillColor = color;
        fillColor.a = fillOpacity;
        material.color = fillColor;
        rangeMaterials[index] = material;

        MeshRenderer renderer =
            fillObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = -20 + index;
        return renderer;
    }

    private Mesh CreateRangeMesh(
        float innerRadius,
        float outerRadius)
    {
        int segments = Mathf.Max(24, circleSegments);
        bool isDisc = innerRadius <= 0.001f;
        Mesh mesh = new()
        {
            name = isDisc
                ? "Combat Distance Disc"
                : "Combat Distance Ring",
            hideFlags = HideFlags.HideAndDontSave
        };

        if (isDisc)
        {
            Vector3[] vertices =
                new Vector3[segments + 2];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int step = 0; step <= segments; step++)
            {
                float angle =
                    step / (float)segments *
                    Mathf.PI * 2f;
                vertices[step + 1] =
                    PointOnCircle(outerRadius, angle);
            }

            for (int step = 0; step < segments; step++)
            {
                int triangle = step * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = step + 2;
                triangles[triangle + 2] = step + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
        }
        else
        {
            Vector3[] vertices =
                new Vector3[(segments + 1) * 2];
            int[] triangles = new int[segments * 6];
            for (int step = 0; step <= segments; step++)
            {
                float angle =
                    step / (float)segments *
                    Mathf.PI * 2f;
                int vertex = step * 2;
                vertices[vertex] =
                    PointOnCircle(innerRadius, angle);
                vertices[vertex + 1] =
                    PointOnCircle(outerRadius, angle);
            }

            for (int step = 0; step < segments; step++)
            {
                int vertex = step * 2;
                int next = vertex + 2;
                int triangle = step * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = next + 1;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = next;
                triangles[triangle + 5] = next + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3 PointOnCircle(
        float radius,
        float angle)
    {
        return new Vector3(
            Mathf.Cos(angle) * radius,
            0f,
            Mathf.Sin(angle) * radius
        );
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
        line.sortingOrder = 10;
    }

    private void HandleSnapshotChanged(
        CombatSpatialSnapshot snapshot)
    {
        UpdateDistanceRoot(snapshot);
        RefreshHighlight(snapshot);
    }

    private void UpdateDistanceRoot(
        CombatSpatialSnapshot snapshot)
    {
        if (distanceRoot == null)
            return;

        Pose opponentPose =
            snapshot.FirstFighter == opponent
                ? snapshot.FirstNeutralPose
                : snapshot.SecondNeutralPose;
        Vector3 groundPosition = opponentPose.position;
        groundPosition.y += groundLocalY;
        distanceRoot.SetPositionAndRotation(
            groundPosition,
            Quaternion.identity
        );
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

            Material fillMaterial = rangeMaterials[index];
            if (fillMaterial == null)
                continue;

            Color fillColor = rangeColors[index];
            fillColor.a = current
                ? activeFillOpacity
                : fillOpacity;
            fillMaterial.color = fillColor;
        }
    }

    private void ApplyVisibility()
    {
        for (int index = 0; index < circles.Length; index++)
        {
            if (circles[index] != null)
                circles[index].gameObject.SetActive(visible);
            if (rangeFills[index] != null)
                rangeFills[index].gameObject.SetActive(visible);
        }

        if (playerFacing != null)
            playerFacing.gameObject.SetActive(visible);
        if (opponentFacing != null)
            opponentFacing.gameObject.SetActive(visible);
    }
}
