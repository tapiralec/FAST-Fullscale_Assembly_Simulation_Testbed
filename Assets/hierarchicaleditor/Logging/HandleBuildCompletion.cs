using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XRTLogging;
public class HandleBuildCompletion : MonoBehaviour
{

    public List<GameObject> gameObjectsToDisableAfterTime;
    public List<GameObject> gameObjectsToHideImmediately;
    public ExperimentManager experimentManager;
    public float secondsUntilDisableGameObjects = 10f;

    public void DoComplete()
    {
        foreach (var g in gameObjectsToHideImmediately)
        {
            HideMesh(g);
        }
        StartCoroutine(WaitThenHideSubstructure());
    }

    private IEnumerator WaitThenHideSubstructure()
    {
        yield return new WaitForSeconds(secondsUntilDisableGameObjects);
        experimentManager.StopLogging();
        foreach (var g in gameObjectsToDisableAfterTime)
        {
            g.SetActive(false);
        }
    }
    private void HideMesh(GameObject g)
    {
        var meshRenderers =  g.GetComponentsInChildren<MeshRenderer>();
        foreach (var m in meshRenderers)
        {
            m.enabled = false;
        }
    }

}
