using UnityEngine;
using UnityEngine.UI;

public sealed class CircleApproachRing : Image
{
    [SerializeField, Min(1f)] float lineWidth = 5f;

    public bool StarShape { get; set; }

    public float LineWidth
    {
        get => lineWidth;
        set { lineWidth = Mathf.Max(1f, value); SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = GetPixelAdjustedRect();
        float radiusX = bounds.width * 0.5f;
        float radiusY = bounds.height * 0.5f;
        if (radiusX <= 0f || radiusY <= 0f) return;
        if (StarShape)
        {
            mesh.AddVert(Vector3.zero, color, Vector2.one * .5f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                float r = i % 2 == 0 ? 1f : .20f;
                mesh.AddVert(new Vector3(Mathf.Cos(a) * radiusX * r,
                    Mathf.Sin(a) * radiusY * r, 0f), color, Vector2.zero);
            }
            for (int i = 0; i < 8; i++) mesh.AddTriangle(0, i + 1, (i + 1) % 8 + 1);
            return;
        }
        float halfWidth = Mathf.Min(lineWidth * 0.5f, Mathf.Min(radiusX, radiusY));

        const int segments = 96;
        for (int i = 0; i <= segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
            mesh.AddVert(new Vector3(bounds.center.x + x * (radiusX - halfWidth),
                bounds.center.y + y * (radiusY - halfWidth), 0f), color, Vector2.zero);
            mesh.AddVert(new Vector3(bounds.center.x + x * (radiusX + halfWidth),
                bounds.center.y + y * (radiusY + halfWidth), 0f), color, Vector2.zero);
            if (i == segments) continue;
            int vertex = i * 2;
            mesh.AddTriangle(vertex, vertex + 3, vertex + 1);
            mesh.AddTriangle(vertex, vertex + 2, vertex + 3);
        }
    }
}
