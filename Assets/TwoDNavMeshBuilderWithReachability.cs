using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Assertions;

/// <summary>
/// Place this script somewhere in the scene to generate the navmesh. If an origin is not specified, it'll use its transform's position as the origin.
/// </summary>
[DefaultExecutionOrder(-102)] //execute before navMeshAgents
public class TwoDNavMeshBuilderWithReachability : MonoBehaviour {
    static TwoDNavMeshBuilderWithReachability main = null;
    public static TwoDNavMeshBuilderWithReachability Main { get { return main; } }

    // The center of the build
    public Transform meshOrigin;

    // The size of the build bounds
    public Vector2 buildSize = new Vector2(100.0f, 100.0f);

    public Vector3 vectorPrecision = new Vector3(0.1f, 0.1f, 0.1f);

    public Vector3[] reachabilityOrigins;
    public bool includeUnreachableAreas; //if true, unreachable navTriangles are included as Area 1
    public int unreachableAreaIndex = 1; //index of the area in the list of areas which we will label unreachable triangles with

    NavMeshData m_NavMesh;
    NavMeshDataInstance m_Instance;
    //List<NavMeshBuildSource> m_Sources = new List<NavMeshBuildSource>();

    void OnEnable() {
        // Construct and add navmesh

        if (meshOrigin == null)
            meshOrigin = transform;

        float yMax = Mathf.Max(Mathf.Abs(meshOrigin.position.z), Mathf.Abs(meshOrigin.position.y));
        Vector3 boundsSize = new Vector3(buildSize.x + Mathf.Abs(meshOrigin.position.x), buildSize.y + yMax, buildSize.y + yMax);
        Bounds bounds = new Bounds(new Vector3(meshOrigin.position.x, meshOrigin.position.z, meshOrigin.position.y), boundsSize); // navmesh space bounds 

        NavMeshBuildSettings buildSettings = NavMesh.GetSettingsByID(0);
        float defaultAgentRadius = buildSettings.agentRadius;
        buildSettings.agentRadius = 0.01f;

        List<NavMeshBuildSource> navSources = PolygonNavmeshObstacle.Collect();
        navSources.Add(generateWalkablePlane());

        //rotation from XY plane to XZ plane
        for (int i = 0; i < navSources.Count; i++) {
            Matrix4x4 transformation = navSources[i].transform;

            Vector4 verticalDirection = transformation.GetRow(1);
            transformation.SetRow(1, transformation.GetRow(2));
            transformation.SetRow(2, verticalDirection);

            //these structs are pass by value, apperently?
            NavMeshBuildSource buildSource = navSources[i];
            buildSource.transform = transformation;
            navSources[i] = buildSource;
        }

        //triangulation breaks and returns vectors with only an x component if we create the navmesh in the XY plane, so we need to do it in the XZ plane then tranform the result.
        m_NavMesh = NavMeshBuilder.BuildNavMeshData(buildSettings, navSources, bounds, Vector3.zero, Quaternion.identity);//Quaternion.LookRotation(-Vector3.up, Vector3.forward));
        m_Instance = NavMesh.AddNavMeshData(m_NavMesh);

        //calculate reachability

        navSources.Clear();
        buildSettings.agentRadius = defaultAgentRadius - buildSettings.agentRadius;
        bounds = new Bounds(meshOrigin.position, boundsSize);

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();

        //do de-duping on vertices
        Dictionary<Vector3, int> positionToNewIndex = new Dictionary<Vector3, int>();
        for (int i = 0; i < triangulation.vertices.Length; i++) {
            Vector3 vertexPosition = Quantize(triangulation.vertices[i], vectorPrecision); //round to one decimal place
            if (!positionToNewIndex.ContainsKey(vertexPosition)) {
                positionToNewIndex[vertexPosition] = positionToNewIndex.Count;
            }
        }

        Vector3[] deDupedVertices = new Vector3[positionToNewIndex.Count];
        foreach (KeyValuePair<Vector3, int> vertexEntry in positionToNewIndex) {
            deDupedVertices[vertexEntry.Value] = vertexEntry.Key;
        }
        int[] triangleIndices = new int[triangulation.indices.Length];
        for (int i = 0; i < triangulation.indices.Length; i++) {
            Vector3 vertexPosition = Quantize(triangulation.vertices[triangulation.indices[i]], vectorPrecision);
            triangleIndices[i] = positionToNewIndex[vertexPosition];
        }

        HashSet<int> reachableVertices;

        navSources.Add(generateReachableArea(deDupedVertices, triangleIndices, out reachableVertices));

        if (includeUnreachableAreas) {
            navSources.Add(generateUnreachableArea(deDupedVertices, triangleIndices, reachableVertices));
        }

        //rotation from XZ plane to XY plane
        for (int i = 0; i < navSources.Count; i++) {
            Matrix4x4 transformation = navSources[i].transform;

            Vector4 verticalDirection = transformation.GetRow(1);
            transformation.SetRow(1, transformation.GetRow(2));
            transformation.SetRow(2, verticalDirection);

            //these structs are pass by value, apperently?
            NavMeshBuildSource buildSource = navSources[i];
            buildSource.transform = transformation;
            navSources[i] = buildSource;
        }

        m_Instance.Remove();
        m_NavMesh = NavMeshBuilder.BuildNavMeshData(buildSettings, navSources, bounds, Vector3.zero, Quaternion.LookRotation(-Vector3.up, Vector3.forward));
        //replace the old navmesh with the new one

        ///*
        m_Instance = NavMesh.AddNavMeshData(m_NavMesh);
        //*/
    }

