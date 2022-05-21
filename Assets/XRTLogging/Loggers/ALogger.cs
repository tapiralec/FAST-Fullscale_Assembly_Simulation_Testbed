using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRTLogging
{
    public abstract class ALogger : MonoBehaviour
    {
        // are we currently logging?
        protected bool isLogging = false;
        // is this our first time logging?
        protected bool firstLog = true;
        public abstract string loggerNameForMetadata { get; }
        
        [HideInInspector] public string cohort = "";
        [HideInInspector] public string participant = "";
        [HideInInspector] public string trial = "";
        
        private TimestampProvider _timestampProvider;
        protected TimestampProvider timestampProvider => _timestampProvider ? _timestampProvider : _timestampProvider = TimestampProvider.Instance;

        
        //TODO: should I be incorporating the scene name into the directory?
        //protected static string loggingDirectory => Path.Combine(Application.persistentDataPath, SceneManager.GetActiveScene().name+"-XRTLogging");
        protected static string loggingDirectory => Path.Combine(Application.persistentDataPath, "XRTLogging");
        protected abstract string specificLoggingDirectory { get; }

        protected virtual string filetype => ".csv";

        protected string _completeLogFilePath;
        
        /// <summary>
        /// Create the complete log file path - any desired changes to the hierarchy of logged data across all types of
        /// data should be done right here.
        /// </summary>
        public string completeLogFilePath => !string.IsNullOrEmpty(_completeLogFilePath) ? _completeLogFilePath :
             (_completeLogFilePath = Path.Combine(specificLoggingDirectory, cohort, participant,
                 participant + (participant != "" ? "_" : "") +
                 trial + (trial != "" ? "_" : "") +
                       dateForFile + filetype));
        private string _dateForFile;
        public string dateForFile => _dateForFile ?? (_dateForFile = timestampProvider.fileStyledDate);

                 
        /// <summary>
        /// Asynchronously writes the provided line to the file
        /// </summary>
        /// <param name="line">the line to write</param>
        protected virtual async void WriteToFile(string line)
        {
            await Task.Run(async () =>
            {
                using (var f = File.AppendText(completeLogFilePath))
                {
                    await f.WriteLineAsync(line);
                }
            });

        }

        /// <summary>
        /// Awaits on a FlushAsync to make sure nothing remains in the buffer. (might not be necessary?)
        /// </summary>
        protected virtual async void ForceFileFlush()
        {
            if (hasFlushedToExit) return;
            if (!File.Exists(completeLogFilePath)) return;
            var f = File.AppendText(completeLogFilePath);
            await f.FlushAsync();
            f.Close();
            hasFlushedToExit = true;
        }

        public bool hasFlushedToExit { get; protected set; }

        public virtual void StopLogging()
        {
            ForceFileFlush();
        }

        /// <summary>
        /// Start logging the tracking data.
        /// </summary>
        public virtual void StartLogging()
        {
            isLogging = true;
            Debug.Log("Starting to log motion data", this);
        }

        /// <summary>
        /// Start logging the tracking data, with optional variables to be set:
        /// </summary>
        /// <param name="newCohort">new value for cohort</param>
        /// <param name="newParticipant">new value for participant</param>
        /// <param name="newTrial">new value for trial</param>
        public virtual void StartLogging(string newCohort, string newParticipant, string newTrial)
        {
            cohort = newCohort;
            participant = newParticipant;
            trial = newTrial;
            StartLogging();
        }
    }
}
