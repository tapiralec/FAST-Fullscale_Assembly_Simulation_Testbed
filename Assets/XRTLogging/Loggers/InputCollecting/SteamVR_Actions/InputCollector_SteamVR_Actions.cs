using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using Valve.VR;

namespace XRTLogging
{
    public class InputCollector_SteamVR_Actions : AInputCollector
    {

        public SteamVR_Input_Sources source;
        public List<NamedBoolAction> boolActionsToLog;
        public List<NamedFloatAction> floatActionsToLog;
        public List<NamedVec2Action> vec2ActionsToLog;
        public List<NamedVec3Action> vec3ActionsToLog;

        public SteamVR_ActionSet loggingActionSet;

        // NB: As of Unity 2020.1, the system should be able to directly serialize this generic type.
        // For compatibility with the 2019 inspector, I'm including concrete subclasses.
        // (The alternative was setting up OnSerialize listeners, and I'm not about that.)
        [Serializable]
        public class NamedAction<T> where T : SteamVR_Action
        {
            public string name;
            public T action;
        }

        [Serializable]
        public class NamedBoolAction : NamedAction<SteamVR_Action_Boolean>
        {
        }

        [Serializable]
        public class NamedFloatAction : NamedAction<SteamVR_Action_Single>
        {
        }

        [Serializable]
        public class NamedVec2Action : NamedAction<SteamVR_Action_Vector2>
        {
        }

        [Serializable]
        public class NamedVec3Action : NamedAction<SteamVR_Action_Vector3>
        {
        }

        [Serializable]
        public class NamedPoseAction : NamedAction<SteamVR_Action_Pose>
        {
        }

        private string _headerStringCached;

        public override string GetHeaderString()
        {
            if (!string.IsNullOrEmpty(_headerStringCached)) return _headerStringCached;
            var sb = new StringBuilder();
            foreach (var actionBoolState in boolActionsToLog)
            {
                sb.Append($"{deviceName}_{actionBoolState.name}_value,");
                sb.Append($"{deviceName}_{actionBoolState.name}_edge,");
            }

            foreach (var actionFloatState in floatActionsToLog)
            {
                sb.Append($"{deviceName}_{actionFloatState.name}_value,");
            }

            foreach (var actionVec2State in vec2ActionsToLog)
            {
                sb.Append($"{deviceName}_{actionVec2State.name}_valueX,");
                sb.Append($"{deviceName}_{actionVec2State.name}_valueY,");
            }

            foreach (var actionVec3State in vec3ActionsToLog)
            {
                sb.Append($"{deviceName}_{actionVec3State.name}_valueX,");
                sb.Append($"{deviceName}_{actionVec3State.name}_valueY,");
                sb.Append($"{deviceName}_{actionVec3State.name}_valueZ,");
            }

            _headerStringCached = sb.ToString();
            return _headerStringCached;
        }


        private string _nanStringCached;

        public override void GetNaNString(ref StringBuilder sb)
        {

            if (!string.IsNullOrEmpty(_nanStringCached))
            {
                sb.Append(_nanStringCached);
                return;
            }

            var NaNBuilder = new StringBuilder();
            foreach (var actionBoolState in boolActionsToLog)
            {
                NaNBuilder.Append($"NaN,");
                NaNBuilder.Append($"NaN,");
            }

            foreach (var actionFloatState in floatActionsToLog)
            {
                NaNBuilder.Append($"NaN,");
            }

            foreach (var actionVec2State in vec2ActionsToLog)
            {
                NaNBuilder.Append($"NaN,");
                NaNBuilder.Append($"NaN,");
            }

            foreach (var actionVec3State in vec3ActionsToLog)
            {
                NaNBuilder.Append($"NaN,");
                NaNBuilder.Append($"NaN,");
                NaNBuilder.Append($"NaN,");
            }

            _nanStringCached = NaNBuilder.ToString();
            sb.Append(NaNBuilder);

        }

        private string _formatSingle;

        private string formatSingle => _formatSingle ?? (_formatSingle =
            string.Join("", Enumerable.Range(0, 1).Select(i => "{" + i + ":" + floatFormat + "},")));

        private string _formatVec2;

        private string formatVec2 => _formatVec2 ?? (_formatVec2 =
            string.Join("", Enumerable.Range(0, 2).Select(i => "{" + i + ":" + floatFormat + "},")));

