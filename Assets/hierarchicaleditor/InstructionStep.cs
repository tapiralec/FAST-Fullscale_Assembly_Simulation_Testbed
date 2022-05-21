using System;
using System.Collections;
using System.Collections.Generic;
using PlayStructure;
using UnityEngine;

namespace PlayStructure
{
    //TODO: How to handle simultaneity? e.g. {pipe with two connectors} going onto {group with two open pipes}???
    // Does each InstructionStep need to actually be a collection of attachments instead of just one?
    
    // This class will describe the InstructionStep for the editor - the current state of things
    [Serializable]
    public class InstructionStep 
    {
        public BuildingPiece toAttach;

        // the AttachPoints are internally kept as indices on their respective BuildingPieces
        // this is because the data can get modified/shuffled externally with direct references, oddly.
        [SerializeField, HideInInspector]
        private int _mToAttach_apIndex;
        public int toAttach_apIndex { get => _mToAttach_apIndex; private set => _mToAttach_apIndex = value; }

        public AttachPoint toAttach_point
        {
            get => toAttach.attachPoints[toAttach_apIndex];
            set
            {
                if (value.owningPiece != toAttach)
                {
                    Debug.LogError("tried to set up an instructionStep with " +
                                   $"toAttach={toAttach.name}," +
                                   $"but toAttach_ap.owningPiece={value.owningPiece.name}");
                }
                toAttach_apIndex = toAttach_point.owningPiece.attachPoints.IndexOf(value);  
            } 
        }
        public Quaternion toAttach_rotation;
        public BuildingPiece fromAttach;
        
        [SerializeField, HideInInspector]
        private int _mFromAttach_apIndex;
        public int fromAttach_apIndex { get => _mFromAttach_apIndex; private set => _mFromAttach_apIndex = value; }

        public AttachPoint fromAttach_point
        {
            get => fromAttach.attachPoints[fromAttach_apIndex];
            set => fromAttach_apIndex = fromAttach_point.owningPiece.attachPoints.IndexOf(value);
        }
        public Quaternion fromAttach_rotation;

        public void ReverseInPlace()
        {
            var _fromAttach = toAttach;
            var _fromAttachAPIndex = toAttach_apIndex;
            var _fromAttachQuat = toAttach_rotation;
            toAttach = fromAttach;
            toAttach_apIndex = fromAttach_apIndex;
            toAttach_rotation = _fromAttachQuat;
            fromAttach = _fromAttach;
            fromAttach_apIndex = _fromAttachAPIndex;
            fromAttach_rotation = _fromAttachQuat;
        }
    }

    // This class will be our data class for the objects to save - the way to rebuild things
    [Serializable]
    public class InstructionStepToSave
    {
        public string name;
        public PieceType toAttach;
        public int toAttachPointIndex;
        public Quaternion toAttach_rotation;
        public PieceColor toAttach_color;
        public int toID;
        
        public PieceType fromAttach;
        public int fromAttachPointIndex;
        public Quaternion fromAttach_rotation;
        public int fromID;
        
        public InstructionStepToSave(InstructionStep stepInScene, int fromID = -1, int toID = -1)
        {
            toAttach = stepInScene.toAttach.pieceType;
            toAttachPointIndex = stepInScene.toAttach_apIndex;
            toAttach_rotation = stepInScene.toAttach_rotation;
            toAttach_color = stepInScene.toAttach.pieceColor;
            this.toID = toID;
            
            fromAttach = stepInScene.fromAttach.pieceType;
            fromAttachPointIndex = stepInScene.fromAttach_apIndex;
            fromAttach_rotation = stepInScene.fromAttach_rotation;
            this.fromID = fromID;
            name = $"Attach {toAttach}_{toID}[{toAttachPointIndex}] to {fromAttach}_{fromID}[{fromAttachPointIndex}]";
        }
        


    }

}