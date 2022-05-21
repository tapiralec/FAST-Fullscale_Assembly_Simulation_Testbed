using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using UnityEngine.Events;
using Object = System.Object;

namespace XRTLogging
{
    
    public class MotionLogger : ABufferedLogger
    {
    
        //TODO: IRL-coordinates in addition to VR coordinates 
        public override string loggerNameForMetadata => "motion logging";
        #region Variables
        #region Configuration

        [Tooltip("float format within the file")]
        public string floatFormat = "f6";
        private string _quatFormatAsArg;
        private string quatFormatAsArg => _quatFormatAsArg ?? (_quatFormatAsArg = 
            string.Join("",Enumerable.Range(0, 7).Select(i => "{"+i+":"+floatFormat+"},")));
        private string _eulerFormatAsArg;
        private string eulerFormatAsArg => _eulerFormatAsArg ?? (_eulerFormatAsArg = 
            string.Join("",Enumerable.Range(0, 6).Select(i => "{"+i+":"+floatFormat+"},")));
        private string _sixdFormatAsArg;
        private string sixdFormatAsArg => _sixdFormatAsArg ?? (_sixdFormatAsArg = 
            string.Join("",Enumerable.Range(0, 9).Select(i => "{"+i+":"+floatFormat+"},")));
        private string _quatNaNString;

        private string _fmtPosRotArgs;
        private string fmtPosRotArgs
        {
            get
            {
                if (!string.IsNullOrEmpty(_fmtPosRotArgs)) return _fmtPosRotArgs;
                _fmtPosRotArgs = 
                    string.Join("",Enumerable.Range(0, numberOutputFields).Select(i => "{"+i+":"+floatFormat+"},"));
                return _fmtPosRotArgs;
            } 
        }

        private string _totalFormatString;

        protected override string totalFormatString
        {
            get
            {
                if (!string.IsNullOrEmpty(_totalFormatString)) return _totalFormatString;
                var sb = new StringBuilder();
                sb.Append("{0:yyyy-MM-dd HH:mm:ss.fff},");
                var index = 1;
                foreach (var o in trackedObjects)
                {
                    // Add in the string for the current NamedTrackedObject
                    sb.Append(string.Join("",
                        Enumerable.Range(index, numberOutputFields)
                            .Select(i => "{" + i + ":" + floatFormat + "},")));
                    index += numberOutputFields;
                    // Add in any fields if it has an appropriate InputCollector
                    if (o.inputCollector != null)
                    {
                        var tempIndex = index;
                        o.inputCollector.GetFormatString(ref sb, ref index);
                    }
                }
                _totalFormatString = sb.ToString();
                if (index < totalFields) Debug.LogWarning($"format string doesn't go to total number of fields.\nfields={totalFields}\nstring={_totalFormatString}");
                return _totalFormatString;
            }
        }
        private object[] posRotDataForOutput;

        private int _numberOutputFields = -1;
        private int numberOutputFields =>
            _numberOutputFields != -1
                ? _numberOutputFields
                : _numberOutputFields =
                    3 +
                    (rotationRepresentation.HasFlag(RotationRepresentation.Euler) ? 3 : 0) +
                    (rotationRepresentation.HasFlag(RotationRepresentation.Quaternion) ? 4 : 0) +
                    (rotationRepresentation.HasFlag(RotationRepresentation.SixD) ? 6 : 0);
        
        private int _totalFields = -1;

        private int totalFields =>
            _totalFields != -1
                ? _totalFields
                : _totalFields =
                    1 + //timestamp
                    numberOutputFields * trackedObjects.Count +
                    trackedObjects
                        .Where(o=>o.inputCollector!=null)
                        .Sum(o => o.inputCollector.NumberOfFields());
        
        private string quatNaNString => _quatNaNString ?? (_quatNaNString =
            string.Join("", Enumerable.Range(0, 7).Select(i => "NaN,")));
        private string _eulerNaNString;
        private string eulerNaNString => _eulerNaNString ?? (_eulerNaNString =
            string.Join("", Enumerable.Range(0, 6).Select(i => "NaN,")));
        private string _sixdNaNString;
        private string sixdNaNString => _sixdNaNString ?? (_sixdNaNString =
            string.Join("", Enumerable.Range(0, 9).Select(i => "NaN,")));

        private string _posAndRotNaNString;
        private string posAndRotNaNString
        {
            get
            {
                if (!string.IsNullOrEmpty(_posAndRotNaNString)) return _fmtPosRotArgs;
                _posAndRotNaNString = 
                    string.Join("",Enumerable.Range(0, numberOutputFields).Select(i => "NaN,"));
                return _posAndRotNaNString;
            } 
        }
        

        [EnumFlags]
        [Tooltip("How to represent the orientations of objects")]
        public RotationRepresentation rotationRepresentation = RotationRepresentation.Quaternion;
        public bool HasRotationFlag(RotationRepresentation flag) => HasRotationFlag(flag,rotationRepresentation);
        public static bool HasRotationFlag(RotationRepresentation flag, RotationRepresentation basis) =>
            (flag & basis) == flag;

