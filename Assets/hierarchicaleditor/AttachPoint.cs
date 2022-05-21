using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using PlayStructure;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using Valve.VR.InteractionSystem;

namespace PlayStructure
{
    [Serializable]
    public class AttachPoint
    {
        [HideInInspector] public string name = "unattached";
        public BuildingPiece owningPiece;
        public Transform attachTransform;

        [SerializeField]
        private AlignSymmetry _mAlignSymmetry = AlignSymmetry.S_90;

        public Quaternion[] OrientationsToAlignTo
        {
            get
            {
                switch (_mAlignSymmetry)
                {
                    case AlignSymmetry.S_90: return _mRot90;
                    case AlignSymmetry.S_180: return _mRot180;
                }
                return _mRotNone;
            }
        }

        private static Quaternion[] _mRot90 =
        {
            Quaternion.identity,
            Quaternion.AngleAxis(90f, Vector3.forward),
            Quaternion.AngleAxis(270f, Vector3.forward),
            Quaternion.AngleAxis(180f, Vector3.forward),
        };

        private static Quaternion[] _mRot180 =
        {
            Quaternion.identity,
            Quaternion.AngleAxis(180f, Vector3.forward),
        };

        private static Quaternion[] _mRotNone = { Quaternion.identity, };

            
            

        //
        public enum AttachState
        {
            FREE, // Unattached to any other building piece in any way.
            LOOSE_LOCK, // Inserted, e.g. Can pull apart without being stopped
            PARTIAL_LOCK, // At least one screw has been put in
            FULL_LOCK
        } // All screws associated with this AttachPoint have been used.

        public enum AlignBehavior
        {
            NONE,
            ALIGN_THIS,
            ALIGN_OTHER
        }

        public enum AlignSymmetry
        {
            NONE, // use canonical orientations only
            S_180, // use canonical and 180 degree offset
            S_90, // use 90 degree offsets
        }

        [SerializeField]
        private AttachState _mCurrentAttachState;

        public AttachState currentAttachState
        {
            get
            {
                if (attachedAttachPoint == null || _mCurrentAttachState != AttachState.FREE)
                    return _mCurrentAttachState;
                Debug.LogWarning($"{owningPiece.transform.name} attached to an " +
                                 "{attachedAttachPoint.owningPiece.transform.name}, but AttachState was free");
                // Set it to at least loose lock.
                _mCurrentAttachState = AttachState.LOOSE_LOCK;
                return _mCurrentAttachState;
            }
        }

        private ScrewPiece currentScrewPiece;
        public void DoPartialLock(ScrewPiece sp)
        {
            currentScrewPiece = sp;
            _mCurrentAttachState = AttachState.PARTIAL_LOCK;
        }

        public void DoFullLock()
        {
            // Update the type of attach to a hierarchical lock from a configurable joint lock.
            var otherAP = attachedAttachPoint;
            Detach(true);
            Attach(otherAP, attachMode:BuildStructure.AttachMode.Hierarchical, ignoreIsFree:true);
            _mCurrentAttachState = AttachState.FULL_LOCK;
        }


        // 
        //BUG: this reference gets lost when entering play mode...
        // TODO Step 1: make sure that the order is predictable by manually assigning in prefab...
        // DONE Step 2: let us continue referencing the other by merit of the BuildingPiece upon which it occurs.
        // Easiest option would be a private property that resolves based on the current attached BuildingPiece and its index.
        [SerializeField, HideInInspector]
        private BuildingPiece _mOtherBuildingPiece;
        [SerializeField, HideInInspector]
        private int _mOtherBuildingPieceAttachPointIndex;

        private AttachPoint _mAttachedAttachPoint
        {
            get
            {
                if (_mOtherBuildingPiece != null && _mOtherBuildingPieceAttachPointIndex!= -1)
                    return _mOtherBuildingPiece.attachPoints[_mOtherBuildingPieceAttachPointIndex];
                return null;
            }
            set
            {
                if (value == null)
                {
                    _mOtherBuildingPiece = null;
                    _mOtherBuildingPieceAttachPointIndex = -1;
                }
                else
                {
                    _mOtherBuildingPiece = value.owningPiece;
                    _mOtherBuildingPieceAttachPointIndex = _mOtherBuildingPiece.attachPoints.FindIndex(a => a == value);
                }
            }
        }
        public AttachPoint attachedAttachPoint => _mAttachedAttachPoint;
        public Quaternion thisToOther;
        //public FixedJoint fixedJoint;
        public ConfigurableJoint configurableJoint;

