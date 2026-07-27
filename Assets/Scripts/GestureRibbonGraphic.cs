using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class GestureRibbonGraphic : MaskableGraphic
{
    private readonly List<Vector2> path = new(96);
    private float ribbonWidth = 64f;
    private int roundSegments = 10;

    public void SetPath(
        IReadOnlyList<Vector2> fixedPath,
        Vector2 liveEndpoint,
        float width,
        Color ribbonColor,
        int capSegments)
    {
        path.Clear();

        if (fixedPath != null)
        {
            for (int index = 0;
                 index < fixedPath.Count;
                 index++)
            {
                path.Add(fixedPath[index]);
            }
        }

        if (path.Count == 0 ||
            (path[^1] - liveEndpoint).sqrMagnitude >
            0.0001f)
        {
            path.Add(liveEndpoint);
        }

        ribbonWidth = Mathf.Max(1f, width);
        roundSegments = Mathf.Clamp(capSegments, 4, 20);
        color = ribbonColor;
        gameObject.SetActive(path.Count > 0);
        SetVerticesDirty();
    }

    public void ClearPath()
    {
        path.Clear();
        SetVerticesDirty();
        gameObject.SetActive(false);
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        if (path.Count == 0)
            return;

        float radius = ribbonWidth * 0.5f;
        Color32 vertexColor = color;

        if (path.Count == 1)
        {
            AddCircle(
                vertexHelper,
                path[0],
                radius,
                vertexColor
            );
            return;
        }

        for (int index = 1; index < path.Count; index++)
        {
            AddSegment(
                vertexHelper,
                path[index - 1],
                path[index],
                radius,
                vertexColor
            );
        }

        for (int index = 0; index < path.Count; index++)
        {
            AddCircle(
                vertexHelper,
                path[index],
                radius,
                vertexColor
            );
        }
    }

    private static void AddSegment(
        VertexHelper vertexHelper,
        Vector2 from,
        Vector2 to,
        float radius,
        Color32 color)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector2 normal = new Vector2(
            -direction.y,
            direction.x
        ).normalized * radius;
        int firstVertex = vertexHelper.currentVertCount;

        AddVertex(vertexHelper, from + normal, color);
        AddVertex(vertexHelper, from - normal, color);
        AddVertex(vertexHelper, to - normal, color);
        AddVertex(vertexHelper, to + normal, color);

        vertexHelper.AddTriangle(
            firstVertex,
            firstVertex + 1,
            firstVertex + 2
        );
        vertexHelper.AddTriangle(
            firstVertex,
            firstVertex + 2,
            firstVertex + 3
        );
    }

    private void AddCircle(
        VertexHelper vertexHelper,
        Vector2 center,
        float radius,
        Color32 color)
    {
        int centerVertex = vertexHelper.currentVertCount;
        AddVertex(vertexHelper, center, color);

        for (int index = 0;
             index <= roundSegments;
             index++)
        {
            float angle =
                index / (float)roundSegments *
                Mathf.PI * 2f;
            Vector2 offset = new(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
            AddVertex(vertexHelper, center + offset, color);
        }

        for (int index = 0; index < roundSegments; index++)
        {
            vertexHelper.AddTriangle(
                centerVertex,
                centerVertex + index + 1,
                centerVertex + index + 2
            );
        }
    }

    private static void AddVertex(
        VertexHelper vertexHelper,
        Vector2 position,
        Color32 color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vertex.uv0 = Vector2.zero;
        vertexHelper.AddVert(vertex);
    }
}
