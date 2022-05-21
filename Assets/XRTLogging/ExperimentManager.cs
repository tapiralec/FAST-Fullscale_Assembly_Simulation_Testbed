// Written for the eXtended Reality and Training Lab at the University of Central Florida, 2022
// Alec G. Moore - agm@knights.ucf.edu

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine.PlayerLoop;

namespace XRTLogging
{
    public class ExperimentManager : Singleton<ExperimentManager>
    {
        public string cohort;
        public string participant;
        public string trial;
        [Tooltip("I would recommend using experimenter UI, but you can just pre-fill these values per-build if needed")]
        public bool startLoggingAtApplicationStart = false;

        public List<ALogger> loggers;

        private static string loggingDirectory => Path.Combine(Application.persistentDataPath, "XRTLogging");
        private string _dateForFile;
        public string dateForFile => _dateForFile ?? (_dateForFile = timestampProvider.fileStyledDate);
        private TimestampProvider _timestampProvider;
        private TimestampProvider timestampProvider => _timestampProvider ? _timestampProvider : _timestampProvider = TimestampProvider.Instance;
        
        public void StartLogging()
        {
            //create metadata file if it doesn't exist
            // TODO: check if the top-level logging directory exists, if not, then add a README to it.
            if (!Directory.Exists(loggingDirectory)) Directory.CreateDirectory(loggingDirectory);
            if (!File.Exists(Path.Combine(loggingDirectory,"metadata.csv")))
            {
                using (var fc = File.CreateText(Path.Combine(loggingDirectory, "metadata.csv")))
                {
                    var sb = new StringBuilder();
                    sb.Append("cohort,participant,trial,startTime,");
                    foreach (var l in loggers)
                    {
                        if (l == null) continue;
                        sb.Append(l.loggerNameForMetadata);
                        sb.Append(",");
                    }
                    fc.WriteLine(sb.ToString());
                }
            }
            //fire event or something at all the loggers.
            foreach (var l in loggers)
            {
                if (l == null) continue;
                l.StartLogging(cohort, participant, trial);
            }
            
            //add to metadata file
            using (var fc = File.AppendText(Path.Combine(loggingDirectory, "metadata.csv")))
            {
                var sb = new StringBuilder();
                sb.Append($"{cohort},{participant},{trial},{timestampProvider.TimestampString},");
                foreach (var l in loggers)
                {
                    if (l == null) continue;
                    sb.Append(l.completeLogFilePath);
                    sb.Append(",");
                }
                fc.WriteLine(sb.ToString());
            }
            
        }

        public void StopLogging()
        {
            foreach (var l in loggers)
            {
                if (l == null) continue;
                l.StopLogging();
            }
        }

        private void ScanMetadataToPopulatePreviousLists()
        {
            _previousCohorts = new List<string>();
            _previousParticipants = new List<string>();
            _previousTrials = new List<string>();
            if (!File.Exists(Path.Combine(loggingDirectory, "metadata.csv"))) return;
            var lines = File.ReadLines(Path.Combine(loggingDirectory, "metadata.csv"));
            foreach (var line in lines.Skip(1).Reverse())
            {
                var vals = line.Split(',');
                if (!_previousCohorts.Contains(vals[0]) && !string.IsNullOrEmpty(vals[0]))
                    _previousCohorts.Add(vals[0]);
                if (!_previousParticipants.Contains(vals[1]) && !string.IsNullOrEmpty(vals[1]))
                    _previousParticipants.Add(vals[1]);
                if (!_previousTrials.Contains(vals[2]) && !string.IsNullOrEmpty(vals[2]))
                    _previousTrials.Add(vals[2]);
            }

        }

        // For previous dropdowns - make sure it's ordered so most recent is at top. unique, CaSe
        private List<string> _previousCohorts;
        public List<string> previousCohorts
        {
            get
            {
                if (_previousCohorts != null) return _previousCohorts;
                ScanMetadataToPopulatePreviousLists();
                return _previousCohorts;
                
            }
        }
        private List<string> _previousParticipants;
        public List<string> previousParticipants
        {
            get
            {
                if (_previousParticipants != null) return _previousParticipants;
                ScanMetadataToPopulatePreviousLists();
                return _previousParticipants;
                
            }
        }
        private List<string> _previousTrials;
        public List<string> previousTrials
        {
            get
            {
                if (_previousTrials != null) return _previousTrials;
                ScanMetadataToPopulatePreviousLists();
                return _previousTrials;
                
            }
        }

        private void Start()
        {
            if (startLoggingAtApplicationStart) StartLogging();
        }
        
        void OnReset()
        {
            loggers = FindObjectsOfType<ALogger>().ToList();
        }

        void Update()
        {
            if (Input.GetKey("escape"))
            {
                #if UNITY_STANDALONE
                Application.Quit();
                #endif
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
            }
        }

    }
}