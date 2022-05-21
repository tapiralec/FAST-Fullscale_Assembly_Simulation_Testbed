using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace XRTLogging
{
    public class PointedAtLabel : MonoBehaviour
    {
        [SerializeField] protected string _label = "unspecified";
        public string label => _label;
    }

}