using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using XRTLogging;

public class OptionSelector : MonoBehaviour
{

    public enum FieldToFill
    {
        Cohort,Participant,Trial
    }
    
    public Dropdown dropdown;
    public InputField inputField;
    private string _valueToUse;
    public string valueToUse
    {
        get => _valueToUse;
        set
        {
            _valueToUse = value;
            switch (fieldToFill)
            {
                case FieldToFill.Cohort:
                    Debug.Log($"Set cohort to {value}");
                    ExperimentManager.Instance.cohort = value;
                    break;
                case FieldToFill.Participant:
                    ExperimentManager.Instance.participant = value;
                    break;
                case FieldToFill.Trial:
                    ExperimentManager.Instance.trial = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    public FieldToFill fieldToFill;
    
    private void Start()
    {
        inputField.onValueChanged.AddListener(delegate { InputFieldChanged(); });
        dropdown.onValueChanged.AddListener(delegate { DropdownChanged();});
        switch (fieldToFill)
        {
            case FieldToFill.Cohort:
                foreach (var s in ExperimentManager.Instance.previousCohorts)
                    dropdown.options.Add(new Dropdown.OptionData(s));
                break;
            case FieldToFill.Participant:
                foreach(var s in ExperimentManager.Instance.previousParticipants)
                    dropdown.options.Add(new Dropdown.OptionData(s));
                break;
            case FieldToFill.Trial:
                foreach(var s in ExperimentManager.Instance.previousTrials)
                    dropdown.options.Add(new Dropdown.OptionData(s));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void InputFieldChanged()
    {
        if (!string.IsNullOrEmpty(inputField.text))
        {
            valueToUse = inputField.text;
        }
    }

    private void DropdownChanged()
    {
        if (dropdown.value != 0)
        {
            valueToUse = dropdown.options[dropdown.value].text;
            inputField.text = "";
            inputField.interactable = false;
        }
        else
        {
            valueToUse = "";
            inputField.interactable = true;
            inputField.ActivateInputField();
        }
    }
    
}
