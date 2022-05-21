using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlayStructure
{
    [CustomEditor(typeof(ScrewPiece))]
    [CanEditMultipleObjects]
    public class ScrewPieceEditor : Editor
    {
        [ExecuteInEditMode]
        public void OnEnable()
        {
            var screwPiece = (ScrewPiece)target;
            if (screwPiece.screwIndex != -1) return;
            if (screwPiece.gameObject.scene != SceneManager.GetActiveScene()) return; 
            var highestIndex = FindObjectsOfType<ScrewPiece>().Max(s => s.screwIndex);
            Undo.RecordObject(screwPiece, "Set screwIndex");
            screwPiece.screwIndex = highestIndex + 1;
            
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
        }
    }

}