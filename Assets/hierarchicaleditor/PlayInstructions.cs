using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PlayStructure;
using UnityEngine;
using UnityEngine.Events;
using Valve.VR.InteractionSystem;

public class PlayInstructions : Singleton<PlayInstructions>
{
    [SerializeField] private bool beginOnStart = true;
    [SerializeField] private bool overrideNoSymmetryCalculations = false;
    [SerializeField]
    private InteractionCue _interactionCue;
    public InteractionCue interactionCue => _interactionCue;

    [SerializeField] private SubstructurePositioningHint substructurePositioningHint;
    public SubstructurePositioningHint getPosHint => substructurePositioningHint;

    [SerializeField] private bool allowPipesToAny90InsertOffset = true;
    [SerializeField]
    private ScriptableInstructions _instructions;

    private int _instructionCounter = 0;

    private Dictionary<int, BuildingPiece> _mBuildingPieces = null;

    private Dictionary<int, BuildingPiece> buildingPieces
    {
        get
        {
            _mBuildingPieces = _mBuildingPieces ??
                               FindObjectsOfType<BuildingPiece>().ToDictionary(b => b.buildingPieceID);
            return _mBuildingPieces;
        }
    }

    private Dictionary<int, ScrewPiece> _mScrewPieces = null;

    private Dictionary<int, ScrewPiece> screwPieces
    {
        get
        {
            if (_mScrewPieces != null) return _mScrewPieces;
            var screws = FindObjectsOfType<ScrewPiece>().OrderBy(s => s.screwIndex).ToList();
            foreach (var s in screws.Where(s => s.screwIndex == -1))
            {
                Debug.LogError("index of this screwPiece was -1", s);
            }
            _mScrewPieces = new Dictionary<int, ScrewPiece>();
            for (var i = 0; i < screws.Count; i++)
            {
                _mScrewPieces.Add(i, screws[i]);
            }

            return _mScrewPieces;
        }
    }


    // going to have InteractionCue automatically do the held-item checks for toggling between
    // cue to grab object and cue to manipulate object
    public enum InstructionSubStepState
    {
        AttachPiece,
        InsertScrew,
        UseKey
    }
    public InstructionSubStepState currentSubStepState = InstructionSubStepState.AttachPiece;

    public UnityEvent<int, InstructionSubStepState> subStepProgress;
    
    [SerializeField] private AudioSource instructionAudioSource;
    [SerializeField] private AudioClip connectClip;
    [SerializeField] private bool playOnPipeConnect = true;
    [SerializeField] private bool playOnScrewConnect = true;
    [SerializeField] private bool playOnScrewTurn = true;
    [SerializeField] private AudioClip completeClip;
    public UnityEvent onCompletedInstructions;

    private Screwdriver _mKey;
    private Screwdriver key
    {
        get
        {
            _mKey ??= FindObjectOfType<Screwdriver>();
            return _mKey;
        }
    }

    void Start()
    {
        
        Teleport.instance?.CancelTeleportHint();

        if (beginOnStart) BeginInstructions();

    }

    public void BeginInstructions()
    {
        // Make sure we have all the parts available:
        for (var i = 0; i < _instructions.instructions.Count; i++)
        {
            var s = _instructions.instructions[i];
            try
            {
                if (!(buildingPieces.Keys.Contains(s.fromID) && buildingPieces.Keys.Contains(s.toID)))
                {
                    Debug.LogError($"Missing a piece for step {i}:{s.name}");
                }
                else
                {
                    if (buildingPieces[s.fromID].pieceType != s.fromAttach)
                    {
                        Debug.LogError(
                            $"Expected piece {s.fromID} to be a {s.fromAttach}, not a {buildingPieces[s.fromID]}");
                    }
                    if (buildingPieces[s.toID].pieceType != s.toAttach)
                    {
                        Debug.LogError(
                            $"Expected piece {s.toID} to be a {s.toAttach}, not a {buildingPieces[s.toID]}");
                    }
                }
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"Two BuildingPieces have the same ID: {e}");
            }

        }
        ExpandSavedInstruction(0);
        subStepProgress?.Invoke(0,currentSubStepState);
        if (substructurePositioningHint != null) substructurePositioningHint.gameObject.SetActive(true);

        foreach (var key in buildingPieces.Keys)
        {
            
        }

