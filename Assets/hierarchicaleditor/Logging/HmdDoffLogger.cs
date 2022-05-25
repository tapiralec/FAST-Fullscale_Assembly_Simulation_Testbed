using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Valve.VR;
using Valve.VR.Extras;
using Valve.VR.InteractionSystem;
using XRTLogging;


public class HmdDoffLogger : EventLogger
{

    private StringBuilder _lineOutBuilder;

    private StringBuilder lineOutBuilder
    {
        get => _lineOutBuilder ?? new StringBuilder();
        set => _lineOutBuilder = value;
    }

    public override string loggerNameForMetadata => "HMD doff logging";
    protected override string specificLoggingDirectory => Path.Combine(loggingDirectory, "HmdDoffing");

    public override void StartLogging()
    {
        Debug.Log("$[HmdDoffLogger] Will be logging to {completeLogFilePath}");
        Directory.CreateDirectory(Path.GetDirectoryName(completeLogFilePath) ?? "");
        using (var fc = File.CreateText(completeLogFilePath))
        {
            fc.WriteLine("Timestamp,event");
        }
    }

    public void LogDonDoffEvent(bool isHmdBeingPutOn)
    {
        Task.Run(() =>
        {
            WriteToFile(string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff},{1}",
                timestampProvider.Timestamp, (isHmdBeingPutOn ? "On" : "Off") ));
        });
    }

    public SteamVR_Action_Boolean Hmd_action;
    private void Start()
    {
        Hmd_action.onChange += delegate(SteamVR_Action_Boolean action, SteamVR_Input_Sources source, bool state) { LogDonDoffEvent(state); };
    }

}