        public AttachPoint(BuildingPiece owningPiece, Transform attachTransform)
        {
            this.owningPiece = owningPiece;
            this.attachTransform = attachTransform;
            this._mCurrentAttachState = AttachState.FREE;
        }

        // Helper functions:
        public bool isFree => currentAttachState == AttachState.FREE;
        public bool isLoose => currentAttachState == AttachState.LOOSE_LOCK;


        public void Attach(AttachPoint otherAP, AttachState state = AttachState.LOOSE_LOCK,
            AlignBehavior align = AlignBehavior.ALIGN_THIS, AlignSymmetry alignSymmetry = AlignSymmetry.S_90,
            BuildStructure.AttachMode? attachMode = null, bool ignoreIsFree = false)
        {
            var initialBPPosition = owningPiece.transform.position;
            var initialOBPPosition = otherAP.owningPiece.transform.position;
            attachMode ??= BuildStructure.instance.attachMode;
            if (!ignoreIsFree && (!isFree || !otherAP.isFree))
            {
                Debug.LogError(
                    $"Tried to attach {owningPiece.name + attachTransform.name} to "
                    + $"{otherAP.owningPiece.name + otherAP.attachTransform.name}; "
                    + $"couldn't because attach state is {currentAttachState.ToString()}, "
                    + $"to {attachedAttachPoint.owningPiece.name + attachedAttachPoint.attachTransform.name}");
                return;
            }

            var (potentialBuildingRot, _) = owningPiece.GetPotentialAlignment(this, otherAP);
            var potentialThisToOther = Quaternion.Inverse(potentialBuildingRot * attachTransform.localRotation) *
                                       otherAP.attachTransform.rotation;
            
            // check if attach is good, if so, continue on AP that might have originally been this, but has possibly
            // been rotated out.
            (bool goodAttach, AttachPoint continueOnAP, Quaternion newOffset) =
                PlayInstructions.instance.CheckAttach(this, otherAP, potentialThisToOther);
            if (!goodAttach)
            {
                Debug.Log("Will not attach because incorrect.");
                return;
            }
            continueOnAP.ContinueAttachAfterChecking(otherAP, state, align, alignSymmetry, attachMode, ignoreIsFree, newOffset,
                initialBPPosition,initialOBPPosition);
        }

