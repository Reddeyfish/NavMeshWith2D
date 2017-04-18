using UnityEngine;
using UnityEngine.AI;
//Taken from Unity's 5.6 navmesh examples: https://github.com/Unity-Technologies/NavMeshComponents
// Walk to a random position and repeat
[RequireComponent(typeof(NavMeshAgent))]
public class RandomWalk : MonoBehaviour
{
    public float m_Range = 25.0f;
    NavMeshAgent m_agent;

    void Start()
    {
        m_agent = GetComponent<NavMeshAgent>();
        NavMeshHit myNavHit;
        if (NavMesh.SamplePosition(transform.position, out myNavHit, 100, -1)) {
            transform.position = myNavHit.position;
        }
    }

    void Update()
    {
        if (m_agent.pathPending || m_agent.remainingDistance > 0.1f)
            return;

        Vector3 destination = m_Range * Random.insideUnitCircle;

        NavMeshHit myNavHit;
        if (NavMesh.SamplePosition(destination, out myNavHit, 100, -1)) {
            destination = myNavHit.position;
        }

        m_agent.destination = destination;
    }
}
