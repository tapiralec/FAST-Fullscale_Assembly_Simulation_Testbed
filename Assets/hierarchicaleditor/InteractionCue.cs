using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PlayStructure;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;


public class InteractionCue : MonoBehaviour
{
    public bool overrideNoSymmetryCalculations = true;
    public void DoNoSymmetry(bool value)  => overrideNoSymmetryCalculations = value;

    [SerializeField]
    private GameObject _thingToMove;
    [SerializeField]
    private Transform _whereThingWillGo;

    public float distanceAnimation = float.PositiveInfinity;
    public float dAngleAnimation = float.PositiveInfinity;

    [SerializeField] private bool hintPipesToAny90InsertOffset = true;
    [SerializeField]
    private bool beAnimating = false;
    private bool isLerping;
    private float startTime;
    private HintTypeToShow previousHintType = HintTypeToShow.GRAB;

    [SerializeField] private bool doResetLerpWhenChangingHintShown = true;
    [SerializeField] private bool doSuspendHintAnimationsWhileKeyTurning = true;
    [HideInInspector] public bool suspendHintAnimations = false;
    public void doSuspendHintAnimations(bool value) => suspendHintAnimations = value;
    

    [SerializeField] private bool ShowHighlightingForGrasps = true;
    [SerializeField] private GameObject interactionCueHighlighter;

    private HintTypeToShow _hintTypeToShow = HintTypeToShow.GRAB;
    //What is the type of hint that needs to be shown currently?
    enum HintTypeToShow
    {
        PIECE=0,SCREW=1,KEY=2,GRAB=3
    }

    //What is the current thing that needs to be manipulated?
    private HintTargetType _hintTargetType = HintTargetType.PIECE;

    public enum HintTargetType
    {
        PIECE=0,SCREW=1,KEY=2
    }

    [SerializeField] private float lerpDuration = 2f;
    [SerializeField] private float holdDuration = 0.8f;

