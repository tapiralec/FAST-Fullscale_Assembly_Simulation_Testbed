using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.TerrainAPI;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace PlayStructure
{
    [CustomEditor(typeof(BuildingPiece))]
    [CanEditMultipleObjects]
    public class BuildingPieceEditor : Editor
    {
        private BuildingPiece _t;

        private float rotateHandleDist = 0.05f;
        private float attachHandleSize = 0.005f;

        void OnEnable()
        {
            _t = (BuildingPiece) target;
            
            // Avoid adding ourselves to BuildStructure when we're looking at the prefab itself (not an instance)
            if (_t.gameObject.scene != SceneManager.GetActiveScene()) return;
            
            BuildStructure.instance.AddBuildingPiece(_t);
            // force ID to get set.
            Undo.RecordObject(_t, "Set buildingPieceID");
            _ = _t.buildingPieceID;
        }
        
        void OnSceneGUI()
        {
            // Don't bother with these widgets if we're just looking at the prefab:
            if (_t.gameObject.scene != SceneManager.GetActiveScene()) return;
            
            foreach (var attachPoint in _t.attachPoints)
            {
                if (attachPoint.attachedAttachPoint == null && !attachPoint.isFree)
                {
                    Debug.LogError($"no attached point to {_t.transform.name}, but also not free");
                }
                // Draw a line to attached ap if there's one
                if (attachPoint.attachedAttachPoint != null)
                {
                    
                    Handles.color = Color.grey;
                    Handles.DrawLine(attachPoint.owningPiece.transform.position,
                        attachPoint.attachedAttachPoint.owningPiece.transform.position,
                        Handles.lineThickness*1.5f); //draw a little thicc
                    Handles.color = Color.white;
                    
                    
                    // detach if attachPoints are sufficiently far or get rotated.
                    bool farFromAttachedPoint =
                        Vector3.Distance(attachPoint.attachTransform.position,
                        attachPoint.attachedAttachPoint.attachTransform.position) > 0.01 ||
                        Quaternion.Angle(attachPoint.attachTransform.rotation,
                            attachPoint.attachedAttachPoint.attachTransform.rotation * attachPoint.attachedAttachPoint.thisToOther)>1f;
                    if (farFromAttachedPoint) EditorBreakLink(attachPoint);
                    
                // Do 90deg rotation handles
                Handles.color = Color.Lerp(Color.yellow, Color.red, 0.5f);
                Handles.DrawWireArc(attachPoint.attachTransform.position, attachPoint.attachTransform.forward, attachPoint.attachTransform.right, 90f, rotateHandleDist);
                Handles.ConeHandleCap(0, attachPoint.attachTransform.position + attachPoint.attachTransform.rotation * Vector3.up * rotateHandleDist,
                    attachPoint.attachTransform.rotation * Quaternion.AngleAxis(-90f, Vector3.up),
                    attachHandleSize*3, EventType.Repaint);
                if (Handles.Button(attachPoint.attachTransform.position
                                   + attachPoint.attachTransform.rotation * ((Vector3.up+Vector3.right)*rotateHandleDist*0.7071f),
                    Quaternion.identity, attachHandleSize, attachHandleSize, Handles.DotHandleCap))
                {

                    // Question: is this acting poorly? maybe don't detach/reattach...
                    var otherAP = attachPoint.attachedAttachPoint;
                    var tempRot = _t.transform.rotation;
                    EditorBreakLink(attachPoint);
                    Undo.RecordObject(_t.transform, "rotate around attachPoint");
                    _t.transform.RotateAround(attachPoint.attachTransform.position, attachPoint.attachTransform.forward, 90f);
                    if (_t.transform.rotation == tempRot) Debug.LogWarning("didn't actually rotate");
                    EditorMakeLink(attachPoint,otherAP);

                }
                    
                }
                // If not attached, let's check for things we can attach to...
                if (attachPoint.attachedAttachPoint == null)
                {
                    bool closeToOtherAP =
                        BuildStructure.instance.GetClosestAttachPoint(attachPoint, out AttachPoint otherAP, _t);
                    //EditorGUI.BeginChangeCheck();
                    //Vector3 newPosition = Handles.PositionHandle(attachPoint.position, attachPoint.rotation);
                    // check if this is close to another attachPoint
                    Handles.color = Color.magenta;
                    float buttonSizeToUse = closeToOtherAP ? attachHandleSize : 0f;
                    if (Handles.Button(
                        Vector3.Lerp(attachPoint.attachTransform.position, otherAP.attachTransform.position, 0.5f),
                        Quaternion.identity, buttonSizeToUse, buttonSizeToUse, Handles.DotHandleCap))
                    {
                        EditorMakeLink(attachPoint,otherAP);
                    }

                    // indicate that we're close to other attachPoint with a line
                    Handles.DrawLine(attachPoint.attachTransform.position, otherAP.attachTransform.position, 1.5f);
                    Handles.color = Color.white;
                }


                Handles.color = Color.white;

                // TODO: Need to add control and indication that pieces are connected.
            }

        }

        public void EditorMakeLink(AttachPoint attachPoint, AttachPoint otherAP)
        {
            Undo.RecordObject(_t.transform, "change position by attachPoint");
            Undo.RecordObjects(_t.transform.GetComponents<FixedJoint>(), "Snapping");
            Undo.RecordObject(_t, "attach");
            Undo.RecordObject(otherAP.owningPiece, "attach");
            _t.SnapToOther(attachPoint, otherAP);
            PrefabUtility.RecordPrefabInstancePropertyModifications(otherAP.owningPiece);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(_t);
            EditorUtility.SetDirty(otherAP.owningPiece);
            //serializedObject.ApplyModifiedProperties();
            using (var otherBuildingPiece = new UnityEditor.SerializedObject(otherAP.owningPiece))
            {
                otherBuildingPiece.ApplyModifiedProperties();
            }
            BuildStructure.instance.currentAttachmentsChanged = true;
        }

        public void EditorBreakLink(AttachPoint ap)
        {
            if (ap == null) return;
            // Destroy our fixed joint if not null
            //if (ap.fixedJoint != null) DestroyImmediate(ap.fixedJoint);
            if (ap.configurableJoint != null) DestroyImmediate(ap.configurableJoint);
            if (ap.attachedAttachPoint != null)
            {
                // Destroy the attached part's fixed joint if it was on that actually
                //if (ap.attachedAttachPoint.fixedJoint != null) DestroyImmediate(ap.attachedAttachPoint.fixedJoint);
                if (ap.attachedAttachPoint.configurableJoint != null) DestroyImmediate(ap.attachedAttachPoint.configurableJoint);

                // Scan through ours and other's fixed joints to make sure it's good all-around:
                // QUESTION: is this causing noticeable lag? Add ref to rigidbody in BuildingPiece and use that instead
                // of GetComponent for cheaper iterations (might need to get done anyway).
                foreach (var fj in ap.owningPiece.transform.GetComponents<FixedJoint>())
                {
                    if (fj.connectedBody == ap.attachedAttachPoint.owningPiece.GetComponent<Rigidbody>())
                    {
                        DestroyImmediate(fj);
                    }
                }

                foreach (var fj in (ap.attachedAttachPoint.owningPiece.transform.GetComponents<FixedJoint>()))
                {
                    if (fj.connectedBody == ap.owningPiece.GetComponent<Rigidbody>())
                    {
                        DestroyImmediate(fj);
                    }
                }

                // Do code detach
                Undo.RecordObject(_t, "detach");
                Undo.RecordObject(ap.attachedAttachPoint.owningPiece, "detach");
                ap.Detach();
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            BuildStructure.instance.currentAttachmentsChanged = true;

        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            // For each attach point where it's attached to something and we've pressed the "break link" button...
            foreach (var ap in _t.attachPoints
                .Where(ap => ap.attachedAttachPoint != null)
                .Where(ap => GUILayout.Button($"Break link to {ap.attachedAttachPoint.owningPiece.name}")))
            {
                EditorBreakLink(ap);
                
            }


            serializedObject.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);

            if (GUILayout.Button("Refresh links"))
            {
                // Break all links in attach points
                foreach (var ap in _t.attachPoints)
                {
                    EditorBreakLink(ap);
                }
                // DestroyImmediate any straggling FixedJoints
                // QUESTION: is this removing fixedjoints that it shouldn't? might need to do better fj management.
                foreach (var fj in _t.GetComponents<FixedJoint>())
                {
                    DestroyImmediate(fj);
                }

                for (var i = 0; i < _t.attachPoints.Count; i++)
                {
                    PrefabUtility.RevertPrefabInstance(_t.gameObject, InteractionMode.UserAction);
                    //PrefabUtility.RevertPropertyOverride(
                    //serializedObject.FindProperty("attachPoints").GetArrayElementAtIndex(i),
                    //InteractionMode.UserAction);
                }

                //instead of this, can we reset to prefab?
                //_t.RefreshAttachPoints();
                
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
            
        }
    }

}

















