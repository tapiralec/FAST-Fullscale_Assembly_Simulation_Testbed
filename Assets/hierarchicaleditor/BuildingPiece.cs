using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace PlayStructure
{

    //TODO: make sure to follow through on the meeting c RPM re virtual hand offset when grasping the structure
    //TODO: make sure to set up the automatic un-grasp with re-grasping for attaching in-hand.
    
    public enum PieceType
    {
        // nominal pieces
        PIPE, PANEL,
        
        // joints
        C_2WAY, C_ELBOW,
        C_TEE, C_3WAY,
        C_4WAY,
        C_5WAY,
        
        // things to attach with
        SCREW,
        SCREW_PANEL
    }

    public enum PieceColor
    {
        BLACK,
        RED,YELLOW,GREEN,BLUE
    }

    public class BuildingPiece : MonoBehaviour
    {
        [Tooltip("Will automatically get incremented on added to scene. Keep at -1 in prefab!")]
        [SerializeField]
        private int _mBuildingPieceID = -1;

        public int buildingPieceID
        {
            get
            {
                if (_mBuildingPieceID == -1)
                {
                    _mBuildingPieceID = BuildStructure.instance.AddBuildingPiece(this);
                }
                return _mBuildingPieceID;
            }
            set => _mBuildingPieceID = value;
        }

        [SerializeField] private PieceType _pieceType;
        public PieceType pieceType => _pieceType;
        public bool isPipe => _pieceType == PieceType.PIPE;
        public bool isPanel => _pieceType == PieceType.PANEL;
        public bool isConnector =>
            _pieceType == PieceType.C_2WAY ||
            _pieceType == PieceType.C_3WAY ||
            _pieceType == PieceType.C_4WAY ||
            _pieceType == PieceType.C_5WAY ||
            _pieceType == PieceType.C_TEE  ||
            _pieceType == PieceType.C_ELBOW;
        public bool isScrew => _pieceType == PieceType.SCREW || _pieceType == PieceType.SCREW_PANEL;
        [SerializeField] private PieceColor _pieceColor;
        public PieceColor pieceColor => _pieceColor;

        [SerializeField] private List<AttachPoint> _attachPoints;

        public List<AttachPoint> attachPoints
        {
            get
            {
                if (_attachPoints == null || _attachPoints.Count == 0)
                {
                    Debug.LogWarning($"Generating Attach Points for {transform.name}"+
                                     "Make this manually assigned!");
                    //_attachPoints = GenAttachPoints();
                }

                return _attachPoints;
            }
            set
            {
                Debug.Log($"Setting Attach Points for {transform.name}");
                _attachPoints = value;
            }
                
        }

        private List<AttachPoint> GenAttachPoints()
        {
            var points = new List<AttachPoint>();
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).name.Contains("Attach"))
                {
                    Debug.LogWarning($"Automatically adding an AttachPoint to {name}. This is bad for " +
                                     "replication. Please set AttachPoints in prefab.");
                    points.Add(new AttachPoint(this, transform.GetChild(i)));
                }
            }

            return points;
        }

        public void RefreshAttachPoints()
        {
            foreach (var ap in attachPoints)
            {
                ap.Detach(force:true);
            }

            Debug.LogWarning($"manually resetting the AttachPoints for {name}- ideally these should be" +
                             " set in prefab, not generated for consistency!");
            //
            //attachPoints = GenAttachPoints();  
        } 

        public List<Transform> attachPointTransforms => attachPoints.Select(x => x.attachTransform).ToList();

        public List<Symmetry> symmetries;

        public void SnapToOther(int attachPointID, BuildingPiece otherBuildingPiece, int otherAttachPointID) =>
            SnapToOther(attachPoints[attachPointID], otherBuildingPiece.attachPoints[otherAttachPointID]);
        public void SnapToOther(AttachPoint thisAttachPoint, BuildingPiece otherBuildingPiece, int otherAttachPointID) =>
            SnapToOther(thisAttachPoint, otherBuildingPiece.attachPoints[otherAttachPointID]);
        public void SnapToOther(int attachPointID, AttachPoint otherAttachPoint) =>
            SnapToOther(attachPoints[attachPointID], otherAttachPoint);
        public void SnapToOther(AttachPoint thisAttachPoint, AttachPoint otherAttachPoint, AttachPoint.AttachState attachState = AttachPoint.AttachState.LOOSE_LOCK)
        {
            if (thisAttachPoint.isFree && otherAttachPoint.isFree)
            {
                thisAttachPoint.Attach(otherAttachPoint);
            }
            //AlignToOther(thisAttachPoint, otherAttachPoint);
        }

        public void AlignToOther(AttachPoint thisAttachPoint, AttachPoint otherAttachPoint,
            //List<Quaternion> alignOffsetsToCheck,
            Quaternion? alignOffset = null)
        {
            (transform.rotation, transform.position) = GetPotentialAlignment(thisAttachPoint, otherAttachPoint, alignOffset);
        }

        public (Quaternion, Vector3) GetPotentialAlignment(AttachPoint thisAttachPoint, AttachPoint otherAttachPoint, Quaternion? alignOffset = null)
        {
            // Find best alignOffset (use actual value if passed).
            if (alignOffset == null)
            {
                alignOffset =otherAttachPoint.OrientationsToAlignTo 
                    .OrderBy(q => Quaternion.Angle(
                        otherAttachPoint.attachTransform.rotation * Quaternion.AngleAxis(180f, Vector3.up) * q,
                        thisAttachPoint.attachTransform.rotation))
                    .First();
            }
            else
            {
                // workaround using System.Nullable since we can't default to Quaternion.identity.
                alignOffset ??= Quaternion.identity;
            }

            //Debug.Log($"using alignOffset={alignOffset}");
            Vector3 posOffset = Vector3.Scale(thisAttachPoint.attachTransform.localPosition, transform.localScale);
            Quaternion rotOffset = thisAttachPoint.attachTransform.localRotation;
            Quaternion outRotation =
                otherAttachPoint.attachTransform.rotation
                //*Quaternion.AngleAxis(180f, alignOffset.Value * Vector3.up) 
                * Quaternion.AngleAxis(180f, Vector3.up) * alignOffset.Value
                //Quaternion.LookRotation(-1 * otherAttachPoint.attachTransform.forward,
                    //otherAttachPoint.attachTransform.up)
                //* (alignOffset * otherAttachPoint.attachTransform.rotation)
                * Quaternion.Inverse(rotOffset);
            //Debug.Log(Quaternion.LookRotation(-1 * otherAttachPoint.forward, otherAttachPoint.up)
                      //* Quaternion.Inverse(rotOffset));
            Vector3 outPosition = otherAttachPoint.attachTransform.position - outRotation * posOffset;
            return (outRotation, outPosition);
        }


        public void CheckForNearbySnapsAndSnap()
        {
            var closestAttachPoint = Mathf.Infinity;
            (AttachPoint, AttachPoint)? toAttach = null;
            foreach (var ap in attachPoints)
            {
                if (BuildStructure.instance.GetClosestAttachPoint(ap, out var outAP, this)
                    && Vector3.Distance(ap.attachTransform.position, outAP.attachTransform.position) <
                    closestAttachPoint)
                {
                    closestAttachPoint = Vector3.Distance(ap.attachTransform.position, outAP.attachTransform.position);
                    toAttach = (ap, outAP);
                }
            }
            if (toAttach != null)
            {
                SnapToOther(toAttach.Value.Item1,toAttach.Value.Item2);
            }
        }
        
        public void BreakJoint(FixedJoint joint) =>  Destroy(joint);
        
        public void OnDestroy()
        {
            //break all attachments
            BuildStructure.instance.RemoveBuildingPiece(this);
        }
        
        //TODO: Change attaching mechanism from FixedJoints
        // Only thing that really would play well would probably be managing a structure that has its own RigidBody, the
        // parts lose theirs and are put under it in hierarchy.
        // The alternative would be doing PhysicsUpdate alignments, but those would have to be propagated multiple times
        // Consider grabbing the structure and pushing down towards the ground - we'd need to propagate back updates 
        // from any piece that collides with the ground since otherwise the rest of the structure could pop it through.
        
        //TODO: Need to set up connections at attachPoints, attachPoint types, 
        
        //TODO: Handle symmetrical orientation for hints and satisfying build instructions
        // This makes the most sense to do here in the BuildingPiece script.
        // Given a "real" piece, held in hand, and a target position and orientation, 
        // identify which of the given symmetries of this object afford the minimal offset to to the target orientations
        // steps:
        //  Determine the target orientation as it "exists" on the fromAttach piece:
        //      fromAttach.attachPoint.orientation * fromAttach.attachPoint.thisToOther
        //      * Quaternion.Inverse(toAttach.attachPoint.localRotation)
        //  Get best possibility of symmetrical options:
        //      Symmetries
        //          .Select(s=>s.Quaternion * targetOrientation)
        //          .Min(q=>Quaternion.AngleBetween(current.transform.orientation, q))
        // NB: also want to get the attachPointMapping so that we can determine if a given insertion is satisfactory or not.
        // Once a piece has been attached, we want to reference the appropriate attachPoint based on that piece's
        // AttachPointMapping thereon for further attachments.
        // This can probably be handled by appropriately rotating the appropriate InstructionStep attachPoint indices

        //TODO: Verify RotateIndices
        // once an object has been put in place, we'll want to be able to reference its appropriate symmetry rotation
        // based on how it was attached originally, thus we'll want to be able to apply a RotateIndices that'll
        // say that we're in a new orientation to use for attaching things etc.
        private Symmetry _mCurrentSymmetry;
        private Symmetry currentSymmetry
        {
            get
            {
                _mCurrentSymmetry ??= symmetries[0];
                return _mCurrentSymmetry;
            }
        }

        public void RotateIndices(int i) => RotateIndices(symmetries[i]);
        public void RotateIndices(Symmetry s)
        {
            if (s.attachPointMapping.Count != attachPoints.Count)
            {
                Debug.LogError($"Wrong count for attachPoint mappings to rotate indices! +" +
                               $"({s.attachPointMapping.Count} mappings, {attachPoints.Count} points)");
                return;
            }
            
            var temp_aps = new AttachPoint[attachPoints.Count];
            attachPoints.CopyTo(temp_aps);
            for (var i = 0; i < s.attachPointMapping.Count; i++)
            {
                var apMap = s.attachPointMapping[i];
                // Ok, devious lick time - I was thinking of adding in an offset quat for each attachPoint that would
                // get applied between every check. But, it's a safe assumption that when determining a symmetry to use, 
                // nothing will have been attached yet, why not simply spin the actual transforms too?
                
                // set attachPoint at index to the one from the mapping
                attachPoints[apMap.attachPointIndex] = temp_aps[i];
                // spin the attachPoint
                var fromQ = attachPoints[apMap.attachPointIndex].attachTransform.localRotation;
                var toQ = fromQ * Quaternion.Inverse(currentSymmetry.attachPointMapping[i].rotationalOffset) *
                    apMap.rotationalOffset;
                attachPoints[apMap.attachPointIndex].attachTransform.localRotation = toQ;
                // QUESTION: because of symmetry, repeatedly spinning the attachPoints should be indistinguishable from
                // resetting them. In practice, however, there might be issues with float arithmetic.
                // I don't anticipate this to be a big issue, granted that probably objects should only set their
                // symmetry on a correct placement, and we aren't going to allow removal thereafter. If you see building
                // errors on object orientations, re-evaluate whether these assumptions are valid. Might need to set up
                // initial rotational offsets in prefab then revert to those and re-mult the symmetry offsets.
            }
        }


        //This doesn't work as I'd hoped...
        //void PhysicsUpdate()
        //{
        //    foreach (var ap in attachPoints)
        //    {
        //        ap.attachedAttachPoint?.owningPiece.AlignToOther(ap.attachedAttachPoint,ap);
        //    }
        //}
    }

    [Serializable]
    public class Symmetry
    {
        public Vector3 eulerRotOffCanonical;
        public Quaternion rotationalOffsetFromCanonical => Quaternion.Euler(eulerRotOffCanonical);
        public List<AttachPointMap> attachPointMapping;
    }

    [Serializable]
    public class AttachPointMap
    {
        public int attachPointIndex;
        public Vector3 eulerOffset;
        public Quaternion rotationalOffset => Quaternion.Euler(eulerOffset);
    }
    

}