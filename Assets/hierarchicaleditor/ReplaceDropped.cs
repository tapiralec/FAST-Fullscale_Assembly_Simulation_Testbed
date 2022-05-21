using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

public class ReplaceDropped : MonoBehaviour
{

    public float minHeightOfCenter = 0.7f;
    public float timeUnderMinToReset = 1f;
    private float currentTimeUnderMin = 0f;
    public bool onlyCountIfNotHeld = true;

    private TableBounds _tableBounds;
    private TableBounds tableBounds => _tableBounds ??= TableBounds.instance;

    [HideInInspector]public Quaternion startRotation;
    [HideInInspector]public Vector3 startPosition;
    
    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }
    
    void Update()
    {
        // don't accumulate time if this object is not top of hierarchy.
        if (transform.parent!=null && transform.parent.name!="PhysicalObjects") return;
        var pos = transform.position;
        
        // check if it's back in bounds and so we should clear out the timer.
        if (tableBounds.testBounds(pos))
        {
            currentTimeUnderMin = 0f;
        }
        else
        {
            // out of bounds: only accumulate time if the object is not being held.
            if (!Player.instance.leftHand.ObjectIsAttached(gameObject) &&
                !Player.instance.rightHand.ObjectIsAttached(gameObject))
                currentTimeUnderMin += Time.deltaTime;
            else if (onlyCountIfNotHeld) currentTimeUnderMin = 0f;
        }

        // We're done in Update if we're still under the maximum amount of time.
        if (!(currentTimeUnderMin > timeUnderMinToReset)) return;
        
        // Zero out the velocities on the rigidbody, and use it to replace if one exists:
        var r = GetComponent<Rigidbody>();
        if (r != null)
        {
            r.MovePosition(startPosition);
            r.MoveRotation(startRotation);
            r.velocity = Vector3.zero;
            r.angularVelocity = Vector3.zero;
        }
        // If there's no rigidbody (how did this fall?) reset it using the retained transform pose.
        else
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }
        
        // After resetting the position and orientation, reset the timer as well.
        currentTimeUnderMin = 0f;
    }
}