        [Header("Tracked Objects")]
        public List<NamedTrackedObject> trackedObjects = new List<NamedTrackedObject>();
        
        #endregion Configuration


        // the StringBuilder we'll reuse for creating the line to write to the log
        private StringBuilder lineOutBuilder = new StringBuilder();
        
        protected override string specificLoggingDirectory => Path.Combine(loggingDirectory,"Motion");

        #endregion Variables
        
        [Serializable]
        public struct NamedTrackedObject
        {
            [Tooltip("This name is what will appear in the logs.")]
            public string name;
            public GameObject gameObject;
            public AInputCollector inputCollector ;
        }


        [Flags]
        public enum RotationRepresentation
        {
            Quaternion = 1 << 0,
            Euler = 1 << 1,
            SixD = 1 << 2
        }

        #region EventFunctions
        private void Update()
        {
            if (!isLogging) return;

            // Handle first log: create the file and populate the header.
            if (firstLog)
            {
                // check if file already exists.
                if (File.Exists(completeLogFilePath))
                {
                    // if file already exists, wipe the hidden var so that on next update we'll get a new filename with a 
                    // new timestamp. If we never get an acceptable filename, we'll never write.
                    _completeLogFilePath = "";
                    return;
                }
                Debug.Log($"[MotionLogger] Will be logging to {completeLogFilePath}");
                Directory.CreateDirectory(Path.GetDirectoryName(completeLogFilePath) ?? "");

                // create file
                try
                {
                    using (var fc = File.CreateText(completeLogFilePath))
                    {
                        // write header
                        fc.WriteLine(GetHeader(trackedObjects, rotationRepresentation));
                    }
                }
                catch (ArgumentException e)
                {
                    Debug.LogError($"[MotionLogger] something wrong with this path? {completeLogFilePath}");
                    throw new ArgumentException(e.Message, e.ParamName, e.InnerException);
                }

                posRotDataForOutput = new object[numberOutputFields];
                firstLog = false;
            }

            var outputBuffer = new Object[totalFields];
            var dataIndex = 0;
            AddObjectsToBuffer(ref outputBuffer, ref dataIndex, trackedObjects);
            AddToDataQueue(outputBuffer);
            
            // Replace this section with an approach similar to that already implemented in BuildStringRepresentation:
            // Set up an Object[] and pump in all the values accordingly.
            // Also set up a fmtPosRotArgs style string that indicates the format for the full list. This can be set at
            // the beginning. (just have to make sure that passing NaNs to formatString correctly turns into "NaN"
            // Then, simply copy the, now pre-determined sized, Object[] to a dumpBuffer of Queue<Object[]>
            // With a consume Task that takes in the formatString (if there are issues getting it some other way)
            //lineOutBuilder.Clear();
            //timestampProvider.AddTimestamp(ref lineOutBuilder);
            //lineOutBuilder.Append(",");
            //BuildStringRepresentation(ref lineOutBuilder, trackedObjects, rotationRepresentation);
            //WriteToFile(lineOutBuilder.ToString());
        }

        private void Awake()
        {
            foreach (var trackedObj in trackedObjects)
            {
                if(trackedObj.inputCollector != null)
                    trackedObj.inputCollector.associatedGameObject = trackedObj.gameObject;
            }
        }
        
        private void Start()
        {
            //Debug.Log(GetTransformStringRepresentation(cachedTimestamp, trackedObjects));
            //isLogging = startLoggingAtApplicationStart;
            foreach (var trackedObj in trackedObjects)
            {
                if (trackedObj.inputCollector!=null && trackedObj.inputCollector.floatFormat != floatFormat)
                    Debug.LogWarning(
                        $"[MotionLogger] float format for {trackedObj.inputCollector.deviceName} is not the same as the MotionLogger float format!");
            }
        }
        
        private void OnApplicationQuit()
        {
            if (firstLog) return; // We never wrote, so we shouldn't try to clean up.
            Debug.Log("[MotionLogger] cleaning up.");
            ForceFileFlush();
        }

