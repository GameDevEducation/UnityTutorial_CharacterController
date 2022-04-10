using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AICharacterMotor))]
public class Navigation_UnityNavMesh : BaseNavigation
{
    [SerializeField] AnimationCurve AngleDeltaToTurnInput;
    [SerializeField] float SlowDownDistance = 2f;
    [SerializeField] AnimationCurve DistanceToSpeedInput;

    [SerializeField] AnimationCurve SpeedScaleWithAngleDelta;

    NavMeshAgent LinkedAgent;
    AICharacterMotor AIMotor;

    Vector3[] CurrentPath;
    int TargetPoint = -1;

    protected override void Initialise()
    {
        LinkedAgent = GetComponent<NavMeshAgent>();
        AIMotor = GetComponent<AICharacterMotor>();

        LinkedAgent.updatePosition = false;
        LinkedAgent.updateRotation = false;
    }

    protected override bool RequestPath()
    {
        LinkedAgent.speed = MaxMoveSpeed;
        LinkedAgent.angularSpeed = RotationSpeed;
        LinkedAgent.stoppingDistance = DestinationReachedThreshold;

        LinkedAgent.SetDestination(Destination);
        
        OnBeganPathFinding();

        return true;
    }

    protected override void Tick_Default()
    {

    }

    protected override void Tick_Pathfinding()
    {
        // no pathfinding in progress?
        if (!LinkedAgent.pathPending)
        {           
            if (LinkedAgent.pathStatus == NavMeshPathStatus.PathComplete)
            {
                CurrentPath = LinkedAgent.path.corners;
                TargetPoint = 0;
                OnPathFound();
            }
            else
                OnFailedToFindPath();
        }
    }

    protected override void Tick_PathFollowing()
    {
        Vector3 targetPosition = CurrentPath[TargetPoint];

        // get the 2D vector to the target
        Vector3 vecToTarget = targetPosition - transform.position;
        vecToTarget.y = 0f;

        // reached the target point?
        if (vecToTarget.magnitude <= DestinationReachedThreshold)
        {
            // advance to next point
            ++TargetPoint;

            // reached destination?
            if (TargetPoint == CurrentPath.Length)
            {
                AIMotor.SetMovement(0f);
                AIMotor.SetTurnRate(0f);

                OnReachedDestination();
                return;
            }

            // refresh the target information
            targetPosition = CurrentPath[TargetPoint];
            vecToTarget = targetPosition - transform.position;
            vecToTarget.y = 0f;
        }

        // calculate the rotation to the target
        Quaternion targetRotation = Quaternion.LookRotation(vecToTarget, Vector3.up);
        Quaternion currentRotation = transform.rotation;
        float angleDelta = Quaternion.Angle(currentRotation, targetRotation);

        AIMotor.SetTurnRate(AngleDeltaToTurnInput.Evaluate(angleDelta) * RotationSpeed);

        float speedInput = DistanceToSpeedInput.Evaluate(vecToTarget.magnitude / SlowDownDistance);
        speedInput *= SpeedScaleWithAngleDelta.Evaluate(Mathf.Abs(angleDelta));

        AIMotor.SetMovement(speedInput * MaxMoveSpeed);

        if (DEBUG_ShowHeading)
            Debug.DrawLine(transform.position + Vector3.up, LinkedAgent.steeringTarget, Color.green);
    }

    private void LateUpdate()
    {
        LinkedAgent.nextPosition = transform.position;
    }

    public override void StopMovement()
    {
        LinkedAgent.ResetPath();

        CurrentPath = null;
        TargetPoint = -1;

        AIMotor.SetMovement(0f);
        AIMotor.SetTurnRate(0f);
    }

    public override bool FindNearestPoint(Vector3 searchPos, float range, out Vector3 foundPos)
    {
        NavMeshHit hitResult;
        if (NavMesh.SamplePosition(searchPos, out hitResult, range, NavMesh.AllAreas))
        {
            foundPos = hitResult.position;
            return true;
        }

        foundPos = searchPos;

        return false;
    }

}