    IEnumerator Animate()
    {
        while (beAnimating)
        {
            
            var targetRotation = overrideNoSymmetryCalculations ?
                _whereThingWillGo.rotation :
                FindClosestSymmetry(_thingToMove, _whereThingWillGo);
            //Debug.Log(_whereThingWillGo.rotation);
            //Debug.Log(overrideNoSymmetryCalculations);
            
            if (_thingToMove == null || _whereThingWillGo == null)
            {
                beAnimating = false;
            }
            var time = Time.time;
            // reset
            if (time > startTime + lerpDuration + holdDuration)
            {
                startTime = time;
            }


            var isThingToMoveHeld = _thingToMove == Player.instance.leftHand.currentAttachedObject ||
                                    
                                    _thingToMove == Player.instance.rightHand.currentAttachedObject;
            var isRightHandBusy = Player.instance.rightHand.currentAttachedObject != null;
            var isLeftHandBusy = Player.instance.leftHand.currentAttachedObject != null;
            
            // Don't actually show any hints if the key is turning and we're suspending hints.
            if (doSuspendHintAnimationsWhileKeyTurning && suspendHintAnimations)
            {
                interactionCueHighlighter.SetActive(false);
                hintPieceParent.SetActive(false);
                hintKeyModel.SetActive(false);
                hintScrewModel.SetActive(false);
                hintLeftModel.SetActive(false);
                hintRightModel.SetActive(false);
                startTime = time;
                yield return new WaitForEndOfFrame();
                continue;
            }
            
            _hintTypeToShow = isThingToMoveHeld ? (HintTypeToShow)_hintTargetType : HintTypeToShow.GRAB;
            // Reset lerp to beginning if the type to show has changed (doesn't happen on hand-switch)
            if (doResetLerpWhenChangingHintShown && previousHintType != _hintTypeToShow)
            {
                startTime = time;
            }

            previousHintType = _hintTypeToShow;

            // Set the correct hint models active:
            hintPieceParent.SetActive(_hintTypeToShow==HintTypeToShow.PIECE);
            hintKeyModel.SetActive(_hintTypeToShow==HintTypeToShow.KEY);
            hintScrewModel.SetActive(_hintTypeToShow==HintTypeToShow.SCREW);
            //NB: apparently Player.instance.hands can include sources other than LeftHand,RightHand, hence the manual filter.
            // This fixes issue where left hand was getting the wrong transforms.
            var fromHand = Player.instance.hands.FirstOrDefault();
            // get closest hand if both are busy or neither are busy:
            if (isRightHandBusy && isLeftHandBusy || !isRightHandBusy && !isLeftHandBusy)
            {
                fromHand = Player.instance.hands.Where(
                    h=>h.handType==SteamVR_Input_Sources.LeftHand || h.handType==SteamVR_Input_Sources.RightHand)
                    .OrderBy( h => Vector3.Distance(h.transform.position, _thingToMove.transform.position)).First();
            }
            else
            {
                fromHand = isRightHandBusy ? Player.instance.leftHand : Player.instance.rightHand;
            }

            var usingLeftHandIfUsingHands = fromHand == Player.instance.leftHand;
            
            hintLeftModel.SetActive(_hintTypeToShow==HintTypeToShow.GRAB &&
                                    fromHand.handType == SteamVR_Input_Sources.LeftHand);
            hintRightModel.SetActive(_hintTypeToShow==HintTypeToShow.GRAB &&
                                     fromHand.handType == SteamVR_Input_Sources.RightHand);

            var hintHandOffsetRot = usingControllerGraspHintsRatherThanHands ?
                Quaternion.identity
                : usingLeftHandIfUsingHands
                ? Quaternion.Euler(0f, 0f, -90f)
                : Quaternion.Euler(0f, 0f, 90f);
            var hintHandOffsetPos = usingControllerGraspHintsRatherThanHands ?
                new Vector3(0f, 0.08f, -0.03f)
                : new Vector3(0f, .015f, +0.06f);

            bool animMoving = time <= startTime + lerpDuration;
            var lerp = (time - startTime) / lerpDuration;
            bool animHolding = time > startTime + lerpDuration && time <= startTime + lerpDuration + holdDuration;

            var goalRot = Quaternion.identity;
            var goalPos = Vector3.zero;
            
            interactionCueHighlighter.SetActive(ShowHighlightingForGrasps && _hintTypeToShow==HintTypeToShow.GRAB);
            
            
            switch (_hintTypeToShow){
            case HintTypeToShow.GRAB:
                dAngleAnimation = float.PositiveInfinity;
                distanceAnimation = float.PositiveInfinity;
                if (animMoving)
                {
                    transform.position = Vector3.Lerp(
                        fromHand.transform.position, _thingToMove.transform.position + hintHandOffsetPos, lerp);
                    transform.rotation = Quaternion.Lerp(
                        fromHand.transform.rotation, Quaternion.identity * hintHandOffsetRot, lerp);
                }
                if (animHolding)
                {
                    transform.position = _thingToMove.transform.position + hintHandOffsetPos;
                    transform.rotation = Quaternion.identity * hintHandOffsetRot;
                }
                break;
            case HintTypeToShow.PIECE:
                distanceAnimation = Vector3.Distance(_thingToMove.transform.position, _whereThingWillGo.position);
                dAngleAnimation = Quaternion.Angle(_thingToMove.transform.rotation, targetRotation);
                if (animMoving)
                {
                    transform.position = Vector3.Lerp(
                        _thingToMove.transform.position, _whereThingWillGo.position, lerp);
                    transform.rotation = Quaternion.Lerp(
                        _thingToMove.transform.rotation, targetRotation, lerp);
                }
                if (animHolding)
                {
                    transform.position = _whereThingWillGo.position;
                    transform.rotation = targetRotation;
                }
                break;
            case HintTypeToShow.SCREW:
                // This should probably be set up elsewhere, but as long as I'm manually checking
                // what kind of cue to show, it's easy enough to do this assumption.
                goalPos = _whereThingWillGo.position + _whereThingWillGo.rotation * new Vector3(0.002f, 0f, 0f);
                goalRot = _whereThingWillGo.rotation * Quaternion.Euler(90f, 0f, 180f);
                goalRot = Quaternion.Angle(_thingToMove.transform.rotation, goalRot) <
                          Quaternion.Angle(_thingToMove.transform.rotation, goalRot * Quaternion.Euler(180f, 0f, 0f))
                    ? goalRot
                    : goalRot * Quaternion.Euler(180f, 0f, 0f);
                distanceAnimation = Vector3.Distance(_thingToMove.transform.position, goalPos);
                dAngleAnimation = Quaternion.Angle(_thingToMove.transform.rotation, goalRot);
                if (animMoving)
                {
                    transform.position = Vector3.Lerp(
                        _thingToMove.transform.position, goalPos,lerp);
                    transform.rotation = Quaternion.Lerp(_thingToMove.transform.rotation, goalRot, lerp);
                }

                if (animHolding)
                {
                    transform.position = goalPos;
                    transform.rotation = goalRot;
                }
                break;
            case HintTypeToShow.KEY:
                goalPos = _whereThingWillGo.position + _whereThingWillGo.rotation * new Vector3(0.002f, 0f, 0f) +
                          _whereThingWillGo.rotation * new Vector3(.03f, 0f, 0f);
                goalRot = _whereThingWillGo.rotation * Quaternion.Euler(90f, 0f, 180f) * Quaternion.Euler(0f, 90f, 0f);
                var altGoalRot = goalRot * Quaternion.Euler(0f, 0f, 180f);
                goalRot = Quaternion.Angle(_thingToMove.transform.rotation, goalRot) <
                          Quaternion.Angle(_thingToMove.transform.rotation, altGoalRot)
                    ? goalRot
                    : altGoalRot;
                distanceAnimation = Vector3.Distance(_thingToMove.transform.position, goalPos);
                dAngleAnimation = Quaternion.Angle(_thingToMove.transform.rotation, goalRot);
                if (animMoving)
                {
                    transform.position = Vector3.Lerp(
                        _thingToMove.transform.position, goalPos,lerp);
                    //transform.rotation = Quaternion.Lerp(_thingToMove.transform.rotation, targetRotation, lerp);
                    transform.rotation = Quaternion.Lerp(_thingToMove.transform.rotation, goalRot, lerp);
                }

                if (animHolding)
                {
                    transform.position = goalPos;
                    //transform.rotation = targetRotation;
                    transform.rotation = goalRot;
                }
                break;
                
            }
            yield return new WaitForEndOfFrame();
        }
    }