    NavMeshBuildSource generateSourceFromData(Vector3[] deDupedVertices, int[] triangleIndices) {
        Dictionary<int, int> vertexIndexMapping = new Dictionary<int, int>();
        for (int i = 0; i < deDupedVertices.Length; i++) {
            vertexIndexMapping[i] = i;
        }

        Mesh mesh = generateBuildSourceFromTriangulation(deDupedVertices, triangleIndices, vertexIndexMapping);
        NavMeshBuildSource reachableResult = new NavMeshBuildSource();
        reachableResult.shape = NavMeshBuildSourceShape.Mesh;
        reachableResult.sourceObject = mesh;
        reachableResult.transform = Matrix4x4.identity;
        reachableResult.area = 0;

        return reachableResult;
    }

    private void LogNavMesh() {
        Vector3[] vertices;
        int[] indices;
        NavMesh.Triangulate(out vertices, out indices);

        //do de-duping on vertices
        //Dictionary<Vector3, int> positionToNewIndex = new Dictionary<Vector3, int>();
        for (int i = 0; i < vertices.Length; i++) {
            Vector3 vertexPosition = vertices[i];
            Debug.Log(new Vector3(vertexPosition.x, 100 * vertexPosition.y, 100 * vertexPosition.z));
            //if (!positionToNewIndex.ContainsKey(vertexPosition)) {
            //    positionToNewIndex[vertexPosition] = positionToNewIndex.Count;
            //}
        }
        this.enabled = false;
    }

    NavMeshBuildSource generateReachableArea(Vector3[] deDupedVertices, int[] triangleIndices, out HashSet<int> reachableVertices) {

        Dictionary<int, List<int>> containedTriangles = new Dictionary<int, List<int>>();

        //map original vertex index to the list of triangles that contain that vertex
        for (int i = 0; i < triangleIndices.Length; i++) {
            int vertexIndex = triangleIndices[i];

            if (!containedTriangles.ContainsKey(vertexIndex)) {
                containedTriangles[vertexIndex] = new List<int>() { Mathf.FloorToInt(i / 3) }; //initialize list if it hasn't been added yet
            } else {
                containedTriangles[vertexIndex].Add(Mathf.FloorToInt(i / 3));
            }
        }

        //calculate closest vertices to the reachability origins
        if (reachabilityOrigins.Length == 0) {
            reachabilityOrigins = new Vector3[] { meshOrigin.position };
        }

        Queue<int> frontierQueue = new Queue<int>(); //queue of vertices to add

        for (int i = 0; i < reachabilityOrigins.Length; i++) {
            Vector3 xzOrigin = new Vector3(reachabilityOrigins[i].x, reachabilityOrigins[i].z, reachabilityOrigins[i].y); //move origin from XY plane to XZ plane

            int originVertexIndex = 0; //find the closest vertex to use as the origin
            float originDistance = Vector3.SqrMagnitude(xzOrigin - deDupedVertices[0]);
            for (int j = 1; j < deDupedVertices.Length; j++) {
                float distance = Vector3.SqrMagnitude(xzOrigin - deDupedVertices[j]);
                if (distance < originDistance) {
                    originDistance = distance;
                    originVertexIndex = j;
                    if (originDistance < vectorPrecision.sqrMagnitude) {
                        break; //we can't be any more precise
                    }
                }
            }

            //enqueue origin vertex
            frontierQueue.Enqueue(originVertexIndex);
        }

        Dictionary<int, int> vertexIndexMapping = new Dictionary<int, int>(); //maps vertex indices in the original triangulation to vertex indices in the new input mesh
                                                                              //if a vertex is reachable, it'll be in this dict.

        while (frontierQueue.Count > 0) {
            int nextVertex = frontierQueue.Dequeue();
            if (vertexIndexMapping.ContainsKey(nextVertex)) { continue; } //if we already processed this vertex, don't re-process

            vertexIndexMapping[nextVertex] = vertexIndexMapping.Count; //add vertex to list of already explored vertices

            for (int i = 0; i < containedTriangles[nextVertex].Count; i++) { //add all connected vertices, by iterating through all triangles it is contained in
                int triangleIndex = containedTriangles[nextVertex][i];

                //add all 3 vertices of the triangle
                for (int j = 0; j < 3; j++) {
                    int potentialIndex = triangleIndices[(3 * triangleIndex) + j];
                    if (!vertexIndexMapping.ContainsKey(potentialIndex)) {
                        frontierQueue.Enqueue(potentialIndex);
                    }
                }
            }
        }
        reachableVertices = new HashSet<int>(vertexIndexMapping.Keys);
        Assert.IsTrue(reachableVertices.Count > 0);

        Mesh mesh = generateBuildSourceFromTriangulation(deDupedVertices, triangleIndices, vertexIndexMapping);

        NavMeshBuildSource reachableResult = new NavMeshBuildSource();
        reachableResult.shape = NavMeshBuildSourceShape.Mesh;
        reachableResult.sourceObject = mesh;
        reachableResult.transform = Matrix4x4.identity;
        reachableResult.area = 0;

        /*
        transform.AddComponent<MeshFilter>().mesh = mesh;
        transform.AddComponent<MeshRenderer>();
        */

        return reachableResult;
    }

