using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace PlayStructure
{
    [CustomEditor(typeof(BuildStructure))]
    public class BuildStructureEditor : Editor
    {

        private ReorderableList buildInstructionList;

        //static (BuildingPiece, BuildingPiece)? buildStepPieces = null;
        private static InstructionStep buildStepPieces = null;

        public void OnEnable()
        {
            var buildStructure = (BuildStructure)target;
            buildStepPieces = null;
            buildInstructionList = new ReorderableList(serializedObject,
                serializedObject.FindProperty("buildInstructions"),
                true,true,false,false);
            buildInstructionList.drawElementCallback += (rect, index, active, focused) =>
                {
                    var element = buildInstructionList.serializedProperty.GetArrayElementAtIndex(index);
                    EditorGUI.ObjectField(new Rect(rect.x, rect.y, 120, EditorGUIUtility.singleLineHeight),
                        element.FindPropertyRelative("toAttach"), typeof(BuildingPiece),GUIContent.none);
                    if (GUI.Button(new Rect(rect.x + 120, rect.y, 50, EditorGUIUtility.singleLineHeight), "swap"))
                    {
                        buildStructure.buildInstructions[index].ReverseInPlace();
                        EditorUtility.SetDirty(target);
                        buildInstructionList.onSelectCallback(buildInstructionList);
                    }
                    EditorGUI.ObjectField(new Rect(rect.x+170, rect.y, 120, EditorGUIUtility.singleLineHeight),
                        element.FindPropertyRelative("fromAttach"),typeof(BuildingPiece),GUIContent.none);
                    EditorGUI.LabelField(new Rect(rect.x+290,rect.y,50,EditorGUIUtility.singleLineHeight),$"{index}");
                };
            buildInstructionList.onSelectCallback += l =>
            {
                var fromAttach = l.serializedProperty.GetArrayElementAtIndex(l.index).FindPropertyRelative("fromAttach") ;
                var toAttach = l.serializedProperty.GetArrayElementAtIndex(l.index).FindPropertyRelative("toAttach");

                //buildStepPieces = ((BuildingPiece)fromAttach.objectReferenceValue,
                    //(BuildingPiece)toAttach.objectReferenceValue);
                buildStepPieces = buildStructure.buildInstructions[l.index];
                EditorUtility.SetDirty(target);
                EditorGUIUtility.PingObject((BuildingPiece)toAttach.objectReferenceValue);

            };

            // Refresh BuildingPieces on delete/add.
            EditorApplication.hierarchyChanged += () =>
            {
                (target as BuildStructure)?.RefreshBuildingPieces();
            };
            EditorApplication.projectChanged += () =>
            {
                (target as BuildStructure)?.RefreshBuildingPieces();
            };

        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var buildStructure = (BuildStructure)target;
            
            
            buildStructure.instructionSet = (ScriptableInstructions)EditorGUILayout.ObjectField(
                buildStructure.instructionSet,typeof(ScriptableInstructions),false);

            if (buildStructure.instructionSet != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Save to disk"))
                {
                    buildStructure.instructionSet.instructions.Clear();
                    foreach (var bi in buildStructure.buildInstructions)
                    {
                        buildStructure.instructionSet.instructions.Add(new InstructionStepToSave(bi,
                            bi.fromAttach.buildingPieceID, bi.toAttach.buildingPieceID));
                    }
                    EditorUtility.SetDirty(buildStructure.instructionSet);
                    Debug.Log($"Saved to {buildStructure.instructionSet.name}");
                }

                if (GUILayout.Button("Load to scene"))
                {
                    throw new NotImplementedException("Haven't set up loading yet, sorry!");
                }
                EditorGUILayout.EndHorizontal();
            }
            
            if (GUILayout.Button("Refresh building objects list"))
            {
                Undo.RecordObject(buildStructure,"Refresh BuildingPieces");
                foreach (var piece in buildStructure.buildingPieces)
                {
                    PrefabUtility.RevertPrefabInstance(piece.gameObject, InteractionMode.UserAction);
                }
                buildStructure.RefreshBuildingPieces();
                //foreach (var b in buildStructure.buildingPieces)
                //{
                    //b.RefreshAttachPoints();
                //}
                //buildStructure.buildInstructions = new List<InstructionStep>();
            }



            if (GUILayout.Button("Check for updated build steps") || buildStructure.currentAttachmentsChanged)
            {
                var attachedPoints = buildStructure.buildingPieces
                    .SelectMany(bp => bp.attachPoints)
                    .Where(ap => ap.attachedAttachPoint != null);

                // Remove the no-longer-valid build steps
                buildStructure.buildInstructions =
                    buildStructure.buildInstructions.Where(step =>
                            attachedPoints.Any(ap =>
                                ap.owningPiece == step.fromAttach &&
                                ap.attachedAttachPoint.owningPiece == step.toAttach))
                        .ToList();

                // Add new build steps that are not yet represented.
                foreach (var ap in attachedPoints)
                {
                    if (!buildStructure.buildInstructions.Any(bi => (bi.fromAttach == ap.owningPiece && bi.toAttach == ap.attachedAttachPoint.owningPiece)
                                                                    || (bi.toAttach == ap.owningPiece && bi.fromAttach == ap.attachedAttachPoint.owningPiece)))
                    {
                        buildStructure.buildInstructions.Add(new InstructionStep
                        {
                            fromAttach = ap.owningPiece, fromAttach_point = ap, fromAttach_rotation = ap.thisToOther,
                            toAttach = ap.attachedAttachPoint.owningPiece, toAttach_point = ap.attachedAttachPoint,
                            toAttach_rotation = ap.attachedAttachPoint.thisToOther
                        });
                    }
                }
                EditorUtility.SetDirty(buildStructure);

            }

            serializedObject.Update();
            buildInstructionList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();

        }

        void OnSceneGUI()
        {

            var buildStructure = (BuildStructure)target;
            for (var i = 0; i < buildStructure.buildInstructions.Count; i++)
            {
                var bi = ((BuildStructure)target).buildInstructions[i];
                var fromPos = bi.fromAttach.transform.position;
                var toPos = bi.toAttach.transform.position;
                var midpoint = Vector3.Lerp(fromPos, toPos, 0.5f);
                Handles.color = Color.gray;
                Handles.DrawLine( fromPos, toPos, Handles.lineThickness*1.5f);
                //// It might be nice to be able to select items in the reorderable list from handles in scene view
                //// UI doesn't seem to want to update appropriately, however...
                //if (Handles.Button(midpoint,Quaternion.identity,0.005f,0.005f,Handles.DotHandleCap))
                //{
                //    buildInstructionList.index = i;
                //    EditorUtility.SetDirty(target);
                //}

                var style = new GUIStyle();
                style.normal.textColor = Color.magenta;
                Handles.Label(midpoint, $"{i}", style);
                Handles.color = Color.white;
            }
            if (buildStepPieces != null)
            {
                Handles.color = Color.magenta;
                //TODO: make this an arrow that shows attach direction from ToAttach to FromAttach.
                //var fromPoint = buildStepPieces.Value.Item1.transform.position;
                //var toPoint = buildStepPieces.Value.Item2.transform.position;
                var fromPoint = buildStepPieces.fromAttach.transform.position;
                var toPoint = buildStepPieces.toAttach.transform.position;
                var midpoint = Vector3.Lerp(fromPoint, toPoint, 0.5f);
                var direction = fromPoint - toPoint;
                Handles.DrawLine(toPoint, midpoint,
                    Handles.lineThickness*1.5f); //draw a little thicc
                Handles.ConeHandleCap(0,midpoint,Quaternion.LookRotation(direction),.015f,EventType.Repaint);
                Handles.color = Color.white;
                //buildStepPieces.Value.Item2

                // Clear buildStepLineToDraw if it's been long enough.
                // (inelegant, but handles hiding this even without a deselect callback available to use)
                //if (startShowBuildStepLineTime + editorTimeToShowBuildStepLine < Time.time)
                //{
                //buildStepLineToDraw = null;
                //}
            }
            
        }
        
    }
    
}