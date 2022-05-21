using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SubstructurePositioningHint : MonoBehaviour
{

    public bool keepSecretUntilFirstInPlace = false;
    public UnityEvent FirstInPlace;

    public UnityEvent<bool> alignEvent;

    [SerializeField] private bool _doSuspendHint;

    public bool doSuspendHint
    {
        get => _doSuspendHint;
        set => _doSuspendHint = value;
    }

    [Tooltip("leave this null to use the transform this is attached to")]
    public Transform targetTransform;

    public Transform substructure;

    public Transform hintTransform;
        
    [Tooltip("the acceptable angle offset on the Y axis before displaying the hint to return to the target pose.")]
    public float yAngleOffset = 20f;

    [Tooltip("the acceptable angle offset on other axes before displaying the hint to return to the target pose.")]
    public float otherAngleOffset = 5f;

    [Tooltip("the acceptable distance offset before displaying the hint to return to the target pose.")]
    public float positionalOffset = 0.8f;

    [SerializeField] private float lerpDuration = 2f;
    [SerializeField] private float holdDuration = 0.3f;

    public Material hintMaterial;

    public bool lingerHintToComplete = false;
    private bool beAnimating = true;
    private bool beShowingTheHint = false;
    public bool isAligned => !beShowingTheHint;
    private float startTime = float.NegativeInfinity;

    void Awake()
    {
        if (targetTransform == null)
        {
            targetTransform = transform;
        }
    }

    void OnEnable()
    {
        StartCoroutine(Animate());
        UpdateMeshes();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    // Update is called once per frame
    private bool wasShowing = false;
    void Update()
    {
        if (substructure == null) return;
        var fromTo =substructure.rotation * Quaternion.Inverse(targetTransform.rotation);
        var yPlaneRotatedForward = Vector3.ProjectOnPlane(fromTo * Vector3.forward,Vector3.up);
        // show the hint if the y-plane angle offset is above the threshold.
        beShowingTheHint = Vector3.Angle(Vector3.forward, yPlaneRotatedForward) > yAngleOffset
           // kinda gross, but since we already computed the y-plane flattened, rotated forward vector, we can invert
           // the rotation back to get just the inverse of the remaining offset that we had squashed.
            || Vector3.Angle(Quaternion.Inverse(fromTo) * yPlaneRotatedForward, Vector3.forward) > otherAngleOffset
           // show the hint if the position offset is above the threshold.
            || Vector3.Distance(targetTransform.position, substructure.position) > positionalOffset;
        if (wasShowing != beShowingTheHint) alignEvent?.Invoke(isAligned);
        wasShowing = beShowingTheHint;
        if (keepSecretUntilFirstInPlace && !beShowingTheHint)
        {
            FirstInPlace?.Invoke();
            keepSecretUntilFirstInPlace = false;
            var rd = substructure.gameObject.GetComponent<ReplaceDropped>();
            if (rd != null)
            {
                rd.startPosition = targetTransform.position;
                rd.startRotation = targetTransform.rotation;
            }
        }
    }

    // This needs to get called whenever things are added to the substructure.
    public void UpdateMeshes()
    {
        // if we don't know what the substructure is yet, search for it.
        if (substructure == null)
        {
            var s = GameObject.Find("substructure");
            if (s == null) return;
            substructure = s.transform;
        }
        // clear out our current hint transforms.
        for (var i = 0; i < hintTransform.childCount; i++)
        {
            Destroy(hintTransform.GetChild(0).gameObject);
        }
        // add hint recursive to our hintTransform, copying from substructure.
        AddHintRecursive(hintTransform, substructure);
    }

    private void AddHintRecursive(Transform copyTo, Transform copyFrom)
    {
        // copy the mesh if there is one.
        var cfMesh = copyFrom.GetComponent<MeshFilter>();
        if (cfMesh != null)
        {
            var cTGO= copyTo.gameObject;
            var m = cTGO.AddComponent<MeshFilter>();
            m.mesh = cfMesh.mesh;
            var r = cTGO.AddComponent<MeshRenderer>();
            r.material = hintMaterial;
        }
        // copy children and make sure their local positions and orientations match. Recurse.
        for (var i = 0; i < copyFrom.childCount; i++)
        {
            var fromChild = copyFrom.GetChild(i);
            var newChild = new GameObject(fromChild.name).transform;
            newChild.parent = copyTo;
            newChild.localPosition = fromChild.localPosition;
            newChild.localRotation = fromChild.localRotation;
            AddHintRecursive(newChild, fromChild);
        }
    }

    IEnumerator Animate()
    {
        while (beAnimating)
        {
            var time = Time.time;
            // allow the hint animation to complete even if beShowingTheHint is false, since I anticipate
            // we'll want a relatively large-ish range on the thresholds, and don't want to annoyingly trigger
            // the animations turning off on a hard wall.
            if (!keepSecretUntilFirstInPlace
                && (substructure!=null && (beShowingTheHint || (lingerHintToComplete && time <= startTime + lerpDuration + holdDuration)))
                && !doSuspendHint)
            {
                hintTransform.gameObject.SetActive(true);
                if (time > startTime + lerpDuration + holdDuration)
                {
                    startTime = time;
                }

                var animMoving = time <= startTime + lerpDuration;
                var lerp = (time - startTime) / lerpDuration;
                var animHolding = time > startTime + lerpDuration && time <= startTime + lerpDuration + holdDuration;

                if (animMoving)
                {
                    hintTransform.position = Vector3.Lerp(
                        substructure.position, targetTransform.position, lerp);
                    hintTransform.rotation = Quaternion.Lerp(
                        substructure.rotation, targetTransform.rotation, lerp);
                }

                if (animHolding)
                {
                    hintTransform.position = targetTransform.position;
                    hintTransform.rotation = targetTransform.rotation;
                }

            }
            else hintTransform.gameObject.SetActive(false);
            yield return new WaitForEndOfFrame();
        }
    }
}
