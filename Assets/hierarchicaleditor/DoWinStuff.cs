using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

//[DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
//[DllImport("winmm.dll")]
//public static extern MediaDevice; 

using UnityEngine;

public class DoWinStuff : MonoBehaviour
{

    public bool doStuff = false;
    
    // Start is called before the first frame update
    void Start()
    {
        if (doStuff)
        {

            var conf = AudioSettings.GetConfiguration();
            var process = new System.Diagnostics.Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.FileName = @"C:\windows\system32\windowspowershell\v1.0\powershell.exe ";
            process.StartInfo.Arguments = "echo 'hello'";

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Debug.Log("test: " + output);
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
