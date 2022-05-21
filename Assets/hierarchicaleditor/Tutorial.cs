using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{

    public int tutorialIndex;
    public AudioSource source;
    public TextMeshPro textMeshPro;
    public SubstructurePositioningHint substructureHint;
    public SubstructurePositioningHint keyHint;
    public InteractionCue interactionCue;
    public float distThreshold = 0.01f;
    public float angleThreshold = 5f;
    
    [Serializable]
    public class TutorialStep
    {
        public string name;
        public string textToShow = "<No Text>";
        public AudioClip clipToPlay;
        public bool[] constraintsSatisfied = {false};
        public UnityEvent enterStep;
        public TutorialStep(string name)
        {
            this.name = name;
        }

        public TutorialStep(string name, int numConstraints)
        {
            this.name = name;
            constraintsSatisfied = new bool[numConstraints];
            for (var i = 0; i < numConstraints; i++)
            {
                constraintsSatisfied[i] = false;
            }
        }
    }

    public List<TutorialStep> tutorialSteps = new List<TutorialStep>()
    {
        new TutorialStep("isStarting"),
        new TutorialStep("isAtTable"),
        
        new TutorialStep("isTouchingTConn"),
        new TutorialStep("isHoldingTConn"),
        new TutorialStep("isTConnAligned"),
        new TutorialStep("isTConnPlaced",2),

        new TutorialStep("isTouchingBluePipe"),
        new TutorialStep("isHoldingBluePipe"),
        new TutorialStep("isBluePipeAligned"),
        new TutorialStep("isBluePipeAttached"),

        new TutorialStep("isTouchingScrewOne"),
        new TutorialStep("isHoldingScrewOne"),
        new TutorialStep("isScrewOneAligned"),
        new TutorialStep("isScrewOneIn"),

        new TutorialStep("isTouchingKeyFirst"),
        new TutorialStep("isHoldingKeyFirst"),
        new TutorialStep("isScrewOneScrewed"),
        new TutorialStep("isKeyBackOverTableFirst"),
        new TutorialStep("isKeyLetGoFirst",3),

        new TutorialStep("isTouchingYellowPipe"),
        new TutorialStep("isHoldingYellowPipe"),
        new TutorialStep("isYellowPipeAligned"),
        new TutorialStep("isYellowPipeAttached"),

        new TutorialStep("isTouchingScrewTwo"),
        new TutorialStep("isHoldingScrewTwo"),
        new TutorialStep("isScrewTwoAligned"),
        new TutorialStep("isScrewTwoIn"),

        new TutorialStep("isTouchingKeySecond"),
        new TutorialStep("isHoldingKeySecond"),
        new TutorialStep("isScrewTwoScrewed"),
        new TutorialStep("isKeyBackOverTableSecond"),
        new TutorialStep("isKeyLetGoSecond",3),
    };
    
    #region handleConstraints

    public void TConnPlaced() => SatisfyConstraintByName("isTConnPlaced");
    public void TConnPickedUp() => SatisfyConstraintByName("isHoldingTConn");
    
    #endregion handleConstraints
        
        

    public TutorialStep GetByName(string name)
    {
        return GetByName(name, tutorialSteps);
    }

    public static TutorialStep GetByName(string name, IEnumerable<TutorialStep> steps)
    {
        return steps.First(s => s.name == name);
    }

    public void SatisfyConstraintByIndex(int index, int constraintIndex = 0)
    {
        tutorialSteps[index].constraintsSatisfied[constraintIndex] = true;
    }

    public void SatisfyConstraintByName(string name)
    {
        SetConstraintByName(true, name, 0);
    }
    public void SatisfyConstraintByName(string name, int constraintIndex)
    {
        SetConstraintByName(true, name, constraintIndex);
    }

    public void SetConstraintByName(bool toSet, string name)
    {
        SetConstraintByName(toSet, name, 0);
    }
    
    public void SetConstraintByName(bool toSet, string name, int constraintIndex)
    {
        GetByName(name).constraintsSatisfied[constraintIndex] = toSet;
    }

    public void TouchingKey()
    {
        if (new []{"isTouchingKeyFirst","isTouchingKeySecond"}.Contains(tutorialSteps[tutorialIndex].name))
            SatisfyConstraintByIndex(tutorialIndex);
    }
    


    public void isTConnAligned(bool isAligned)
    {
        SetConstraintByName(isAligned, "isTConnAligned");
        SetConstraintByName(isAligned, "isTConnPlaced");
    }

    public void isTConnHeld(bool isHeld)
    {
        SetConstraintByName(isHeld, "isHoldingTConn");
        SetConstraintByName(!isHeld,"isTConnPlaced",1);
    }

    public void isKeyAligned(bool isAligned)
    {
        SetConstraintByName( isAligned && tutorialIndex >= nameIndex("isKeyBackOverTableFirst") && tutorialIndex <= nameIndex("isKeyLetGoFirst"),
            "isKeyBackOverTableFirst");
        SetConstraintByName( isAligned && tutorialIndex >= nameIndex("isKeyBackOverTableFirst") && tutorialIndex <= nameIndex("isKeyLetGoFirst"),
        "isKeyLetGoFirst");
        SetConstraintByName( isAligned && tutorialIndex >= nameIndex("isKeyBackOverTableSecond") && tutorialIndex <= nameIndex("isKeyLetGoSecond"),
         "isKeyBackOverTableSecond");
        SetConstraintByName( isAligned && tutorialIndex >= nameIndex("isKeyBackOverTableSecond") && tutorialIndex <= nameIndex("isKeyLetGoSecond"),
         "isKeyLetGoSecond");
    }

    public void isKeyHeld(bool isHeld)
    {
        SetConstraintByName(isHeld &&
                            tutorialIndex >= nameIndex("isTouchingKeyFirst") &&
                            tutorialIndex <= nameIndex("isHoldingKeyFirst"),
            "isHoldingKeyFirst");
        SetConstraintByName(isHeld &&
                            tutorialIndex >= nameIndex("isTouchingKeySecond") &&
                            tutorialIndex <= nameIndex("isHoldingKeySecond"),
            "isHoldingKeySecond");
        SetConstraintByName(!isHeld,"isKeyLetGoFirst",1);
        SetConstraintByName(!isHeld,"isKeyLetGoSecond",1);
    }

    private int nameIndex(string name)
    {
        return tutorialSteps.FindIndex(s => s.name == name);
    }
    
    void Update()
    {
        UpdateCueBasedConstraints();
        if (source != null && source.isPlaying) return;
        for (var i = tutorialSteps.Count - 1; i >= 0; i--)
        {
            if (tutorialIndex <= i && tutorialSteps[i].constraintsSatisfied.All(c => c))
            {
                DoTutorialInstruction(i);
            }
        }
    }

    void UpdateCueBasedConstraints()
    {
        var isClose = interactionCue.distanceAnimation < distThreshold && interactionCue.dAngleAnimation < angleThreshold;
        SetConstraintByName(isClose && tutorialIndex>=nameIndex("isHoldingBluePipe"), "isBluePipeAligned");
        SetConstraintByName(isClose && tutorialIndex>=nameIndex("isHoldingScrewOne"), "isScrewOneAligned");
        SetConstraintByName(isClose && tutorialIndex>=nameIndex("isHoldingYellowPipe"), "isYellowPipeAligned");
        SetConstraintByName(isClose && tutorialIndex>=nameIndex("isHoldingScrewTwo"), "isScrewTwoAligned");
    }

    void Reset()
    {
        source = GetComponent<AudioSource>();
        textMeshPro = GetComponentInChildren<TextMeshPro>();
        // Make sure the first one is good to go.
        SatisfyConstraintByIndex(0);
        var textInstructions = Resources.Load<TextAsset>("Tutorial/TutorialMonologue");
        for (var i = 0; i < tutorialSteps.Count; i++)
        {
            tutorialSteps[i].clipToPlay = Resources.Load<AudioClip>($"Tutorial/Tutorial-{i + 1:00}");
            tutorialSteps[i].textToShow = textInstructions.text.Split('\n')[i];
        }
    }
    
    private void DoTutorialInstruction(int index)
    {
        if (tutorialSteps.Count <= index) return;
        // Do any skipped over UnityEvents, in order:
        for (var i = tutorialIndex; i < index; i++)
        {
            tutorialSteps[i].enterStep?.Invoke();
        }
        tutorialSteps[index].enterStep?.Invoke();
        source.PlayOneShot(tutorialSteps[index].clipToPlay);
        textMeshPro.SetText(tutorialSteps[index].textToShow);
        Debug.Log($"tutorial {tutorialSteps[index].name}");
        tutorialIndex = index + 1;
    }

    void OnTriggerEnter(Collider c)
    {
        if (c.transform.name == "HeadCollider") SatisfyConstraintByName("isAtTable");
    }
    
    

    public void HandleInstructionStep(int instructionIndex, PlayInstructions.InstructionSubStepState subStepState)
    {
        if (instructionIndex == 0 && subStepState == PlayInstructions.InstructionSubStepState.InsertScrew)
            SatisfyConstraintByName("isBluePipeAttached");
        if (instructionIndex == 0 && subStepState == PlayInstructions.InstructionSubStepState.UseKey)
            SatisfyConstraintByName("isScrewOneIn");
        if (instructionIndex == 1 && subStepState == PlayInstructions.InstructionSubStepState.AttachPiece)
        {
            SatisfyConstraintByName("isScrewOneScrewed");
            SatisfyConstraintByName("isKeyLetGoFirst", 2);
        }

        if (instructionIndex == 1 && subStepState == PlayInstructions.InstructionSubStepState.InsertScrew)
            SatisfyConstraintByName("isYellowPipeAttached");
        if (instructionIndex == 1 && subStepState == PlayInstructions.InstructionSubStepState.UseKey)
            SatisfyConstraintByName("isScrewTwoIn");
        if (instructionIndex == 2)
        {
            SatisfyConstraintByName("isScrewTwoScrewed");
            SatisfyConstraintByName("isKeyLetGoSecond", 2);
        }
    }
}
