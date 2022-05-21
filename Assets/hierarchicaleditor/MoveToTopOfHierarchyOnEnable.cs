using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveToTopOfHierarchyOnEnable : MonoBehaviour
{
    private void OnEnable()
    {
        transform.parent = null;
    }
}