        if (screwPieces.Count < _instructions.instructions.Count)
        {
            Debug.LogError( $"Not enough screws ({screwPieces.Count}) for the number of instructions "+
                $"({_instructions.instructions.Count})!");
        }
        //var firstStep = _instructions.instructions[0];
        //_interactionCue.ShowCue(firstStep.toAttach);
        
    }

    void Update()
    {
        
    }

    private bool TryTransitState(InstructionSubStepState toState)
    {
        var currentInstruction = _instructions.instructions[_instructionCounter];
        var toAttach = buildingPieces[currentInstruction.toID];
        var fromAttach = buildingPieces[currentInstruction.fromID];
        var thisPipe = toAttach.isPipe ? toAttach : fromAttach;
        var apIndex = toAttach.isPipe ? currentInstruction.toAttachPointIndex: currentInstruction.fromAttachPointIndex;
        switch (currentSubStepState)
        {
            case InstructionSubStepState.AttachPiece:
                if (toState != InstructionSubStepState.InsertScrew) return false;
                // update the interaction cue to start looking for a screw ("the" screw?)
                // and give it the attachPoint that it needs to be putting the screw in.
                //var sp = FindObjectOfType<ScrewPiece>();
                var sp = screwPieces[_instructionCounter];
                // Toggle the physics of the screw piece if it has a TogglePhysics script.
                var spPhysToggle = sp.GetComponent<TogglePhysics>();
                if (spPhysToggle != null) spPhysToggle.Toggle(true);
                substructurePositioningHint.UpdateMeshes();
                if (instructionAudioSource!=null && connectClip!=null && playOnPipeConnect) instructionAudioSource.PlayOneShot(connectClip);
                _interactionCue.ShowHint(InteractionCue.HintTargetType.SCREW, sp.gameObject,
                    thisPipe.attachPoints[apIndex].attachTransform);
                currentSubStepState = InstructionSubStepState.InsertScrew;
                subStepProgress?.Invoke(_instructionCounter, currentSubStepState);
                return true;
            case InstructionSubStepState.InsertScrew:
                if (toState != InstructionSubStepState.UseKey) return false;
                // update the interaction cue to start looking for the key
                // and give it the screw that it needs to be used to screw in.
                substructurePositioningHint.UpdateMeshes();
                if (instructionAudioSource!=null && connectClip!=null && playOnScrewConnect) instructionAudioSource.PlayOneShot(connectClip);
                _interactionCue.ShowHint(InteractionCue.HintTargetType.KEY, key.gameObject,
                    thisPipe.attachPoints[apIndex].attachTransform);
                currentSubStepState = InstructionSubStepState.UseKey;
                subStepProgress?.Invoke(_instructionCounter, currentSubStepState);
                return true;
            case InstructionSubStepState.UseKey:
                if (toState != InstructionSubStepState.AttachPiece) return false;
                if (instructionAudioSource!=null && connectClip!=null && playOnScrewTurn) instructionAudioSource.PlayOneShot(connectClip);
                // go to next instruction step.
                // (unless there are no more steps, then trigger "completion")
                //ExpandSavedInstruction(++_instructionCounter);
                CompletedStep();
                currentSubStepState = InstructionSubStepState.AttachPiece;
                subStepProgress?.Invoke(_instructionCounter, currentSubStepState);
                return true;
        }
        return false;
    }

    // Check if two pieces that have been attached were correct
    public (bool,AttachPoint,Quaternion) CheckAttach(AttachPoint ap, AttachPoint otherAP, Quaternion? potentialThisToOther = null)
    {
        // ignore verification if in edit mode:
        if (!Application.isPlaying) return (true, ap, Quaternion.identity);
        var currentInstruction = _instructions.instructions[_instructionCounter];
        AttachPoint fromAP;
        AttachPoint toAP;
        bool toAPIsThisAP = false;
        if (ap.owningPiece.buildingPieceID == currentInstruction.fromID &&
            otherAP.owningPiece.buildingPieceID == currentInstruction.toID)
        {
            fromAP = ap;
            toAP = otherAP;
        }
        else if (ap.owningPiece.buildingPieceID == currentInstruction.toID &&
                 otherAP.owningPiece.buildingPieceID == currentInstruction.fromID)
        {
            fromAP = otherAP;
            toAP = ap;
            toAPIsThisAP = true;
        }
        else
        {
            Debug.Log($"Attached the wrong pieces ({ap.owningPiece.buildingPieceID},{otherAP.owningPiece.buildingPieceID}), returning false on CheckAttach.");
            return (false, null, Quaternion.identity);// if they aren't the right pieces, return false.
        } 

        // get the closest symmetrical attach based on what we would expect, and always rotate indices on that:
        var toPiece = toAP.owningPiece;
        var sym_offset = Quaternion.identity;
        var thisToOther = potentialThisToOther ?? toAP.attachTransform.rotation * otherAP.attachTransform.rotation;
        var hasBeenFlippedForPipe = false;
        if (!overrideNoSymmetryCalculations)
        {
            // This is a little janky, but it works
            // instead of futzing with rotating attachPoint indices, instead just always snap to the best symmetrical
            // orientation. There's no perceptual difference on the user side.
            // (best practices would probably involve checking first if the symmetric would be good, and *then* snapping
            // to it)
            // (this probably breaks if you accrete parts together in multiple substructures)
            var origIndex = toPiece.attachPoints.IndexOf(toAP);
            var s = _interactionCue.FindClosestSymmetry(toPiece, GetTargetLocation(currentInstruction));
            sym_offset = Quaternion.Inverse(s.rotationalOffsetFromCanonical);
            toPiece.transform.rotation = toPiece.transform.rotation * sym_offset;
            var newIndex = s.attachPointMapping[origIndex].attachPointIndex;
            hasBeenFlippedForPipe = newIndex != origIndex;
            toAP = toPiece.attachPoints[newIndex];
            Debug.Log($"Remapping for symmetry: {origIndex}->{newIndex}, ideal is {currentInstruction.toAttachPointIndex}");
            // thisToOther at this point isn't the physical _this to other_, but specifically the potential, aligned, _this to other_
            ap = toAPIsThisAP ? toAP : fromAP;
            otherAP = toAPIsThisAP ? fromAP : toAP;
            var (potentialBuildingRot, _) = toPiece.GetPotentialAlignment(ap,otherAP);
            thisToOther= Quaternion.Inverse(potentialBuildingRot * ap.attachTransform.localRotation) *
                                   otherAP.attachTransform.rotation;
        }
        // We will need to apply a RotateIndices for a given attach, but we don't need among the old ones
        
        // Check if it's the right attachPoints involved:
        if (toAP.owningPiece.attachPoints[currentInstruction.toAttachPointIndex] != toAP ||
            fromAP.owningPiece.attachPoints[currentInstruction.fromAttachPointIndex] != fromAP)
        {
            Debug.Log("Attached correct pieces, but wrong attachpoints");
            return (false,null, Quaternion.identity);
        }

        // if it's not a pipe, just check if it's correctly oriented:
        if ((!toAP.owningPiece.isPipe || !allowPipesToAny90InsertOffset) &&
            Mathf.Approximately(Quaternion.Angle(thisToOther, currentInstruction.toAttach_rotation), 0f))
        {
            Debug.Log("Seems to be right, starting to check for screws");
            TryTransitState(InstructionSubStepState.InsertScrew);
            return (true,toAPIsThisAP ? toAP : fromAP, Quaternion.identity);
            //return CheckNext(ap);
        }

        if (toAP.owningPiece.isPipe && allowPipesToAny90InsertOffset)
        {
            // if it's a pipe, check 4-way symmetry:
            var offset = new Quaternion();
            var found_offset = false;

            Debug.Log("checking 4-way symmetry because it's a pipe");
            Debug.Log($"Current:{thisToOther.eulerAngles}, Target: {currentInstruction.toAttach_rotation.eulerAngles}");
            foreach (var s in new[]
                     {
                         Quaternion.identity, Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(0f, 0f, 180f),
                         Quaternion.Euler(0f, 0f, 270f)
                     })
            {
                if (!Mathf.Approximately(Quaternion.Angle(thisToOther * s, currentInstruction.toAttach_rotation), 0f))
                    continue;
                Debug.Log("found best symmetry:" + s.eulerAngles);
                Debug.Log("thisToOther:" + thisToOther.eulerAngles);
                offset = s;
                found_offset = true;
                break;
            }

            if (found_offset)
            {
                Debug.Log("Seems to be right, starting to check for screws");
                TryTransitState(InstructionSubStepState.InsertScrew);
                var apFlip = Vector3.Angle(ap.attachTransform.forward, ap.owningPiece.transform.forward) < 90f;
                return (true, ap, apFlip ? offset : Quaternion.Inverse(offset));
                //return CheckNext(ap);
            }
        }

        Debug.Log("Something's wrong with the attach");
        return (false,null, Quaternion.identity);

    }

    public bool CheckScrewInsert(AttachPoint ap)
    {
        var otherAP = ap.attachedAttachPoint;
        var currentInstruction = _instructions.instructions[_instructionCounter];
        AttachPoint fromAP;
        AttachPoint toAP;
        if (ap.owningPiece.buildingPieceID == currentInstruction.fromID &&
            otherAP.owningPiece.buildingPieceID == currentInstruction.toID)
        {
            fromAP = ap;
            toAP = otherAP;
        }
        else if (ap.owningPiece.buildingPieceID == currentInstruction.toID &&
                 otherAP.owningPiece.buildingPieceID == currentInstruction.fromID)
        {
            fromAP = otherAP;
            toAP = ap;
        }
        else
        {
            Debug.Log("Attached the wrong pieces, returning false on CheckScrewInsert.");
            return false;// if they aren't the right pieces, return false.
        } 
        
        // Check if it's the right attachPoints involved:
        // TODO: this is disabled for now because the logic is weird, but with the linear version that prevents
        // additional loose_locks during building, we shouldn't need to do this.
        //if (toAP.owningPiece.attachPoints[currentInstruction.toAttachPointIndex] != toAP ||
            //fromAP.owningPiece.attachPoints[currentInstruction.fromAttachPointIndex] != fromAP)
        //{
            //return false;
        //}
        Debug.Log("Seems like a good screw attach, transiting to UseKey");
        TryTransitState(InstructionSubStepState.UseKey);
        return true;
        
        //correct pieces and attachpoints, now to check that there's a screw in place:
        //if (toAP.currentAttachState==AttachPoint.AttachState.PARTIAL_LOCK)

    }

    public bool CheckKeyUsage(AttachPoint ap)
    {
        if (currentSubStepState != InstructionSubStepState.UseKey) return false;
        var otherAP = ap.attachedAttachPoint;
        if (_instructionCounter >= _instructions.instructions.Count) return true;
        var currentInstruction = _instructions.instructions[_instructionCounter];
        AttachPoint fromAP;
        AttachPoint toAP;
        if (ap.owningPiece.buildingPieceID == currentInstruction.fromID &&
            otherAP.owningPiece.buildingPieceID == currentInstruction.toID)
        {
            fromAP = ap;
            toAP = otherAP;
        }
        else if (ap.owningPiece.buildingPieceID == currentInstruction.toID &&
                 otherAP.owningPiece.buildingPieceID == currentInstruction.fromID)
        {
            fromAP = otherAP;
            toAP = ap;
        }
        else
        {
            Debug.Log("Attempted to use key on wrong pieces, not ok...");
            return false;// if they aren't the right pieces, return false.
        } 
        //TODO: check to make sure it's the right APs and not just the right pieces.
        // OK for now, since linear progression will prevent other screws from being available
        // to screw in.
        Debug.Log("Seems like a good key usage, transiting to next step!");
        TryTransitState(InstructionSubStepState.AttachPiece);
        return true;
    }

    /// <summary>
    /// Call this when a step is completed correctly.
    /// </summary>
    public void CompletedStep()
    {
        _instructionCounter++;
        if (_instructionCounter >= _instructions.instructions.Count)
        {
            Debug.Log("Completed!");
            if (instructionAudioSource!=null && completeClip!=null) instructionAudioSource.PlayOneShot(completeClip);
            onCompletedInstructions?.Invoke();
            _interactionCue.StopAllCoroutines();
            _interactionCue.gameObject.SetActive(false);
            return;
        }
        ExpandSavedInstruction();

    }

    private void ExpandSavedInstruction(int? instructionCounter = null)
    {
        instructionCounter ??= _instructionCounter;
        Debug.Log($"Expanding instruction {instructionCounter}");
        var stepToExpand = _instructions.instructions[instructionCounter.Value];
        
        //TODO: figure out how I want to do this:
        //  can have users go to appropriate bucket and only spawn when they reach in
        //  then the InteractionCue can show that they need to go to the bucket first...
        //  can set the other buckets to be disabled to prevent excessive piece spawning
        //  (can have grasps on wrong buckets get recorded as types of errors)
        //  alternative: can have the piece spawn in the appropriate place upon entering the step, but...
        //  now the other buckets are empty?
        //  Also, ought I to have a piece of paper with an image of the piece contained over the buckets themselves?
        //  (both in VR and IRL)
        
        //Create toAttach piece:

        //var spawnerBucket = BucketManager.instance.GetBucket(stepToExpand.toAttach, stepToExpand.toAttach_color);
        //var toAttach =spawnerBucket.SpawnPiece(stepToExpand.toID);
        Debug.Log($"build structure list count = {BuildStructure.instance.buildingPieces.Count}");
        Debug.Log($"From ID: {stepToExpand.fromID}, To ID: {stepToExpand.toID}");
        var toAttach = BuildStructure.instance.buildingPieces.First(b => b.buildingPieceID == stepToExpand.toID);
        var fromAttach = BuildStructure.instance.buildingPieces.First(b => b.buildingPieceID == stepToExpand.fromID);
        var fromTransform = fromAttach.transform;

        // toggle on the physics of our objects if there's an associated TogglePhysics script.
        var toAttachPhysToggle = toAttach.GetComponent<TogglePhysics>();
        if (toAttachPhysToggle != null) toAttachPhysToggle.Toggle(true);
        var fromAttachPhysToggle = fromAttach.GetComponent<TogglePhysics>();
        if (fromAttachPhysToggle != null) fromAttachPhysToggle.Toggle(true);


        Debug.Log($"fromAttach: {fromAttach.name}[{stepToExpand.fromAttachPointIndex}]", fromAttach);
        Debug.Log($"toAttach: {toAttach.name}[{stepToExpand.toAttachPointIndex}]",toAttach);

        // Need the target location to be based on
        // fromAttach position (in the current structure)
        // + offset of the attachPoint that we will use (based on the presently-attached symmetry), using the ID
        // + toAttach.rotation (or fromAttach.rotation, or inverse of one of those, double check)
        // + inverse of the toAttachPoint, using the ID
        // gets us the canonical orientation and position of where we want it to be
        // but then after we have this, we need the InteractionCue to also be checking the present orientation of the 
        // new piece and updating the orientation of the toAttach lerp.
        // might be handy to have a double transform under the fromTransform, outer for position and canonical
        // orientation(which shouldn't change with symmetry, and an inner for the current symmetry orientation)
        // then lerp to that, and once the correct step has been taken, *then* go ahead and remove these transforms.
        var targetLocationSymmetrical = new GameObject().transform;

        GetTargetLocation(stepToExpand);

        targetLocationSymmetrical.parent = targetLocation.transform;
        targetLocationSymmetrical.localPosition = Vector3.zero;
        targetLocationSymmetrical.localRotation = FindClosestSymmetry(toAttach, targetLocation);

        substructurePositioningHint.UpdateMeshes();
        _interactionCue.overrideNoSymmetryCalculations = overrideNoSymmetryCalculations;
        _interactionCue.ShowHint(InteractionCue.HintTargetType.PIECE, toAttach.gameObject, targetLocation);
        _interactionCue.EnableHintByTypeAndColor(toAttach.pieceType, toAttach.pieceColor);
        _interactionCue.UpdateInteractionCue(toAttach.gameObject, targetLocation);

    }

    private Transform _mTargetLocation;

    private Transform targetLocation
    {
        get
        {
            _mTargetLocation ??= new GameObject().transform;
            return _mTargetLocation;
        }
    } 
    private Transform GetTargetLocation(InstructionStepToSave stepToExpand)
    {
        var fromAttach = buildingPieces[stepToExpand.fromID];
        var toAttach = buildingPieces[stepToExpand.toID];
        targetLocation.parent = fromAttach.transform;
        var fapt = fromAttach.attachPoints[stepToExpand.fromAttachPointIndex].attachTransform;
        var faptpos = fapt.localPosition;
        var faptrot = fapt.localRotation;
        var tapt = toAttach.attachPoints[stepToExpand.toAttachPointIndex].attachTransform;
        var taptpos = tapt.localPosition;
        var taptrot = tapt.localRotation;

        // the goal rotation and position of the toattach attachpoint 
        var taptrotGoal = fapt.rotation * stepToExpand.fromAttach_rotation;
        var taptposGoal = fapt.position;

        //TODO: check these equations
        targetLocation.rotation = taptrotGoal * Quaternion.Inverse(taptrot);
        targetLocation.position = taptposGoal - taptrotGoal * Quaternion.Inverse(taptrot) * taptpos;
            //taptrotGoal * Quaternion.Inverse(taptrot) * taptpos + taptposGoal;
        return targetLocation;




    }
    
    //NB: this shouldn't be too costly, but still really doesn't need to run at 90Hz. Put whatever
    // will keep this updating in a coroutine or something polling at 5Hz or so.
    private Quaternion FindClosestSymmetry(BuildingPiece piece, Transform targetTransform)
    {
        float bestOffset = Mathf.Infinity;
        Quaternion bestMatchingSymmetry = Quaternion.identity;
        foreach (var s in piece.symmetries)
        {
            var offset = Quaternion.Angle(targetTransform.rotation * s.rotationalOffsetFromCanonical,
                piece.transform.rotation);
            if (offset < bestOffset)
            {
                bestOffset = offset;
                bestMatchingSymmetry = s.rotationalOffsetFromCanonical;
            }
        }
        return bestMatchingSymmetry;
    }
    


}