        private string _formatVec3;

        private string formatVec3 => _formatVec3 ?? (_formatVec3 =
            string.Join("", Enumerable.Range(0, 3).Select(i => "{" + i + ":" + floatFormat + "},")));

        public void Start()
        {
            // Ensure that the action set that we're trying to log from is actually active:
            if (!loggingActionSet.IsActive())
                loggingActionSet.Activate(priority: 0, disableAllOtherActionSets: false);
        }

        private string formatString;

        public override string GetFormatString(int startIndex)
        {
            if (!string.IsNullOrEmpty(formatString)) return formatString;
            var sb = new StringBuilder();
            GetFormatString(ref sb, ref startIndex);
            return sb.ToString();
        }

        public override void GetFormatString(ref StringBuilder sb, ref int startIndex)
        {
            if (!string.IsNullOrEmpty(formatString))
            {
                sb.Append(formatString);
                return;
            }

            var justOurSB = new StringBuilder();
            foreach (var actionBoolState in boolActionsToLog)
            {
                justOurSB.Append($"{{{startIndex++}}},");
                justOurSB.Append($"{{{startIndex++}}},");
            }
            foreach (var actionFloatState in floatActionsToLog)
            {
                justOurSB.Append($"{{{startIndex++}:{floatFormat}}},");
            }

            foreach (var actionVec2State in vec2ActionsToLog)
            {
                justOurSB.Append($"{{{startIndex++}:{floatFormat}}},");
                justOurSB.Append($"{{{startIndex++}:{floatFormat}}},");
            }
            foreach (var actionVec3State in vec3ActionsToLog) {
                justOurSB.Append($"{{{startIndex++}:{floatFormat}}},");
                justOurSB.Append($"{{{startIndex++}:{floatFormat}}},");
                justOurSB.Append($"{{{startIndex++}:{floatFormat}}},");
            }

            sb.Append(justOurSB);
            formatString = justOurSB.ToString();
        }
        
        public override void CopyData(ref object[] dataList, ref int index)
        {
            foreach (var actionBoolState in boolActionsToLog)
            {
                dataList[index++] = actionBoolState.action.GetState(source) ? 1 : 0;
                dataList[index++] =
                    actionBoolState.action.GetStateDown(source) ? 1 : // NB rising edge is "StateDown"
                    actionBoolState.action.GetStateUp(source) ? -1 : 0; // NB falling edge is "StateUp" 
            }
            foreach (var actionFloatState in floatActionsToLog)
            {
                dataList[index++] = actionFloatState.action.GetAxis(source);
            }

            foreach (var actionVec2State in vec2ActionsToLog)
            {
                dataList[index++] = actionVec2State.action.GetAxis(source).x;
                dataList[index++] = actionVec2State.action.GetAxis(source).y;
            }
            foreach (var actionVec3State in vec3ActionsToLog)
            {
                dataList[index++] = actionVec3State.action.GetAxis(source).x;
                dataList[index++] = actionVec3State.action.GetAxis(source).y;
                dataList[index++] = actionVec3State.action.GetAxis(source).z;
            }
        }
        public override void CopyNaNs(ref object[] dataList, ref int index)
        {
            foreach (var actionBoolState in boolActionsToLog)
            {
                dataList[index++] = float.NaN;
                dataList[index++] = float.NaN;
            }
            foreach (var actionFloatState in floatActionsToLog)
            {
                dataList[index++] = float.NaN;
            }

            foreach (var actionVec2State in vec2ActionsToLog)
            {
                dataList[index++] = float.NaN;
                dataList[index++] = float.NaN;
            }
            foreach (var actionVec3State in vec3ActionsToLog)
            {
                dataList[index++] = float.NaN;
                dataList[index++] = float.NaN;
                dataList[index++] = float.NaN;
            }
        }

        private int numberOfFields = -1;

        public override int NumberOfFields()
        {
            if (numberOfFields != -1) return numberOfFields;
            numberOfFields = 0;
            numberOfFields += boolActionsToLog.Count * 2;
            numberOfFields += floatActionsToLog.Count;
            numberOfFields += vec2ActionsToLog.Count * 2;
            numberOfFields += vec3ActionsToLog.Count * 3;
            return numberOfFields;
        }
    }

}