    public void ShowHint(HintTargetType targetType, GameObject thingToMove, Transform toMoveTo)
    {
        _hintTargetType = targetType;
        _thingToMove = thingToMove;
        interactionCueHighlighter.GetComponent<MeshFilter>().mesh =
            _thingToMove.GetComponentInChildren<MeshFilter>().mesh;
        interactionCueHighlighter.transform.parent = _thingToMove.transform;
        interactionCueHighlighter.transform.localPosition = Vector3.zero;
        interactionCueHighlighter.transform.localRotation = Quaternion.identity;
        
        _whereThingWillGo = toMoveTo;
    }
    
    [Serializable]
    private struct HintBuildingPieceLookup
    {
        public PieceType pieceType;
        public PieceColor pieceColor;
        public GameObject gameObject;
    }

    public GameObject hintLeftModel;
    public GameObject hintRightModel;
    public GameObject hintPieceParent;
    public GameObject hintScrewModel;
    public GameObject hintKeyModel;

    [SerializeField] private List<HintBuildingPieceLookup> hintBuildingPieceLookup;

    // Should be able to pass in a null to disable them all now.
    public void EnableHintByTypeAndColor(PieceType? pieceType, PieceColor? pieceColor)
    {
        Debug.Log($"Enabling by type({pieceType}) and color({pieceColor})");
        // if no pieceColor provided, only activate the first one that matches by piecetype
        if (pieceColor == null && pieceType != null)
        {
            foreach (var plu in hintBuildingPieceLookup)
            {
                plu.gameObject.SetActive(false);
            }
            hintBuildingPieceLookup.FirstOrDefault(plu => plu.pieceType == pieceType).gameObject.SetActive(true);
        }
        // if both or neither are provided, then just SetActive based on matching.
        else
        {
            foreach (var plu in hintBuildingPieceLookup)
            {
                plu.gameObject.SetActive(false);
            }
            foreach (var plu in hintBuildingPieceLookup)
            {
                if (plu.pieceType == pieceType && plu.pieceColor == pieceColor)
                {
                    plu.gameObject.SetActive(true);
                }
            }
        }
    }

    public bool usingControllerGraspHintsRatherThanHands = true;

    public void ShowCue(BuildingPiece piece, Transform target)
    {
        EnableHintByTypeAndColor(piece.pieceType, piece.pieceColor);
        _thingToMove = piece.gameObject;
        _whereThingWillGo = target;
        StartCoroutine(Animate());

    }


