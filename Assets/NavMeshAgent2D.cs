using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody2D))]
public class NavMeshAgent2D : MonoBehaviour {

    public float acceleration = 8.0f;
    public float speed = 3.5f;
    public float angularSpeed = 120f;

    NavMeshAgent agent;
    Rigidbody2D rigid;

	// Use this for initialization
	void Awake () {
        agent = GetComponent<NavMeshAgent>();
        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        rigid = GetComponent<Rigidbody2D>();
    }
	
    float ToAngle(Vector2 vector) {
        return Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
    }

	// Update is called once per frame
	void Update () {
        Vector3 target = agent.steeringTarget;
        Vector2 targetDirection = target - transform.position;
        targetDirection.Normalize();

        //custom movement logic replaces this
        rigid.velocity = Vector2.MoveTowards(rigid.velocity, targetDirection * speed, Time.deltaTime * acceleration);
        rigid.MoveRotation(Mathf.MoveTowardsAngle(rigid.rotation, ToAngle(targetDirection), Time.deltaTime * angularSpeed));

        agent.nextPosition = transform.position;
    }
}
