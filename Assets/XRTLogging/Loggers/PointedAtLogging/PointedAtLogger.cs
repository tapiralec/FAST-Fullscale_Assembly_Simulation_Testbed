using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Valve.VR.InteractionSystem;
using Object = System.Object;

namespace XRTLogging
{
    public class PointedAtLogger : ABufferedLogger
    {

        public bool defaultToGameObjectName = true;
        [Tooltip("The name to write when no valid object is being pointed at (e.g. skybox)")]
        public string defaultNoObjectName = "null";
        
        public override string loggerNameForMetadata => "pointing at logging";
        protected override string specificLoggingDirectory => Path.Combine(loggingDirectory, "Pointing");
        
        //To add in gaze tracking, extend this script, making sure to:
        // * Add additional args to the totalFormatString
        // * Add the gazed at object to the OutputBuffer in AddObjectsToOutputBuffer
        //   (keep the columns consistent: make sure to add the defaultNoObjectName if you can't get a valid object to look at)
        // * Add the name to the GetHeader function
        // * Increase total fields to account for newly added fields.
        // make sure ordering stays consistent or the data will be mislabeled.
        
        private string _totalFormatString;
        protected override string totalFormatString
        {
            get
            {
                if (!string.IsNullOrEmpty(_totalFormatString)) return _totalFormatString;
                var sb = new StringBuilder();
                sb.Append("{0:yyyy-MM-dd HH:mm:ss.fff},");
                var index = 1;
                foreach (var v in trackedPointers)
                {
                    sb.Append($"{{{index++}}},");
                }

                foreach (var h in handHovers)
                {
                    sb.Append($"{{{index++}}},");
                }
                _totalFormatString = sb.ToString();
                if (index < totalFields) Debug.LogWarning( $"format string doesn't go to total number of fields.\nfields={totalFields}\nstring={_totalFormatString}");
                return _totalFormatString;
            }
        }
        
        public List<namedTransformDirection> trackedPointers;
        [Serializable]
        public class namedTransformDirection
        {
            public string name;
            public Transform transform;
            public Vector3 direction;
        }

        public List<namedHand> handHovers;

        [Serializable]
        public class namedHand
        {
            public string name;
            public Hand hand;
        }

        private void Update()
        {
            if (!isLogging) return;
            if (firstLog)
            {
                if (File.Exists(completeLogFilePath))
                {
                    _completeLogFilePath = "";
                    return;
                }

                Debug.Log($"[GazeLogger] Will be logging to {completeLogFilePath}");
                Directory.CreateDirectory(Path.GetDirectoryName(completeLogFilePath) ?? "");

                try
                {
                    using (var fc = File.CreateText(completeLogFilePath))
                    {
                        fc.WriteLine(GetHeader());
                    }
                }
                catch (ArgumentException e)
                {
                    Debug.LogError($"[GazeLogger] something wrong with this path? {completeLogFilePath}");
                    throw new ArgumentException(e.Message, e.ParamName, e.InnerException);
                }
                firstLog = false;
            }

            var outputBuffer = new Object[totalFields];
            var dataIndex = 0;
            AddObjectsToBuffer(ref outputBuffer, ref dataIndex, trackedPointers);
            AddToDataQueue(outputBuffer);
        }

        private string GetHeader()
        {
            var sb = new StringBuilder();
            sb.Append("Timestamp,");
            foreach (var p in trackedPointers)
            {
                sb.Append($"{p.name},");
            }
            foreach (var h in handHovers)
            {
                sb.Append($"{h.name},");
            }
            return sb.ToString();
        }

        private void AddObjectsToBuffer(ref Object[] outputBuffer, ref int dataIndex,
            List<namedTransformDirection> trackedPointers)
        {
            outputBuffer[dataIndex++] = timestampProvider.Timestamp;
            foreach (var t in trackedPointers)
            {
                if (t.transform.gameObject.activeInHierarchy && Physics.Raycast(t.transform.position, t.transform.rotation * t.direction, out var ray))
                {
                    if (ray.collider.gameObject.TryGetComponent(out PointedAtLabel pointedAtLabel))
                    {
                        outputBuffer[dataIndex++] = pointedAtLabel.label;
                    }
                    else if (defaultToGameObjectName)
                    {
                        outputBuffer[dataIndex++] = ray.collider.gameObject.name;
                    }
                    else
                    {
                        outputBuffer[dataIndex++] = defaultNoObjectName;
                    }
                }
                else
                {
                    outputBuffer[dataIndex++] = defaultNoObjectName;
                }
            }

            foreach (var h in handHovers)
            {
                if (h.hand.hoveringInteractable != null)
                {
                    if (h.hand.hoveringInteractable.gameObject.TryGetComponent(out PointedAtLabel pointedAtLabel))
                    {
                        outputBuffer[dataIndex++] = pointedAtLabel.label;
                    }
                    else if (defaultToGameObjectName)
                    {
                        outputBuffer[dataIndex++] = h.hand.hoveringInteractable.gameObject.name;
                    }
                    else
                    {
                        outputBuffer[dataIndex++] = defaultNoObjectName;
                    }
                }
                else
                {
                    outputBuffer[dataIndex++] = defaultNoObjectName;
                }
            }
        }


        private int _totalFields = -1;

        private int totalFields =>
            _totalFields != -1
                ? _totalFields
                : _totalFields =
                    1 + //timestamp
                    trackedPointers.Count + handHovers.Count;

    }
}