        private void ContinueAttachAfterChecking(AttachPoint otherAP, AttachState state,
                 AlignBehavior align, AlignSymmetry alignSymmetry, 
                 BuildStructure.AttachMode?attachMode, bool ignoreIsFree, Quaternion newOffset,
                 Vector3 initialBPPosition, Vector3 initialOBPPosition){
            //Get most reasonable alignment first, use that!
            var orientationsToCheck = new List<Quaternion> { Quaternion.identity };
            switch (alignSymmetry)
            {
                case AlignSymmetry.S_90:
                    orientationsToCheck.Add(Quaternion.AngleAxis(90f, Vector3.forward));
                    orientationsToCheck.Add(Quaternion.AngleAxis(270f, Vector3.forward));
                    orientationsToCheck.Add(Quaternion.AngleAxis(180f, Vector3.forward));
                    break;
                case AlignSymmetry.S_180:
                    orientationsToCheck.Add(Quaternion.AngleAxis(180f, Vector3.forward));
                    break;
            }
            
            Quaternion bestOffsetRotation = Quaternion.identity;
            switch (align)
            {
                case AlignBehavior.ALIGN_THIS:
                    //TODO: check this:
                    Debug.Log($"Aligning self by {newOffset.eulerAngles}");
                    owningPiece.transform.rotation = owningPiece.transform.rotation * newOffset;
                    owningPiece.AlignToOther(this, otherAP);
                    break;
                case AlignBehavior.ALIGN_OTHER:
                    //TODO: check this:
                    Debug.Log($"Aligning other by {newOffset.eulerAngles}");
                    otherAP.owningPiece.transform.rotation = otherAP.owningPiece.transform.rotation * newOffset;
                    otherAP.owningPiece.AlignToOther(otherAP, this);
                    break;
                case AlignBehavior.NONE:
                    break;
            }
            //Debug.Log($"best offset: {bestOffsetRotation}");

            

            // Set the reference to the other attached point
            _mAttachedAttachPoint = otherAP;
            name = $"Attached to {otherAP.owningPiece.transform.name}";
            otherAP._mAttachedAttachPoint = this;
            otherAP.name = $"Attached to {owningPiece.transform.name}";

            // Set the attached states
            _mCurrentAttachState = state;
            otherAP._mCurrentAttachState = state;

            // Set the relative orientations
            thisToOther = Quaternion.Inverse(attachTransform.rotation) * otherAP.attachTransform.rotation;
            otherAP.thisToOther = Quaternion.Inverse(thisToOther);

            //switch (BuildStructure.instance.attachMode)
            switch(attachMode)
            {
                case BuildStructure.AttachMode.ConfigurableJoint:
                    // Make the configurable joint
                    configurableJoint = owningPiece.gameObject.AddComponent<ConfigurableJoint>();
                    configurableJoint.connectedBody = otherAP.owningPiece.GetComponent<Rigidbody>();
                    configurableJoint.xMotion = ConfigurableJointMotion.Locked;
                    configurableJoint.yMotion = ConfigurableJointMotion.Locked;
                    configurableJoint.zMotion = ConfigurableJointMotion.Locked;
                    configurableJoint.angularXMotion = ConfigurableJointMotion.Locked;
                    configurableJoint.angularYMotion = ConfigurableJointMotion.Locked;
                    configurableJoint.angularZMotion = ConfigurableJointMotion.Locked;
                    configurableJoint.projectionMode = JointProjectionMode.PositionAndRotation;
                    otherAP.configurableJoint = configurableJoint;
                    break;
                case BuildStructure.AttachMode.Hierarchical:
                    //check if this or the other has an owning piece:
                    GameObject substructure = null;
                    if (owningPiece.transform.parent != null)
                    {
                        substructure = owningPiece.transform.parent.gameObject;
                    }
                    // if both have a parent, default to other piece, since this is the one being attached.
                    if (otherAP.owningPiece.transform.parent != null)
                    {
                        substructure = otherAP.owningPiece.transform.parent.gameObject;
                    }

                    // Create new parent if neither had a parent
                    if (substructure == null)
                    {
                        substructure = new GameObject("substructure");
                        substructure.transform.position = otherAP.owningPiece.transform.position;
                        substructure.AddComponent<Rigidbody>();
                        var rd = substructure.AddComponent<ReplaceDropped>();
                        rd.startPosition = PlayInstructions.instance.transform.position;
                        rd.startRotation = PlayInstructions.instance.transform.rotation;
                        var otherAPThrowable = otherAP.owningPiece.GetComponent<Valve.VR.InteractionSystem.Throwable>();
                        var substructureThrowable = substructure.AddComponent<Valve.VR.InteractionSystem.Throwable>();
                        // If there are any other relevant changed values, make sure to copy them over here:
                        substructureThrowable.attachmentFlags = otherAPThrowable.attachmentFlags;
                    }

                    Hand holdingOtherBuildingPiece = null;
                    // Drop the held pieces
                    foreach (var hand in BuildStructure.instance.hands)
                    {
                        if (hand.AttachedObjects.Any(a => a.attachedObject == owningPiece.gameObject))
                        {
                            hand.DetachObject(owningPiece.gameObject,true);
                        }
                        if (hand.AttachedObjects.Any(a => a.attachedObject == otherAP.owningPiece.gameObject))
                        {
                            hand.DetachObject(otherAP.owningPiece.gameObject,true);
                            holdingOtherBuildingPiece = hand;
                        }
                    }
                    

                    // Force a grab onto substructure for the hand that was holding the piece that has the otherAP:
                    if (holdingOtherBuildingPiece != null)
                        holdingOtherBuildingPiece.AttachObject(substructure,
                            holdingOtherBuildingPiece.GetBestGrabbingType(),
                            substructure.GetComponent<Valve.VR.InteractionSystem.Throwable>().attachmentFlags);
                    
                    //We need to remove throwable and rigidbody on these since the substructure will take care of those
                    var thisThrowable = owningPiece.GetComponent<Valve.VR.InteractionSystem.Throwable>();
                    if (thisThrowable != null)
                    {
                        GameObject.Destroy(thisThrowable);
                    }
                    var thisRigidbody = owningPiece.GetComponent<Rigidbody>();
                    if (thisRigidbody != null)
                    {
                        GameObject.Destroy(thisRigidbody);
                    }

                    var thisInteractable = owningPiece.GetComponent<Interactable>();
                    if (thisInteractable != null)
                    {
                        GameObject.Destroy(thisInteractable);
                    }
                    var otherThrowable = otherAP.owningPiece.GetComponent<Valve.VR.InteractionSystem.Throwable>();
                    if (otherThrowable != null)
                    {
                        GameObject.Destroy(otherThrowable);
                    }
                    var otherRigidbody = otherAP.owningPiece.GetComponent<Rigidbody>();
                    if (otherRigidbody != null)
                    {
                        GameObject.Destroy(otherRigidbody);
                    }
                    var otherInteractable = otherAP.owningPiece.GetComponent<Interactable>();
                    if (otherInteractable != null)
                    {
                        GameObject.Destroy(otherInteractable);
                    }
                    // Set them under the substructure in the hierarchy.
                    owningPiece.transform.parent = substructure.transform;
                    otherAP.owningPiece.transform.parent = substructure.transform;
                    // Bump up the substructure if needed
                    //if (owningPiece.transform.position.y < .99f || otherAP.owningPiece.transform.position.y < .99f)
                    //{
                    //    substructure.transform.position =
                    //        initialBPPosition - (substructure.transform.rotation * owningPiece.transform.localPosition);
                    //    //substructure.transform.position = substructure.transform.position +
                    //                                      //Vector3.up * 0.15f;
                    //    var rb = substructure.GetComponent<Rigidbody>();
                    //    rb.velocity = Vector3.zero;
                    //    rb.position = Vector3.zero;
                    //}
                    break;
                    
            }
            // Make the fixed joint
            //fixedJoint = owningPiece.gameObject.AddComponent<FixedJoint>();
            //fixedJoint.connectedBody = otherAP.owningPiece.GetComponent<Rigidbody>();
            //otherAP.fixedJoint = fixedJoint;
            
            

        }
        

