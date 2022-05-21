using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TableBounds : Singleton<TableBounds>
{
    public float tableHeight = 0.7f;
    public float tableWidth = 2f;
    public float tableDepth = 1f;

    private float _maxX = float.NegativeInfinity;
    private float maxX => float.IsNegativeInfinity(_maxX) ? _maxX = transform.position.x + tableWidth / 2f: _maxX; 
    private float _minX = float.PositiveInfinity;
    private float minX => float.IsPositiveInfinity(_minX) ? _minX = transform.position.x - tableWidth / 2f: _minX;
    private float _maxZ = float.NegativeInfinity;
    private float maxZ => float.IsNegativeInfinity(_maxZ) ? _maxZ = transform.position.z + tableDepth / 2f: _maxZ; 
    private float _minZ = float.PositiveInfinity;        
    private float minZ => float.IsPositiveInfinity(_minZ) ? _minZ = transform.position.z - tableDepth / 2f: _minZ; 

    public bool testBounds(Transform t)
    {
        return testBounds(t.position);
    }

    public bool testBounds(Vector3 v)
    {
        return v.x > minX && v.x < maxX && v.z > minZ && v.z < maxZ && v.y > tableHeight;
    }

}
