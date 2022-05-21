using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PlayStructure;
using UnityEngine;
using Valve.VR;

namespace PlayStructure
{
    public class BuildStructure : Singleton<BuildStructure>
    {
        public enum AttachMode
        {
            ConfigurableJoint,
            Hierarchical
        }
        public AttachMode attachMode = AttachMode.ConfigurableJoint;

        private Valve.VR.InteractionSystem.Hand[] _hands = null;
        public Valve.VR.InteractionSystem.Hand[] hands =>
            _hands ??= FindObjectsOfType<Valve.VR.InteractionSystem.Hand>();
        
        [HideInInspector]
        public ScriptableInstructions instructionSet;
        [HideInInspector]
        public List<InstructionStep> buildInstructions;
        //{
            //get => _mBuildInstructions ??= new List<InstructionStep>();
            //set => _mBuildInstructions = value;
        //}

        //private List<InstructionStep> _mBuildInstructions;
        
        public float editorSnapAttachDistance = 0.1f;
        
        //TODO: use this to clean up calls to check for when attachments have changed.
        public bool currentAttachmentsChanged = false;

        private List<BuildingPiece> _mBuildingPieces;
        public List<BuildingPiece> buildingPieces
        {
            get
            {
                //remove this and uncomment the null-coalesce if this exhibits issues on startup.
                _mBuildingPieces ??= FindObjectsOfType<BuildingPiece>().ToList();
                //_mBuildingPieces ??= new List<BuildingPiece>();
                //quick way to drop disabled and null building pieces
                _mBuildingPieces = _mBuildingPieces.Where(p=>p!=null && p.enabled).ToList();
                return _mBuildingPieces;
            }
        }

        public int AddBuildingPiece(BuildingPiece piece)
        {
            if (!buildingPieces.Contains(piece))
                buildingPieces.Add(piece);
            return buildingPieces.IndexOf(piece);
        }

        public void RemoveBuildingPiece(BuildingPiece piece)
        {
            // Removed because buggy.
            /*
            if (buildingPieces.Contains(piece))
            {
                buildingPieces.Remove(piece);
            }
            for (int i = 0; i < buildingPieces.Count; i++)
            {
                Debug.Log($"[{i}]:{buildingPieces[i].transform.name}");
                buildingPieces[i].buildingPieceID = i;
            }
            */
                
        }

        public bool GetClosestAttachPoint(Vector3 position, out AttachPoint attachPoint,
            AttachPoint.AttachState attachState=AttachPoint.AttachState.FREE,
            bool pipesOnly = false)
        {
            if (buildingPieces == null || buildingPieces.Count == 0)
            {
                attachPoint = null;
                return false;
            }
            var closestAP = buildingPieces
                .SelectMany(p => p.attachPoints)
                .Where(a=>a.currentAttachState==attachState)
                .Where(a=>!pipesOnly || a.owningPiece.isPipe)
                //.Select(a=>a.attachTransform)
                .OrderBy(a => Vector3.Distance(a.attachTransform.position, position))
                .DefaultIfEmpty().First();
            if (closestAP!=null && Vector3.Distance(closestAP.attachTransform.position, position) <= editorSnapAttachDistance)
            {
                attachPoint = closestAP;
                return true;
            }
            attachPoint = null;
            return false;
        }

        public bool GetClosestAttachPoint(AttachPoint fromAP, out AttachPoint attachPoint) => 
            GetClosestAttachPoint(fromAP, out attachPoint, new List<BuildingPiece>());
        public bool GetClosestAttachPoint(AttachPoint fromAP, out AttachPoint attachPoint, BuildingPiece ignorePiece) =>
            GetClosestAttachPoint(fromAP, out attachPoint, new List<BuildingPiece> { ignorePiece });
        public bool GetClosestAttachPoint(AttachPoint fromAP, out AttachPoint attachPoint, List<BuildingPiece> ignorePieces)
        {
            if (buildingPieces == null || buildingPieces.Count == 0)
            {
                attachPoint = fromAP;
                return false;
            }

            bool lookForConnectors = fromAP.owningPiece.isPipe;
            bool lookForPipes = fromAP.owningPiece.isConnector;
            var closestAP = buildingPieces
                .Where(p => !ignorePieces.Contains(p))
                .Where(p => (lookForConnectors && p.isConnector) || (lookForPipes && p.isPipe))
                .SelectMany(p => p.attachPoints)
                .Where(a=>a.isFree)
                //.Select(a=>a.attachTransform)
                .OrderBy(a => Vector3.Distance(a.attachTransform.position, fromAP.attachTransform.position))
                .FirstOrDefault();
            if (closestAP == null)
            {
                attachPoint = fromAP;
                return false;
            }
            if (Vector3.Distance(closestAP.attachTransform.position, fromAP.attachTransform.position) <= editorSnapAttachDistance)
            {
                attachPoint = closestAP;
                return true;
            }
            attachPoint = fromAP;
            return false;
        }

        public List<Transform> GetAllAttachPoints => buildingPieces.SelectMany(p => p.attachPointTransforms).ToList();

        // First, add the BuildingPieces that are in the scene that we don't have in buildingPieces
        // Then drop any values from buildingPieces that are null or not in the scene
        // Finally, set the buildingPiece IDs
        public void RefreshBuildingPieces()
        {
            // Question: change this to use GUIDs?
            // Pro: guaranteed that each ID is unique, won't change from other BPs getting added/removed
            // Con: more difficult to read, IDs aren't guaranteed to persist between editor and build
            /*
            var piecesInScene = FindObjectsOfType(typeof(BuildingPiece)).Cast<BuildingPiece>().OrderBy(bp=>bp.buildingPieceID);
            foreach (var bp in piecesInScene)
            {
                if (!buildingPieces.Contains(bp))
                {
                    bp.buildingPieceID = AddBuildingPiece(bp);
                }
            }
            foreach (var bp in buildingPieces)
            {
                // Remove from buildingPieces if 
                if (bp == null || !piecesInScene.Contains(bp))
                {
                    buildingPieces.Remove(bp);
                }
            }
            for (var i = 0; i < buildingPieces.Count; i++)
            {
                buildingPieces[i].buildingPieceID = i;
            }
            

            Debug.Log($"Refreshed building pieces - have {buildingPieces.Count} pieces.");
            */
        }


        public static Quaternion GetBestTargetRotation(AttachPoint fromAttach, AttachPoint toAttach, Quaternion fromAttach_rotation)
            => GetBestSymmetry(fromAttach, toAttach, fromAttach_rotation).rotationalOffsetFromCanonical;
        public static Symmetry GetBestSymmetry(AttachPoint fromAttach, AttachPoint toAttach,
            Quaternion fromAttach_rotation)
        {
            var baseTargetRotation =
                fromAttach.attachTransform.rotation * fromAttach_rotation // toAttach AP target
                * Quaternion.Inverse(toAttach.attachTransform.localRotation); // go from toAttach AP to buildingPiece
            var currentRotation = toAttach.owningPiece.transform.rotation;

            return toAttach.owningPiece.symmetries
                .OrderBy(s => 
                    Quaternion.Angle(currentRotation, s.rotationalOffsetFromCanonical* baseTargetRotation))
                .First();
        }
    }


}