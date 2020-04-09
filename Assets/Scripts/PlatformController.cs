using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlatformController : RaycastController
{
    struct PassengerMovement
    {
        public Transform transform;
        public Vector3 velocity;
        public bool standingOnPlatform;
        public bool moveBeforePlatform;

        public PassengerMovement(Transform _transform, Vector3 _velocity, bool _standingOnPlatform, bool _moveBeforePlatform)
        {
            transform = _transform;
            velocity = _velocity;
            standingOnPlatform = _standingOnPlatform;
            moveBeforePlatform = _moveBeforePlatform;
        }
    }

    List<PassengerMovement> passengerMovement;
    Dictionary<Transform, Controller2D> passengerDictionary = new Dictionary<Transform, Controller2D>();
    
    public LayerMask passengerMask;

    public Vector3[] localWaypoints;
    Vector3[] globalWaypoints;

    public float speed;
    public bool cyclic;
    public float waitTime;
    [Range(0, 2)] public float easeAmount;

    int fromWaypointIndex;
    float percentBetweenWaypoints;
    float nextMoveTime;

    void Start()
    {
        StartRaycastController();
        
        globalWaypoints = new Vector3[localWaypoints.Length];
        for(int i = 0; i < localWaypoints.Length; ++i)
        {
            globalWaypoints[i] = localWaypoints[i] + transform.position;
        }
    }

    void MovePassengers(bool moveBeforePlatform)
    {
        foreach(PassengerMovement passenger in passengerMovement)
        {
            if(!passengerDictionary.ContainsKey(passenger.transform))
            {
                passengerDictionary.Add(passenger.transform, passenger.transform.GetComponent<Controller2D>());
            }

            if(passenger.moveBeforePlatform == moveBeforePlatform)
            {
                passengerDictionary[passenger.transform].Move(passenger.velocity, Vector2.zero, passenger.standingOnPlatform);
            }
        }
    }

    float Ease(float x)
    {
        float a = easeAmount + 1;
        float result = Mathf.Pow(x, a) / (Mathf.Pow(x, a) + Mathf.Pow(1 - x, a));
        return(result);
    }

    void Update()
    {
        UpdateRaycastOrigins();

        Vector3 velocity;

        // Calculate Platform Movement
        if(Time.time < nextMoveTime)
        {
            velocity = Vector3.zero;
        }
        else
        {
            fromWaypointIndex %= globalWaypoints.Length;
            int toWaypointIndex = (fromWaypointIndex+1) % globalWaypoints.Length;
            float distanceBetweenWaypoints = Vector3.Distance(globalWaypoints[fromWaypointIndex], globalWaypoints[toWaypointIndex]);
            percentBetweenWaypoints += Time.deltaTime * speed / distanceBetweenWaypoints;
            percentBetweenWaypoints = Mathf.Clamp01(percentBetweenWaypoints);
            float easedPercentBetweenWaypoints = Ease(percentBetweenWaypoints);

            Vector3 newPos = Vector3.Lerp(globalWaypoints[fromWaypointIndex], globalWaypoints[toWaypointIndex], easedPercentBetweenWaypoints);

            if(percentBetweenWaypoints >= 1)
            {
                percentBetweenWaypoints = 0;
                fromWaypointIndex++;

                if(!cyclic)
                {
                    if(fromWaypointIndex >= globalWaypoints.Length - 1)
                    {
                        fromWaypointIndex = 0;
                        System.Array.Reverse(globalWaypoints);
                    }
                }

                nextMoveTime = Time.time + waitTime;
            }

            velocity = newPos - transform.position;
        }

        ///

        HashSet<Transform> movedPassengers = new HashSet<Transform>(); // HashSets are fast at adding and checking if they contain certain things
        passengerMovement = new List<PassengerMovement>();

        Vector2 direction;
        direction.x = Mathf.Sign(velocity.x);
        direction.y = Mathf.Sign(velocity.y);

        // Vertically Moving Platform
        if(velocity.y != 0)
        {
            float rayLength = Mathf.Abs(velocity.y) + SKIN_WIDTH;

            for(int i = 0; i < verticalRayCount; ++i)
            {
                Vector2 rayOrigin = (direction.y == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
                rayOrigin += Vector2.right * (verticalRaySpacing*i);
                RaycastHit2D passenger = Physics2D.Raycast(rayOrigin, Vector2.up*direction.y, rayLength, passengerMask);

                if(passenger && (passenger.distance != 0))
                {
                    if(!movedPassengers.Contains(passenger.transform))
                    {
                        movedPassengers.Add(passenger.transform);
                        Vector2 push;
                        push.x = (direction.y == 1) ? velocity.x : 0;
                        push.y = velocity.y - (passenger.distance-SKIN_WIDTH)*direction.y;

                        passengerMovement.Add(new PassengerMovement(passenger.transform, new Vector3(push.x, push.y), (direction.y == 1), true));
                    }
                }
            }
        }

        // Horizontally Moving Platform
        if(velocity.x != 0)
        {
            float rayLength = Mathf.Abs(velocity.x) + SKIN_WIDTH;

            for(int i = 0; i < horizontalRayCount; ++i)
            {
                Vector2 rayOrigin = (direction.x == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
                rayOrigin += Vector2.up * (horizontalRaySpacing*i);
                RaycastHit2D passenger = Physics2D.Raycast(rayOrigin, Vector2.right*direction.x, rayLength, passengerMask);

                if(passenger && (passenger.distance != 0))
                {
                    if(!movedPassengers.Contains(passenger.transform))
                    {
                        movedPassengers.Add(passenger.transform);
                        Vector2 push;
                        push.x = velocity.x - (passenger.distance-SKIN_WIDTH)*direction.x;
                        push.y = -SKIN_WIDTH;

                        passengerMovement.Add(new PassengerMovement(passenger.transform, new Vector3(push.x, push.y), false, true));
                    }
                }
            }
        }

        // Passenger on Top of non-upward moving platform
        if(direction.y == -1 || (velocity.y == 0 && velocity.x != 0))
        {
            float rayLength = SKIN_WIDTH * 2;

            for(int i = 0; i < verticalRayCount; ++i)
            {
                Vector2 rayOrigin = raycastOrigins.topLeft + Vector2.right*(verticalRaySpacing*i);
                RaycastHit2D passenger = Physics2D.Raycast(rayOrigin, Vector2.up, rayLength, passengerMask);

                if(passenger && (passenger.distance != 0))
                {
                    if(!movedPassengers.Contains(passenger.transform))
                    {
                        movedPassengers.Add(passenger.transform);
                        Vector2 push;
                        push.x = velocity.x;
                        push.y = velocity.y;

                        passengerMovement.Add(new PassengerMovement(passenger.transform, new Vector3(push.x, push.y), true, false));
                    }
                }
            }
        }

        MovePassengers(true);
        transform.Translate(velocity);
        MovePassengers(false);
    }

    void OnDrawGizmos()
    {
        if (localWaypoints != null)
        {
            Gizmos.color = Color.cyan;
            float size = .3f;

            for (int i = 0; i < localWaypoints.Length; ++i)
            {
                Vector3 globalWaypointPos = (Application.isPlaying) ? globalWaypoints[i] : localWaypoints[i] + transform.position;
                Gizmos.DrawLine(globalWaypointPos - Vector3.up * size, globalWaypointPos + Vector3.up * size);
                Gizmos.DrawLine(globalWaypointPos - Vector3.left * size, globalWaypointPos + Vector3.left * size);
            }
        }
    }
}