        /// <summary>
        ///  Search for objects the developer likely wants to track on Reset for convenience.
        /// </summary>
        private void Reset()
        {
            // TODO: add other reasonable defaults to this list based on different VR setups.
            // Look for heads to track:
            foreach (var trackedObjectName in new[] { "VRCamera" })
            {
                var foundGameObject = GameObject.Find(trackedObjectName);
                if (foundGameObject != null)
                {
                    trackedObjects.Add(new NamedTrackedObject
                    {
                        name = $"Head{(trackedObjects.Count == 0 ? "" : trackedObjects.Count.ToString())}",
                        gameObject = foundGameObject
                    });
                }
            }
            // TODO: add other reasonable defaults to this list based on different VR setups.
            // Look for other tracked objects to track
            foreach (var trackedObjectName in new[] { "LeftHand", "RightHand" })
            {
                var foundGameObject = GameObject.Find(trackedObjectName);
                if (foundGameObject != null)
                {
                    var currentLogger = gameObject.GetComponents<AInputCollector>().FirstOrDefault(l => l.deviceName == trackedObjectName);
                    if (currentLogger == null)
                    {
                        // might not be able to do this cleanly. Can probably just set up prefabs though.
                        //currentLogger = gameObject.AddComponent<InputCollector_SteamVR_Vive_Raw>();
                        //currentLogger.deviceName = trackedObjectName;
                    }

                    trackedObjects.Add(new NamedTrackedObject
                    {
                        name = trackedObjectName, gameObject = foundGameObject, inputCollector = currentLogger
                    });
                }
            }

            // Let the developer know what trackedObjects was populated with
            foreach (var foundObject in trackedObjects)
            {
                Debug.LogWarning($"Populating trackedObjects with {foundObject.gameObject.name} ({foundObject.name})",
                    foundObject.gameObject);
            }

        }
        #endregion EventFunctions
        
        #region Methods


        // Setters that can be called from Unity Events:
        public void SetCohort(string newCohort) => cohort = newCohort;
        public void SetParticipant(string newParticipant) => participant = newParticipant;
        public void SetTrial(string newTrial) => trial = newTrial;

        /// <summary>
        /// Suspends or resumes logging. Toggles current state when not given an argument.
        /// </summary>
        /// <param name="resume">true to force logging back on, false to force logging off </param>
        /// <returns>whether we are now logging</returns>
        public bool SuspendResumeLogging(bool? resume) => isLogging = resume ?? !isLogging;

        /// <summary>
        /// Generates a header string given the provided objects and the rotation representation
        /// </summary>
        /// <param name="objects">the list of tracked objects that will be getting logged</param>
        /// <param name="rotRep">the representation for rotations that we'll be using</param>
        /// <returns>the header as a string</returns>
        private static string GetHeader(List<NamedTrackedObject> objects, RotationRepresentation rotRep)
        {
            var sb = new StringBuilder();
            sb.Append("Timestamp,");
            foreach (var o in objects)
            {
                sb.Append($"{o.name}_position_x,{o.name}_position_y,{o.name}_position_z,");
                if (HasRotationFlag(RotationRepresentation.Quaternion, rotRep))
                    sb.Append($"{o.name}_quat_x,{o.name}_quat_y,{o.name}_quat_z,{o.name}_quat_w,");
                if (HasRotationFlag(RotationRepresentation.Euler,rotRep))
                    sb.Append($"{o.name}_euler_x,{o.name}_euler_y,{o.name}_euler_z,");
                if (HasRotationFlag(RotationRepresentation.SixD, rotRep))
                    sb.Append($"{o.name}_sixD_a,{o.name}_sixD_b,{o.name}_sixD_c,{o.name}_sixD_d,{o.name}_sixD_e,{o.name}_sixD_f,");
                if (o.inputCollector!=null) sb.Append(o.inputCollector.GetHeaderString());
            }

            return sb.ToString();
        }
        
        private void AddObjectsToBuffer(ref Object[] buffer, ref int dataIndex, List<NamedTrackedObject> objects)
        {
            buffer[dataIndex++] = timestampProvider.Timestamp;
            foreach (var o in objects)
            {
                if (o.gameObject.activeInHierarchy)
                {
                    var pos = o.gameObject.transform.position;
                    var rot = o.gameObject.transform.rotation;
                    buffer[dataIndex++] = pos.x;
                    buffer[dataIndex++] = pos.y;
                    buffer[dataIndex++] = pos.z;
                    if (HasRotationFlag(RotationRepresentation.Quaternion))
                    {
                        buffer[dataIndex++] = rot.x;
                        buffer[dataIndex++] = rot.y;
                        buffer[dataIndex++] = rot.z;
                        buffer[dataIndex++] = rot.w;
                    }

                    if (HasRotationFlag(RotationRepresentation.Euler))
                    {
                        buffer[dataIndex++] = rot.eulerAngles.x;
                        buffer[dataIndex++] = rot.eulerAngles.y;
                        buffer[dataIndex++] = rot.eulerAngles.z;
                    }

                    if (HasRotationFlag(RotationRepresentation.SixD))
                    {
                        SixDConversions.CopyTo6DInPlace(rot,ref buffer ,ref dataIndex);
                    }

                    if (o.inputCollector != null) o.inputCollector.CopyData(ref buffer, ref dataIndex);
                }
                else
                {
                    // Copy NaNs for the position and orientation values
                    for (int i = 0; i < numberOutputFields; i++)
                    {
                        buffer[dataIndex++] = float.NaN;
                    }
                    // If we have an inputCollector, copy the NaNs to the buffer as well.
                    if (o.inputCollector != null) o.inputCollector.CopyNaNs(ref buffer, ref dataIndex);
                }
            }

        }
        #endregion Methods
    }

}