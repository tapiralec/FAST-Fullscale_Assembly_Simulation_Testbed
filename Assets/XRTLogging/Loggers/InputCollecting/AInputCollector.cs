using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace XRTLogging
{
    public abstract class AInputCollector : MonoBehaviour
    {
        public abstract string GetHeaderString();
        public abstract void GetNaNString(ref StringBuilder sb);
        public string deviceName;
        public string floatFormat = "f6";
        
        public GameObject associatedGameObject;

        public abstract string GetFormatString(int startIndex);
        public abstract void GetFormatString(ref StringBuilder sb, ref int startIndex);
        public abstract void CopyData(ref object[] dataList, ref int startIndex);
        public abstract void CopyNaNs(ref object[] dataList, ref int index);

        public abstract int NumberOfFields();
    }
}
