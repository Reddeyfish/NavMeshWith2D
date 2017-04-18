# NavMeshWith2D
Scripts to create NavMeshes using 2D polygoncolliders as input

GIF example:
[![https://gyazo.com/6ef90d580a742e94b2e6f7aaacc39495](https://i.gyazo.com/6ef90d580a742e94b2e6f7aaacc39495.gif)](https://gyazo.com/6ef90d580a742e94b2e6f7aaacc39495)

(https://gyazo.com/6ef90d580a742e94b2e6f7aaacc39495)

The project takes the data from the polygon colliders, converts them into 3D meshes, and feeds them into the low-level navmesh builder (at runtime).

To use, place the TwoDNavmeshBuilder somewhere in the scene, and place PolygonNavmeshObstacle scripts on all polygon colliders you want to include in the navmesh.
