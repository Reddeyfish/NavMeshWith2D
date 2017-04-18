using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Place this script on a polygon collider to have the collider included in the 2D navmesh.
/// </summary>
[DefaultExecutionOrder(-200)] //execute before the navmesh builder
[RequireComponent(typeof(PolygonCollider2D))]
public class PolygonNavmeshObstacle : MonoBehaviour {

    static List<PolygonNavmeshObstacle> allPolygonSources = new List<PolygonNavmeshObstacle>();

    PolygonCollider2D polyCollider;

    Vector3 convertVector(Vector2 twoD, float z = 0) {
        return new Vector3(twoD.x, twoD.y, z);
    }

    // Use this for initialization
    void Awake() {
        polyCollider = GetComponent<PolygonCollider2D>();

        allPolygonSources.Add(this);
    }

    Mesh convertToMesh() {
        Mesh generatedMesh = new Mesh();

        //mesh data
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int pathIndex = 0; pathIndex < polyCollider.pathCount; pathIndex++) {
            Vector2[] path = polyCollider.GetPath(pathIndex);

            int startingVertexIndex = vertices.Count;
            vertices.Add(convertVector(path[0]));
            vertices.Add(convertVector(path[0], 1));
            for (int i = 1; i < path.Length; i++) {
                int vertexIndex = vertices.Count;
                vertices.Add(convertVector(path[i]));
                vertices.Add(convertVector(path[i], 1));

                //lower triangle
                triangles.Add(vertexIndex - 2);
                triangles.Add(vertexIndex + 0);
                triangles.Add(vertexIndex + 1);

                //upper triangle
                triangles.Add(vertexIndex - 2);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex - 1);
            }

            //connect last vertex to form a loop

            int lastVertexIndex = vertices.Count - 2;

            //lower triangle
            triangles.Add(startingVertexIndex);
            triangles.Add(lastVertexIndex + 0);
            triangles.Add(lastVertexIndex + 1);

            //upper triangle
            triangles.Add(startingVertexIndex);
            triangles.Add(lastVertexIndex + 1);
            triangles.Add(startingVertexIndex + 1);
        }

        generatedMesh.SetVertices(vertices);
        generatedMesh.SetTriangles(triangles, 0);
        generatedMesh.RecalculateBounds();
        generatedMesh.RecalculateNormals();

        return generatedMesh;

    }

    public NavMeshBuildSource convertToNavMeshSource() {
        NavMeshBuildSource result = new NavMeshBuildSource();

        Mesh mesh = convertToMesh();
        result.shape = NavMeshBuildSourceShape.Mesh;
        result.sourceObject = mesh;
        Matrix4x4 sourceTransform = transform.localToWorldMatrix;
        sourceTransform.m23 = 0; //zero out the offset in the Z-direction, since that's ignored for most 2D games
        result.transform = sourceTransform;
        result.area = 0;

        return result;
    }

    
    public static List<NavMeshBuildSource> Collect() {

        List<NavMeshBuildSource> result = new List<NavMeshBuildSource>();

        for (int i = 0; i < allPolygonSources.Count; ++i) {
            result.Add(allPolygonSources[i].convertToNavMeshSource());
        }
        return result;
    }
    
}
