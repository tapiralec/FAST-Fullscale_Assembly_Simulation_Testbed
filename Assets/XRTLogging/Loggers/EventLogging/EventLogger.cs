using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace XRTLogging
{
    public class EventLogger : ALogger
    {
        public override string loggerNameForMetadata => "Event Logger";
        protected override string specificLoggingDirectory => Path.Combine(loggingDirectory, "Events");

        public override void StartLogging(string cohort, string participant, string trial)
        {
            this.cohort = cohort;
            this.participant = participant;
            this.trial = trial;
            StartLogging();
        }

        public override void StartLogging()
        {
            Debug.Log($"[EventLogger] Will be logging to {completeLogFilePath}");
            Directory.CreateDirectory(Path.GetDirectoryName(completeLogFilePath) ?? "");
            using (var fc = File.CreateText(completeLogFilePath))
            {

                fc.WriteLine("Timestamp,logType,parameters...");
            }
        }

        // Probably could do something better here...
        public void LogString(string logType, string parameters)
        {
            var lineOutBuilder = new StringBuilder();
            timestampProvider.AddTimestamp(ref lineOutBuilder);
            lineOutBuilder.Append(",");
            lineOutBuilder.Append(logType);
            lineOutBuilder.Append(",");
            lineOutBuilder.Append(parameters);
            lineOutBuilder.Append(",");
            WriteToFile(lineOutBuilder.ToString());
        }

        private void OnApplicationQuit()
        {
            if (firstLog) return;
            Debug.Log("[EventLogger] cleaning up.");
            ForceFileFlush();
        }
    }

}