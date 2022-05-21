using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PlayStructure
{
    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/ScriptableInstructions")]
    public class ScriptableInstructions : ScriptableObject
    {
        [SerializeField]
        private List<InstructionStepToSave> _mInstructions;

        public List<InstructionStepToSave> instructions
        {
            get => _mInstructions ??= new List<InstructionStepToSave>();
            set => _mInstructions = value;
        }
    }
}