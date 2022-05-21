using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Valve.VR.InteractionSystem;

public class TogglePhysics : MonoBehaviour
{


    public bool physicsOffShortlyAfterStart = true;
    public float timeToWait = 0.2f;
    private bool hasBeenToldToStayOn = false;
    private List<Collider> collidersToToggle = new List<Collider>();
    private Interactable interactable;

    // direct access to rigidbody has been fully deprecated (throws NotSupportedException)
    // hiding UnityEngine.component.rigidbody is perfectly fine.
#pragma warning disable CS0108, CS0114
    private Rigidbody rigidbody;
#pragma warning restore CS0108, CS0114

    void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (physicsOffShortlyAfterStart) StartCoroutine(sleepAfterStart());
        collidersToToggle = GetComponents<Collider>().Where(c => c.enabled).ToList();
        interactable = GetComponent<Interactable>();

    }
    
    IEnumerator sleepAfterStart()
    {
        yield return new WaitForSeconds(timeToWait);
        if (!hasBeenToldToStayOn) Toggle(false);
    }


    public void Toggle(bool? toSet)
    {
        if (rigidbody == null) return;
        toSet = toSet ?? !rigidbody.detectCollisions;
        var value = toSet.Value;
        hasBeenToldToStayOn = value;
        rigidbody.detectCollisions = value;
        rigidbody.useGravity = value;
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        // Toggling the colliders and the Interactable might be overkill, but 
        // Looking into SteamVR Hand, it seems that it might check overlapped colliders irrespective.
        foreach (var c in collidersToToggle)
        {
            c.enabled = value;
        }
        if (interactable != null) interactable.enabled = value;


    }
    public void PhysOff()
    {
        Toggle(false);
    }

    public void PhysOn()
    {
        Toggle(true);
    }

}
