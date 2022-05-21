using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace XRTLogging
{
    public class LogEventEmitter : MonoBehaviour
    {
        public string logType;
        public string eventMessage;
        public UnityEvent<string> e;
        public EventLogger eventLogger;

        public void SendEventMessage()
        {
            eventLogger.LogString(logType,eventMessage);
        }
        private void Reset()
        {
            eventLogger = FindObjectOfType<EventLogger>();
        }
    }

}