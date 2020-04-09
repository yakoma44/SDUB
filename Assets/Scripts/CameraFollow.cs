using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    struct FocusArea
    {
        public Vector2 center;
        public Vector2 velocity;

        float left;
        float right;
        float top;
        float bottom;

        public FocusArea(Bounds targetBounds, Vector2 size)
        {
            left = targetBounds.center.x - size.x / 2;
            right = targetBounds.center.x + size.x / 2;
            bottom = targetBounds.min.y;
            top = targetBounds.min.y + size.y;

            center = new Vector2((left + right) / 2, (top + bottom) / 2);
            velocity = Vector2.zero;
        }

        public void Update(Bounds targetBounds)
        {
            Vector2 shift = Vector2.zero;

            // X
            if (targetBounds.min.x < left)
            {
                shift.x = targetBounds.min.x - left;
            }
            else if (targetBounds.max.x > right)
            {
                shift.x = targetBounds.max.x - right;
            }

            left += shift.x;
            right += shift.x;

            // Y
            if (targetBounds.min.y < bottom)
            {
                shift.y = targetBounds.min.y - bottom;
            }
            else if (targetBounds.max.y > top)
            {
                shift.y = targetBounds.max.y - top;
            }

            top += shift.y;
            bottom += shift.y;

            ///

            center = new Vector2((left + right) / 2, (top + bottom) / 2);
            velocity = new Vector2(shift.x, shift.y);
        }
    }

    public Controller2D target;
    public Vector2 focusAreaSize;

    public float verticalOffset;
    public float xLookAheadDistance;
    public float xLookSmoothTime;
    public float verticalSmoothTime;

    FocusArea focusArea;

    float xCurrentLookAhead;
    float xTargetLookAhead;
    float xLookAheadDirection;
    float xSmoothLookVelocity;
    float ySmoothVelocity;
    bool lookAheadStopped;


    void Start()
    {
        focusArea = new FocusArea(target.boxCollider.bounds, focusAreaSize);
    }

    void LateUpdate() // "End Step"
    {
        focusArea.Update(target.boxCollider.bounds);

        Vector2 focusPosition = focusArea.center + Vector2.up*verticalOffset;

        if(focusArea.velocity.x != 0)
        {
            xLookAheadDirection = Mathf.Sign(focusArea.velocity.x);
            if((Mathf.Sign(target.playerInput.x) == Mathf.Sign(focusArea.velocity.x)) && target.playerInput.x != 0)
            {
                xTargetLookAhead = xLookAheadDirection + xLookAheadDistance;
                lookAheadStopped = false;
            }
            else
            {
                if(!lookAheadStopped)
                {
                    xTargetLookAhead = xCurrentLookAhead + (xLookAheadDirection*xLookAheadDistance - xCurrentLookAhead)/4f;
                    lookAheadStopped = true;
                }
            }
        }

        xTargetLookAhead = xLookAheadDirection * xLookAheadDistance;
        xCurrentLookAhead = Mathf.SmoothDamp(xCurrentLookAhead, xTargetLookAhead, ref xSmoothLookVelocity, xLookSmoothTime);

        focusPosition.y = Mathf.SmoothDamp(transform.position.y, focusPosition.y, ref ySmoothVelocity, verticalSmoothTime);
        focusPosition += Vector2.right * xCurrentLookAhead;

        transform.position = (Vector3)focusPosition + Vector3.forward*-10;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.32f, 0.32f, 1.0f, 0.45f);
        Gizmos.DrawCube(focusArea.center, focusAreaSize);
    }
}
