using LibSM64;
using SM64Mod;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ColliderToMesh : MonoBehaviour
{
    [SerializeField]
    public Collider2D[] colliders;
    public bool addRenderer = false;
    public Material material;
    public float updateInterval = 0.1f;

    private float timer = 0f;

    void Awake()
    {
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
        }
        SelectAllColliders();
        CreateMeshes();

        SM64Plugin.Instance.RefreshStaticTerrain();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= updateInterval)
        {
            timer = 0f;
            try
            {
                Destroy(GameObject.Find("SM64_DEATH_SURFACE"));
                Vector3 P = SM64Plugin.Instance.Player.transform.position;
                P.y -= 20f;
                GameObject surfaceObj = new GameObject("SM64_DEATH_SURFACE");
                MeshCollider surfaceMesh = surfaceObj.AddComponent<MeshCollider>();
                surfaceObj.AddComponent<SM64StaticTerrain>();

                MeshRenderer meshRenderer = surfaceObj.AddComponent<MeshRenderer>();
                Material whiteMaterial = new Material(Shader.Find("Standard"));
                whiteMaterial.color = Color.white;
                meshRenderer.material = whiteMaterial;
                Mesh mesh = new Mesh();
                mesh.name = "TEST_MESH";
                mesh.SetVertices(
                new Vector3[]
                    {
                            new Vector3(P.x-128,P.y,P.z-128), new Vector3(P.x+128,P.y,P.z+128), new Vector3(P.x+128,P.y,P.z-128),
                            new Vector3(P.x+128,P.y,P.z+128), new Vector3(P.x-128,P.y,P.z-128), new Vector3(P.x-128,P.y,P.z+128),
                    }
                );
                mesh.SetTriangles(new int[] { 0, 1, 2, 3, 4, 5 }, 0);
                surfaceMesh.sharedMesh = mesh;
            }
            catch { }
            SM64Plugin.Instance.RefreshStaticTerrain();
        }
    }

    public void SelectAllColliders()
    {
        colliders = FindObjectsOfType<Collider2D>()
            .Where(x => (x is PolygonCollider2D || x is BoxCollider2D || x is CircleCollider2D) &&
                        x.gameObject.scene == gameObject.scene &&
                        !x.isTrigger &&
                        IsAllowed(x.transform))
            .ToArray();
    }
    private bool IsAllowed(Transform t)
    {
        while (t != null)
        {
            if (t.name == "Player" || t.name == "Orange" || t.name == "Coffee+Cup+Takeaway" || t.name == "SnowHat")
                return false;
            t = t.parent;
        }
        return true;
    }
    public void CreateMeshes()
    {
        GameObject compositeHolder = new GameObject("TempCompositeHolder");
        compositeHolder.SetActive(false);
        CompositeCollider2D compositeCollider = compositeHolder.AddComponent<CompositeCollider2D>();
        compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;

        foreach (Collider2D originalCollider in colliders)
        {
            GameObject child = new GameObject("TempPoly");
            child.transform.SetParent(compositeHolder.transform, false);
            PolygonCollider2D childPoly = child.AddComponent<PolygonCollider2D>();
            childPoly.usedByComposite = true;
            Vector3 lossyScale = originalCollider.transform.lossyScale;

            if (originalCollider is PolygonCollider2D originalPoly)
            {
                Vector2[] scaledPoints = new Vector2[originalPoly.points.Length];
                for (int i = 0; i < originalPoly.points.Length; i++)
                {
                    scaledPoints[i] = new Vector2(
                        originalPoly.points[i].x * lossyScale.x,
                        originalPoly.points[i].y * lossyScale.y
                    );
                }
                childPoly.points = scaledPoints;
                childPoly.offset = originalPoly.offset;
                child.transform.position = originalPoly.transform.position;
                child.transform.rotation = originalPoly.transform.rotation;
                child.transform.localScale = originalPoly.transform.lossyScale;
            }
            else if (originalCollider is BoxCollider2D box)
            {
                Vector2[] points = new Vector2[4];
                float halfWidth = (box.size.x * lossyScale.x) / 2f;
                float halfHeight = (box.size.y * lossyScale.y) / 2f;
                Vector2 scaledOffset = new Vector2(
                    box.offset.x * lossyScale.x,
                    box.offset.y * lossyScale.y
                );
                points[0] = scaledOffset + new Vector2(-halfWidth, -halfHeight);
                points[1] = scaledOffset + new Vector2(halfWidth, -halfHeight);
                points[2] = scaledOffset + new Vector2(halfWidth, halfHeight);
                points[3] = scaledOffset + new Vector2(-halfWidth, halfHeight);
                childPoly.points = points;
            }
            else if (originalCollider is CircleCollider2D circle)
            {
                int numSegments = 32;
                Vector2[] points = new Vector2[numSegments];
                float radiusX = circle.radius * lossyScale.x;
                float radiusY = circle.radius * lossyScale.y;
                Vector2 scaledOffset = new Vector2(
                    circle.offset.x * lossyScale.x,
                    circle.offset.y * lossyScale.y
                );
                for (int i = 0; i < numSegments; i++)
                {
                    float angle = (float)i / numSegments * Mathf.PI * 2f;
                    points[i] = scaledOffset + new Vector2(
                        Mathf.Cos(angle) * radiusX,
                        Mathf.Sin(angle) * radiusY
                    );
                }
                childPoly.points = points;
            }

            child.transform.position = originalCollider.transform.position;
            child.transform.rotation = originalCollider.transform.rotation;
            child.transform.localScale = Vector3.one;
        }

        compositeHolder.SetActive(true);
        compositeCollider.GenerateGeometry();

        List<Vector2[]> allPaths = new List<Vector2[]>();
        for (int i = 0; i < compositeCollider.pathCount; i++)
        {
            List<Vector2> path = new List<Vector2>();
            compositeCollider.GetPath(i, path);

            Vector2[] transformedPath = path
                .Select(p => compositeHolder.transform.TransformPoint(new Vector3(p.x, p.y, 0)))
                .Select(v => new Vector2(v.x, v.y))
                .ToArray();

            allPaths.Add(transformedPath);
        }

        CombineInstance[] combine = new CombineInstance[allPaths.Count];
        for (int i = 0; i < allPaths.Count; i++)
        {
            Mesh pathMesh = CreateMesh(allPaths[i]);
            combine[i].mesh = pathMesh;
            combine[i].transform = Matrix4x4.identity;
        }

        Mesh finalMesh = new Mesh();
        finalMesh.CombineMeshes(combine, true, true);
        finalMesh.RecalculateNormals();
        finalMesh.RecalculateBounds();

        GameObject meshObject = new GameObject("CombinedMesh");
        meshObject.transform.position = Vector3.zero;
        meshObject.transform.rotation = Quaternion.identity;
        meshObject.transform.localScale = Vector3.one;
        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        meshFilter.mesh = finalMesh;

        MeshCollider meshCollider = meshObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = finalMesh;
        meshObject.AddComponent<SM64StaticTerrain>();
        meshObject.GetComponent<SM64StaticTerrain>().SurfaceType = SM64SurfaceType.Hangable;

        if (addRenderer)
        {
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.material = material;
        }

        DestroyImmediate(compositeHolder);
    }

    static Mesh CreateMesh(Vector2[] poly)
    {
        Triangulator triangulator = new Triangulator(poly);
        int[] indices = triangulator.Triangulate();
        Mesh m = new Mesh();
        Vector3[] vertices = new Vector3[poly.Length * 2];

        for (int i = 0; i < poly.Length; i++)
        {
            vertices[i].x = poly[i].x;
            vertices[i].y = poly[i].y;
            vertices[i].z = -10;
            vertices[i + poly.Length].x = poly[i].x;
            vertices[i + poly.Length].y = poly[i].y;
            vertices[i + poly.Length].z = 10;
        }

        int[] triangles = new int[indices.Length * 2 + poly.Length * 6];
        int count_tris = 0;
        for (int i = 0; i < indices.Length; i += 3)
        {
            triangles[i] = indices[i];
            triangles[i + 1] = indices[i + 1];
            triangles[i + 2] = indices[i + 2];
        }
        count_tris += indices.Length;
        for (int i = 0; i < indices.Length; i += 3)
        {
            triangles[count_tris + i] = indices[i + 2] + poly.Length;
            triangles[count_tris + i + 1] = indices[i + 1] + poly.Length;
            triangles[count_tris + i + 2] = indices[i] + poly.Length;
        }
        count_tris += indices.Length;

        for (int i = 0; i < poly.Length; i++)
        {
            int n = (i + 1) % poly.Length;
            triangles[count_tris] = n;
            triangles[count_tris + 1] = i + poly.Length;
            triangles[count_tris + 2] = i;
            triangles[count_tris + 3] = n;
            triangles[count_tris + 4] = n + poly.Length;
            triangles[count_tris + 5] = i + poly.Length;
            count_tris += 6;
        }

        m.vertices = vertices;
        m.triangles = triangles;
        m.RecalculateNormals();
        m.RecalculateBounds();
        m.Optimize();
        return m;
    }
}