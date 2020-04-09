using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent (typeof(Controller2D))]
[RequireComponent (typeof(Animator))]
public class Player : MonoBehaviour
{
    [NonSerialized] public Vector3 velocity;
    Controller2D controller;
    Animator animator;

    public float X_WALK_ACCELERATION;   // NOTE(hayden): Affected by deltaTime
    public float X_WALK_FRICTION;
    public float X_RUN_ACCELERATION;    // NOTE(hayden): Affected by deltaTime
    public float X_RUN_FRICTION;
    public int   X_RUN_HOLDOVER_FRAMES; // Keep running after letting go of the button for x frames
    public float X_STOP_THRESHOLD;      // Stop moving if velocity.x is below this number
    public float X_BACKWARD_AERIAL_MOMENTUM_MULTIPLIER;
    public float X_SKID_AMOUNT;
    public float X_SKID_THRESHOLD;
    public float X_SKID_SNAP_THRESHOLD;

    public float Y_JUMP_HEIGHT_MIN;
    public float Y_JUMP_HEIGHT_MAX;
    public float Y_JUMP_TIME_TO_APEX;
    public float Y_JUMP_JOLT_FACTOR;    // NOTE(hayden): Must be a smallish value (prob < 5)
    public float Y_JUMP_GRAVITY_UPWARD;
    public float Y_JUMP_GRAVITY_DOWNWARD;

    Vector3 acceleration;
    float gravity;
    float jumpVelocityMin;
    float jumpVelocityMax;
    float jumpInitialHeight;
    int runningFramesLeft;
    bool jumping;
    bool skidding;
    int facing;

    /* TODO(hayden):
    ** set velocity.x to zero when landing while holding backward?
    ** variable jump height
    ** aerial momentum
    ** running jump height?
    ** actual floating
    ** FixedUpdate()
    ** double jumps
    ** wall jumping
    ** dash
    ** jump "forgiveness" frames
    ** comment public values
    */

    void Start()
    {
        controller = GetComponent<Controller2D>();
        animator = GetComponent<Animator>();

        /// Initialize values
        facing = 1;
        jumping = false;
        skidding = false;
    }

    void Update()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        int xInputDirection = Math.Sign(input.x);

        // X ////////////////////////////////////////////////////////////////////////
        {
            // Don't maintain momentum upon left/right collision
            if(controller.collisions.left || controller.collisions.right)
            {
                velocity.x = 0;
                acceleration.x = 0;
            }

            // Walking
            acceleration.x = X_WALK_ACCELERATION;
            float friction = (1 - X_WALK_FRICTION);

            // Running
            if(Input.GetKey(KeyCode.Z))
            {
                runningFramesLeft = X_RUN_HOLDOVER_FRAMES;
            }
            else 
            { 
                --runningFramesLeft; 
            }

            if((Input.GetKey(KeyCode.Z) && Mathf.Abs(input.x) > 0) || runningFramesLeft > 0)
            {
                acceleration.x = X_RUN_ACCELERATION;
                friction = (1 - X_RUN_FRICTION);
            }

            // Stop floating-point sub-pixel slide
            float xVelocityAbs = Mathf.Abs(velocity.x);
            int xVelocitySign = Math.Sign(velocity.x);

            if(xInputDirection == 0 && (xVelocityAbs < X_STOP_THRESHOLD))
            {
                velocity.x = 0;
            }

            // Skidding
            {
                if((xVelocityAbs > 0) && (xVelocitySign == -xInputDirection) && controller.collisions.below)
                {
                    if(xVelocityAbs >= X_SKID_THRESHOLD)
                    {
                        velocity.x -= X_SKID_AMOUNT * xVelocitySign * Time.deltaTime;
                        skidding = true;
                    }

                    // Skid Snap/Turnaround
                    if(xVelocityAbs <= X_SKID_SNAP_THRESHOLD) // NOTE(hayden): Enable for slightly "snappier" turns
                    {
                        //acceleration.x = -X_WALK_ACCELERATION * xVelocitySign;
                    }
                }
                else if((xVelocitySign == xInputDirection) || (xInputDirection == 0))
                {
                    skidding = false;
                }
            }

            // Aerial acceleration
            if ((controller.collisions.below == false) && (facing == -xInputDirection))
            {
                acceleration.x *= X_BACKWARD_AERIAL_MOMENTUM_MULTIPLIER;
            }

            // Commit!
            velocity.x += xInputDirection * acceleration.x * Time.deltaTime;
            velocity.x *= friction;
        }

        // Y ////////////////////////////////////////////////////////////////////////
        {
            float jumpGravityUpward   = -((Y_JUMP_GRAVITY_UPWARD*Y_JUMP_HEIGHT_MAX) / (Y_JUMP_TIME_TO_APEX*Y_JUMP_TIME_TO_APEX));
            float jumpGravityDownward = -((Y_JUMP_GRAVITY_DOWNWARD*Y_JUMP_HEIGHT_MAX) / (Y_JUMP_TIME_TO_APEX*Y_JUMP_TIME_TO_APEX));

            gravity = jumpGravityUpward;
            // Variable jump height
            jumpVelocityMin = Mathf.Sqrt(2 * Mathf.Abs(gravity) * Y_JUMP_HEIGHT_MIN);
            jumpVelocityMax = Mathf.Abs(gravity) * Y_JUMP_TIME_TO_APEX;

            // Don't maintain momentum upon above/below collision
            if(controller.collisions.above || controller.collisions.below) // NOTE(hayden): This may need to be moved below the call to Move()
            {
                velocity.y = 0;
                gravity = 0;
            }

            // Jump!
            {
                // Start jump
                if(Input.GetKeyDown(KeyCode.X) && controller.collisions.below)
                {
                    velocity.y = jumpVelocityMax;
                    jumpInitialHeight = transform.position.y;
                    jumping = true;
                }
                // Jump Ended/Released
                else if(Input.GetKeyUp(KeyCode.X) || velocity.y <= 0)
                {
                    if(velocity.y > jumpVelocityMin)
                    {
                        velocity.y = jumpVelocityMin;
                    }

                    gravity = jumpGravityDownward;
                }
                // Apply jolt
                else if(Input.GetKey(KeyCode.X) && (velocity.y > 0))
                {
                    gravity = jumpGravityUpward + Y_JUMP_JOLT_FACTOR * (transform.position.y-jumpInitialHeight);
                }

                // Set jump off
                if(controller.collisions.below && velocity.y <= 0)
                {
                    jumping = false;
                }
            }

            // Commit!
            acceleration.y = gravity; // NOTE(hayden): acceleration.y and gravity are the same thing
            velocity.y += acceleration.y * Time.deltaTime;
        }

        controller.Move(velocity*Time.deltaTime, input);

        // TEMP (Animation) /////////////////////
        if(jumping == false)
        {
            if((xInputDirection == 1 && facing == -1) || (xInputDirection == -1 && facing == 1))
            {
                Vector3 scale = transform.localScale;
                scale.x *= -1;
                transform.localScale = scale;
                facing = -facing;
            }
        }

        animator.SetBool("skidding", skidding);
        ///
    }

    void FixedUpdate()
    {
    }
}