    NavMeshBuildSource generateUnreachableArea(Vector3[] deDupedVertices, int[] triangleIndices, HashSet<int> reachableVertices) {
        Dictionary<int, int> vertexIndexMapping = new Dictionary<int, int>();
        for (int i = 0; i < deDupedVertices.Length; i++) {
            if (!reachableVertices.Contains(i)) {
                vertexIndexMapping[i] = vertexIndexMapping.Count;
            }
        }

        Mesh mesh = generateBuildSourceFromTriangulation(deDupedVertices, triangleIndices, vertexIndexMapping);

        Debug.Log(mesh.triangles.Length);

        NavMeshBuildSource reachableResult = new NavMeshBuildSource();
        reachableResult.shape = NavMeshBuildSourceShape.Mesh;
        reachableResult.sourceObject = mesh;
        reachableResult.transform = Matrix4x4.identity;
        reachableResult.area = unreachableAreaIndex;

        return reachableResult;
    }

    Mesh generateBuildSourceFromTriangulation(Vector3[] deDupedVertices, int[] triangleIndices, Dictionary<int, int> vertexIndexMapping) {

        //Create a mesh out of the reachable area
        Vector3[] vertices = new Vector3[vertexIndexMapping.Count];
        foreach (KeyValuePair<int, int> vertexMapping in vertexIndexMapping) {
            vertices[vertexMapping.Value] = deDupedVertices[vertexMapping.Key];
        }

        List<int> triangles = new List<int>();
        for (int i = 0; i < triangleIndices.Length; i += 3) {
            if (vertexIndexMapping.ContainsKey(triangleIndices[i])) {
                //if the triangle contains a reached index, the triangle is reachable
                triangles.Add(vertexIndexMapping[triangleIndices[i + 0]]);
                triangles.Add(vertexIndexMapping[triangleIndices[i + 1]]);
                triangles.Add(vertexIndexMapping[triangleIndices[i + 2]]);
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
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
        float x = quant.x * Mathf.RoundToInt(v.x / quant.x);
        float y = quant.y * Mathf.RoundToInt(v.y / quant.y);
        float z = quant.z * Mathf.RoundToInt(v.z / quant.z);
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

    Vector3 RandomPointInBounds() {
        float x = Random.Range(-buildSize.x, buildSize.x);
        float y = Random.Range(-buildSize.y, buildSize.y);
        return new Vector3(x, y, 0);
    }

    public Vector3 RandomPointOnNavmesh(int numRegenerations = 0, int areaMask = NavMesh.AllAreas) {
        Vector3 destinationPoint = main.RandomPointInBounds();
        NavMeshHit myNavHit;
        while (!NavMesh.SamplePosition(destinationPoint, out myNavHit, 100 + numRegenerations * 10, areaMask)) {
            destinationPoint = main.RandomPointInBounds(); //regenerate point
            numRegenerations++; //lower our restrictions
        }
        return myNavHit.position;
    }

    public NavMeshPath RandomPath(Vector3 startingPoint, out Vector3 destinationPoint, int areaMask = NavMesh.AllAreas) {
        NavMeshPath path = new NavMeshPath();
        int numRegenerations = 0; //we'll lower our restrictions as we have more regenerations
        destinationPoint = main.RandomPointOnNavmesh(numRegenerations);
        while (!NavMesh.CalculatePath(startingPoint, destinationPoint, areaMask, path)) {
            //if path is invalid, pick a new destination and regenerate
            numRegenerations++;
            destinationPoint = main.RandomPointOnNavmesh(numRegenerations);
        }
        return path;
    }
}