        public void Detach(bool force = false)
        {
            if (!force && !(isLoose && attachedAttachPoint is { isLoose: true }))
            {
                Debug.LogWarning("Won't detach locked objects");
                return;
            }

            // Handle cleanup on attached attach point first, since we'll be clearing our reference to it soon.
            if (attachedAttachPoint != null)
            {
                // Destroy the fixed joint
                //if (attachedAttachPoint.fixedJoint != null)
                //    UnityEngine.Object.Destroy(fixedJoint);
                //attachedAttachPoint.fixedJoint = null;
                if (attachedAttachPoint.configurableJoint != null)
                    UnityEngine.Object.Destroy(configurableJoint);
                attachedAttachPoint.configurableJoint = null;
                // Clear the relative orientations
                attachedAttachPoint.thisToOther = Quaternion.identity;
                // Free the attach states
                attachedAttachPoint._mCurrentAttachState = AttachState.FREE;
                // Clear the reference to the other AttachPoint
                attachedAttachPoint._mAttachedAttachPoint = null;
                attachedAttachPoint.name = "unattached";
            }

            // Destroy the fixed joint
            //if (fixedJoint != null) UnityEngine.Object.Destroy(fixedJoint);
            //fixedJoint = null;
            if (configurableJoint != null) UnityEngine.Object.Destroy(configurableJoint);
            configurableJoint = null;
            // Clear the relative orientations
            thisToOther = Quaternion.identity;
            // Free the attach states
            _mCurrentAttachState = AttachState.FREE;
            // Clear the reference to the other AttachPoint
            _mAttachedAttachPoint = null;
            name = "unattached";

        }


    }
}