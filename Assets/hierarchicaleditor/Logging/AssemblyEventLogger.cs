using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using XRTLogging;

public class AssemblyEventLogger : EventLogger
{

    private StringBuilder _lineOutBuilder;
    private StringBuilder lineOutBuilder
    {
        get => _lineOutBuilder ??= new StringBuilder();
        set => _lineOutBuilder = value;
    }

    public override string loggerNameForMetadata => "assembly event logging";
    protected override string specificLoggingDirectory => Path.Combine(loggingDirectory,"Assembly");
    
    public override void StartLogging()
    {
        Debug.Log($"[AssemblyEventLogger] Will be logging to {completeLogFilePath}");
        Directory.CreateDirectory(Path.GetDirectoryName(completeLogFilePath) ?? "");
        using (var fc = File.CreateText(completeLogFilePath))
        {

            fc.WriteLine("Timestamp,step,subStep");
        }
    }

    private static readonly Dictionary<PlayInstructions.InstructionSubStepState, string> subStepNames =
        new Dictionary<PlayInstructions.InstructionSubStepState, string>
        {
            { PlayInstructions.InstructionSubStepState.AttachPiece, "AttachPiece" },
            { PlayInstructions.InstructionSubStepState.InsertScrew, "InsertScrew" },
            { PlayInstructions.InstructionSubStepState.UseKey, "UseKey" },
        };
    public void LogAssemblyEvent(int step, PlayInstructions.InstructionSubStepState subStep)
    {
        Task.Run(() =>
        {
            WriteToFile(string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff},{1},{2}",
                timestampProvider.Timestamp,
                step, subStepNames[subStep]));
        });
    }

}
