using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller2D : RaycastController
{
    public struct CollisionInfo
    {
        public bool above;
        public bool below;
        public bool left;
        public bool right;
        public bool ascendingSlope;
        public bool descendingSlope;
        public int fallingThroughPlatformTimer;
        public float slopeAngle;
        public float slopeAnglePrevious;
        public Vector3 velocityPrevious;
    }

    const float MAX_SLOPE_ANGLE_ASCENDING = 80;
    const float MAX_SLOPE_ANGLE_DESCENDING = 80;

    public CollisionInfo collisions;
    [HideInInspector] public Vector2 playerInput;

    void Start()
    {
        StartRaycastController();
    }

    public void Move(Vector3 velocity, Vector2 input, bool standingOnPlatform = false)
    {
        // Reset values
        playerInput = input;

        collisions.above              = false;
        collisions.below              = false;
        collisions.left               = false;
        collisions.right              = false;
        collisions.ascendingSlope     = false;
        collisions.descendingSlope    = false;
        collisions.slopeAnglePrevious = collisions.slopeAngle;
        collisions.slopeAngle         = 0;
        collisions.velocityPrevious   = velocity;

        UpdateRaycastOrigins();

        // Descend Slope
        if(velocity.y <= 0)
        {
            float xDirection = Mathf.Sign(velocity.x);
            Vector2 rayOrigin = (xDirection == -1) ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, Mathf.Infinity, collisionMask);

            if(hit)
            {
                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                if(slopeAngle != 0 && slopeAngle <= MAX_SLOPE_ANGLE_DESCENDING)
                {
                    if(Mathf.Sign(hit.normal.x) == xDirection)
                    {
                        if(hit.distance - SKIN_WIDTH <= Mathf.Tan(slopeAngle*Mathf.Deg2Rad) * Mathf.Abs(velocity.x))
                        {
                            float moveDistance = Mathf.Abs(velocity.x);
                            float yDescendVelocity = Mathf.Sin(slopeAngle*Mathf.Deg2Rad) * moveDistance;
                            velocity.x = Mathf.Cos(slopeAngle*Mathf.Deg2Rad) * moveDistance * Mathf.Sign(velocity.x);
                            velocity.y -= yDescendVelocity;

                            collisions.slopeAngle = slopeAngle;
                            collisions.descendingSlope = true;
                            collisions.below = true;
                        }
                    }
                }
            }
        }

        // Horizontal Collisions
        if(velocity.x != 0)
        {
            float xDirection = Mathf.Sign(velocity.x);
            float rayLength = Mathf.Abs(velocity.x) + SKIN_WIDTH;

            for(int i = 0; i < horizontalRayCount; ++i)
            {
                Vector2 rayOrigin = (xDirection == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
                rayOrigin += Vector2.up * horizontalRaySpacing * i;
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right*xDirection, rayLength, collisionMask);

                Debug.DrawRay(rayOrigin, Vector2.right*xDirection*rayLength, Color.red);

                if(hit)
                {
                    if(hit.distance == 0)
                    {
                        continue;
                    }

                    float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                    
                    // Climb Slope
                    if(i == 0 && slopeAngle <= MAX_SLOPE_ANGLE_ASCENDING)
                    {
                        if(collisions.descendingSlope)
                        {
                            collisions.descendingSlope = false;
                            velocity = collisions.velocityPrevious;
                        }

                        float distanceToSlopeStart = 0;
                        if(slopeAngle != collisions.slopeAnglePrevious)
                        {
                            distanceToSlopeStart = hit.distance - SKIN_WIDTH;
                            velocity.x -= distanceToSlopeStart * xDirection;
                        }

                        float moveDistance = Mathf.Abs(velocity.x);
                        float yClimbVelocity = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

                        if(velocity.y <= yClimbVelocity)
                        {
                            velocity.x = Mathf.Cos(slopeAngle*Mathf.Deg2Rad) * moveDistance * Mathf.Sign(velocity.x);
                            velocity.y = yClimbVelocity;
                            collisions.below = true;
                            collisions.ascendingSlope = true;
                            collisions.slopeAngle = slopeAngle;
                        }

                        velocity.x += distanceToSlopeStart*xDirection;
                    }


                    if(!collisions.ascendingSlope || slopeAngle > MAX_SLOPE_ANGLE_ASCENDING)
                    {
                        velocity.x = (hit.distance - SKIN_WIDTH) * xDirection;
                        rayLength = hit.distance;

                        if (collisions.ascendingSlope)
                        {
                            velocity.y = Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(velocity.x);
                        }

                        collisions.left  = (xDirection == -1);
                        collisions.right = (xDirection ==  1);
                    }
                }
            }
        }

        // Vertical Collisions
        if(velocity.y != 0)
        {
            float yDirection = Mathf.Sign(velocity.y);
            float rayLength = Mathf.Abs(velocity.y) + SKIN_WIDTH;
            
            for (int i = 0; i < verticalRayCount; ++i)
            {
                Vector2 rayOrigin = (yDirection == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
                rayOrigin += Vector2.right * (verticalRaySpacing*i + velocity.x);
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up*yDirection, rayLength, collisionMask);

                Debug.DrawRay(rayOrigin, Vector2.up*yDirection*rayLength, Color.red);

                if(hit)
                {
                    // Move through platforms
                    if(hit.collider.CompareTag("Platform"))
                    {
                        if(yDirection == 1 || hit.distance == 0) { continue; }

                        if(collisions.fallingThroughPlatformTimer > 0) { continue; }

                        if(playerInput.y == -1)
                        {
                            collisions.fallingThroughPlatformTimer = 8; // Set timer
                            continue;
                        }
                    }
                    
                    if(collisions.fallingThroughPlatformTimer > 0) { --collisions.fallingThroughPlatformTimer; } // Timer countdown

                    // Other handling
                    velocity.y = (hit.distance - SKIN_WIDTH) * yDirection;
                    rayLength = hit.distance;

                    if(collisions.ascendingSlope)
                    {
                        velocity.x = velocity.y / Mathf.Tan(collisions.slopeAngle*Mathf.Deg2Rad) * Mathf.Sign(velocity.x);
                    }

                    collisions.above = (yDirection ==  1);
                    collisions.below = (yDirection == -1);
                }
            }

            if(collisions.ascendingSlope)
            {
                float xDirection = Mathf.Sign(velocity.x);
                rayLength = Mathf.Abs(velocity.x) + SKIN_WIDTH;
                Vector2 rayOrigin = ((xDirection == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight) + Vector2.up*velocity.y;
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right*xDirection, rayLength, collisionMask);

                if(hit)
                {
                    float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                    
                    if(slopeAngle != collisions.slopeAngle)
                    {
                        velocity.x = (hit.distance-SKIN_WIDTH) * xDirection;
                        collisions.slopeAngle = slopeAngle;
                    }
                }
            }
        }

        // Commit changes
        transform.Translate(velocity);

        if(standingOnPlatform)
        {
            collisions.below = true;
        }
    }
}
