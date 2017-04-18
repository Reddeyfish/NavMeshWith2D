using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-102)] //execute before navMeshAgents
public class TwoDNavMeshBuilder : MonoBehaviour {
    // The center of the build
    public Transform meshOrigin;

    // The size of the build bounds
    public Vector3 buildSize = new Vector3(100.0f, 100.0f, 100.0f);

    NavMeshData m_NavMesh;
    NavMeshDataInstance m_Instance;
    //List<NavMeshBuildSource> m_Sources = new List<NavMeshBuildSource>();

    void OnEnable() {
        // Construct and add navmesh

        if (meshOrigin == null)
            meshOrigin = transform;

        Bounds bounds = new Bounds(meshOrigin.position, new Vector3(buildSize.x, buildSize.z, buildSize.y)); // navmesh space bounds 

        NavMeshBuildSettings defaultBuildSettings = NavMesh.GetSettingsByID(0);

        List<NavMeshBuildSource> navSources = PolygonNavmeshObstacle.Collect();
        navSources.Add(generateWalkablePlane());

        m_NavMesh = NavMeshBuilder.BuildNavMeshData(defaultBuildSettings, navSources, bounds, Vector3.zero, Quaternion.LookRotation(-Vector3.up, Vector3.forward));
        m_Instance = NavMesh.AddNavMeshData(m_NavMesh);
    }

    NavMeshBuildSource generateWalkablePlane() {
        NavMeshBuildSource result = new NavMeshBuildSource();

        Mesh mesh = new Mesh();

        Vector3 center = meshOrigin.position;

        //a quad representing the walkable area
        //0  1
        //2  3
        Vector3[] vertices = new Vector3[4];
        vertices[0] = center + new Vector3(-buildSize.x, +buildSize.y, 0);
        vertices[1] = center + new Vector3(+buildSize.x, +buildSize.y, 0);
        vertices[2] = center + new Vector3(-buildSize.x, -buildSize.y, 0);
        vertices[3] = center + new Vector3(+buildSize.x, -buildSize.y, 0);

        mesh.vertices = vertices;
        mesh.triangles = new int[] { 0, 3, 1, 0, 2, 3 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        result.shape = NavMeshBuildSourceShape.Mesh;
        result.sourceObject = mesh;
        result.transform = Matrix4x4.identity;
        result.area = 0;

        return result;
    }


    void OnDisable() {
        // Unload navmesh and clear handle
        m_Instance.Remove();
    }

    static Vector3 Quantize(Vector3 v, Vector3 quant) {
        float x = quant.x * Mathf.Floor(v.x / quant.x);
        float y = quant.y * Mathf.Floor(v.y / quant.y);
        float z = quant.z * Mathf.Floor(v.z / quant.z);
        return new Vector3(x, y, z);
    }

    void OnDrawGizmosSelected() {
        if (m_NavMesh) {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(m_NavMesh.sourceBounds.center, m_NavMesh.sourceBounds.size);
        }

        Gizmos.color = Color.yellow;

        Vector3 gizmoPosition = meshOrigin != null ? meshOrigin.position : transform.position;

        Gizmos.DrawWireCube(gizmoPosition, buildSize);

        Gizmos.color = Color.green;
        var center = meshOrigin ? meshOrigin.position : transform.position;
        Gizmos.DrawWireCube(center, buildSize);
    }
}
