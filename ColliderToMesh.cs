using LibSM64;
using SM64Mod;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ColliderToMesh : MonoBehaviour
{
    [SerializeField]
    public PolygonCollider2D[] polygonCollider2Ds;
    //public bool addRenderer = true;
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
        polygonCollider2Ds = FindObjectsOfType<PolygonCollider2D>()
            .Where(x => x.gameObject.scene == gameObject.scene && !x.isTrigger &&
                        !IsChildOfPlayer(x.transform))
            .ToArray();
    }
    private bool IsChildOfPlayer(Transform t)
    {
        while (t != null)
        {
            if (t.name == "Player")
                return true;
            t = t.parent;
        }
        return false;
    }
    public void CreateMeshes()
    {
        GameObject compositeHolder = new GameObject("TempCompositeHolder");
        compositeHolder.SetActive(false);
        Rigidbody2D rb = compositeHolder.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        CompositeCollider2D compositeCollider = compositeHolder.AddComponent<CompositeCollider2D>();
        compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;

        foreach (PolygonCollider2D originalPoly in polygonCollider2Ds)
        {
            GameObject child = new GameObject("TempPoly");
            child.transform.SetParent(compositeHolder.transform, false);
            PolygonCollider2D childPoly = child.AddComponent<PolygonCollider2D>();
            childPoly.usedByComposite = true;
            childPoly.points = originalPoly.points;
            child.transform.position = originalPoly.transform.position;
            child.transform.rotation = originalPoly.transform.rotation;
            child.transform.localScale = originalPoly.transform.lossyScale;
        }

        compositeHolder.SetActive(true);
        compositeCollider.GenerateGeometry();

        List<Vector2[]> allPaths = new List<Vector2[]>();
        for (int i = 0; i < compositeCollider.pathCount; i++)
        {
            List<Vector2> path = new List<Vector2>();
            compositeCollider.GetPath(i, path);
            allPaths.Add(path.ToArray());
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
        meshObject.transform.SetParent(transform, false);
        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        meshFilter.mesh = finalMesh;

        MeshCollider meshCollider = meshObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = finalMesh;

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