    public void UpdateInteractionCue(GameObject piece, Transform place)
    {
        //throw new NotImplementedException();
        
        // Depending on the style of interaction cue, do one of several things:
        
        // Check if pieceToPlace is currently held or not - if not, then we need a cue for the user
        // to pick up the object
        // if it is being held, then we need a cue for the user to move the object to the wherePieceWillGo
        //  in the event of doing an animated transparent style hint, want to make sure that we check
        //  among valid symmetrical placements and select the one with the smallest angle-between for the animation

        // Doublecheck if this will still be needed, I think I'm planning on having the target rotation
        // get updated on the outside by updating a transform and that should reduce the complexity, I think.
        _thingToMove = piece;
        _whereThingWillGo = place;
        //var targetQuat = FindClosestSymmetry(_thingToMove, _whereThingWillGo);
    }

    public Quaternion FindClosestSymmetry(GameObject obj, Transform targetTransform)
    {
        if (obj.GetComponent<BuildingPiece>() != null)
        {
            var closestS = FindClosestSymmetry(obj.GetComponent<BuildingPiece>(), targetTransform);
            return targetTransform.rotation * closestS.rotationalOffsetFromCanonical;
        }
        //if necessary, handle other classes that provide symmetries.
        //Ideally refactor out symmetries to an interface, but do only if there's enough extra time.

        //Debug.LogWarning("Couldn't use a component that provides symmetry so defaulting to identity interaction cues");
        return Quaternion.identity;
    }
    
    public Symmetry FindClosestSymmetry(BuildingPiece piece, Transform targetTransform)
    {
        //Hack: instead of checking if a piece is a tube and allowing for 90 degree rotations if so, we should really
        // be checking the mAlignSymmetry of the AttachPoint that this is going onto...
        
        
        var bestOffset = Mathf.Infinity;
        var bestMatchingSymmetry = Quaternion.identity;
        var bestSymmetryIndex = 0;
        for (var i = 0; i < piece.symmetries.Count; i++)
        {
            var s = piece.symmetries[i];
            if (piece.isPipe && hintPipesToAny90InsertOffset)
            {
                foreach (var s2 in new []
                {
                    Quaternion.identity, Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(0f, 0f, 180f), Quaternion.Euler(0f, 0, 270f)
                })
                {
                    var offset = Quaternion.Angle( targetTransform.rotation * s2 *  s.rotationalOffsetFromCanonical,
                        piece.transform.rotation);
                    if (!(offset < bestOffset)) continue;
                    bestOffset = offset;
                    bestSymmetryIndex = i;

                }
            }
            else
            {
                var offset = Quaternion.Angle(targetTransform.rotation * s.rotationalOffsetFromCanonical,
                    piece.transform.rotation);
                if (!(offset < bestOffset)) continue;
                bestOffset = offset;
                bestMatchingSymmetry = s.rotationalOffsetFromCanonical;
                bestSymmetryIndex = i;
            }
        }

        //Let's try rotating the indices based on the best symmetry index.
        //piece.RotateIndices(bestSymmetryIndex);
        //return targetTransform.rotation * bestMatchingSymmetry;
        return piece.symmetries[bestSymmetryIndex];
    }
    
    
    // Start is called before the first frame update
    void Start()
    {
        if (_thingToMove == null)
        {
            interactionCueHighlighter.SetActive(false);
        }
        else
        {
            EnableHintByTypeAndColor(PieceType.C_TEE,PieceColor.BLACK);
            interactionCueHighlighter.GetComponent<MeshFilter>().mesh =
                _thingToMove.GetComponentInChildren<MeshFilter>().mesh;
            interactionCueHighlighter.transform.parent = _thingToMove.transform;
            interactionCueHighlighter.transform.localPosition = Vector3.zero;
            interactionCueHighlighter.transform.localRotation = Quaternion.identity;
        }

        StartCoroutine(Animate());

    }

    // Update is called once per frame
    void Update()
    {
        // if we have 2 hands and neither are holding the object:
        //showGrabInsteadOfAttach = Player.instance.hands.Length > 1 &&
                                  //Player.instance.hands[0].currentAttachedObject != pieceToPlace &&
                                  //Player.instance.hands[1].currentAttachedObject != pieceToPlace;
        
    }
    
    
}
