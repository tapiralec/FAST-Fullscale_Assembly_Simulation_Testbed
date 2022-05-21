using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace XRTLogging
{
    public class TimestampProvider : Singleton<TimestampProvider>
    {
        
        [Tooltip("timestamp format for filenames")]
        public string fileDateFormat = "yyyyMMdd_HHmmss";
        private string _fileDateFormatAsArg;
        private string fileDateFormatAsArg => _fileDateFormatAsArg ?? (_fileDateFormatAsArg =
            "{0:" + fileDateFormat + "}");

        public string fileStyledDate => string.Format(CultureInfo.InvariantCulture, fileDateFormatAsArg, Timestamp);

        // exposed variable for timestampFormat disabled temporarily because optimization in the cached StringBuilder
        // requires splitting out only the changed values, and that would require a lot of logic to handle properly with
        // a variable instead of a constant string.
        //[Tooltip("timestamp format within files")]
        //public string timestampFormat = "yyyy-MM-dd HH:mm:ss.fff";
        //private string _timestampFormatAsArg;
        //private string timestampFormatAsArg => _timestampFormatAsArg ?? (_timestampFormatAsArg =
        //    "{0:" + timestampFormat + "}");

        public void AddTimestamp(ref StringBuilder sb)
        {
            sb.Append(TimestampString);
        }

        /// <summary>
        /// if true, sets the timestamp in LateUpdate (in a different thread) so it's ready ahead of time by Update.
        /// </summary>
        public bool isEvaluatedEveryFrame = true;

        private DateTime _timestamp = DateTime.UtcNow;
        /// <summary>
        /// Gets the "official" timestamp for the frame.
        /// </summary>
        public DateTime Timestamp
        {
            get
            {
                if (_newTimestamp)
                {
                    _newTimestamp = false;
                    _timestamp = DateTime.UtcNow;
                }
                return _timestamp;
            }
        }

        private StringBuilder _cachedTimestampSB;
        /// <summary>
        /// The StringBuilder representation of the most recently given DateTime, used for reducing ToString casts.
        /// (getter is used to ensure valid DateTime in contents from first access.)
        /// </summary>
        private StringBuilder cachedTimestampSB
        {
            get
            {
                if (_cachedTimestampSB != null) return _cachedTimestampSB;
                _cachedTimestampSB = new StringBuilder(capacity: 32); //default capacity = 16
                _cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff}", Timestamp);
                return _cachedTimestampSB;
            }
        }
    
        /// <summary>
        /// The DateTime form of the string currently in cachedTimestampSB, for quick comparison.
        /// </summary>
        private DateTime _cachedTimestampDT;

        private Task<string> _task;
        /// <summary>
        /// Get the string representation of the timestamp via an asynchronous StringBuilder.
        /// (if using isEvaluatedEveryFrame=False, consider using AnticipateTimestampString to begin Async Task ahead of
        /// time.)
        /// </summary>
        public string TimestampString
        {
            get
            {
                //if the task is null, or it's not been evaluated for this frame:
                if (_task == null || _newTimestampString)
                {
                    AnticipateTimestampString();
                }
                return _task.Result;
            }
        }

        /// <summary>
        /// Runs the async task to convert the current timestamp into a string. Not necessary if using
        /// isEvaluatedEveryFrame=true. (Running this more than once won't break it, but it will cost time and GC).
        /// </summary>
        public void AnticipateTimestampString()
        {
            _task = Task.Run(_generateTimestampString);
        }

        /// <summary>
        /// Asynchronously converts a DateTime object into a string, caching parts of the result.
        /// 
        /// This is a CPU-bound async task, and as such, it contains no await functions internally.
        /// Because of this, it will run synchronously for no benefit unless put on a background thread,
        /// thus, it should be called via a Task and awaited elsewhere. e.g.: "Task.Run(()=>_generateTimestampString(timestamp))"
        /// </summary>
        /// <param name="time">A datetime object to turn into a string</param>
        /// <returns>A timestamp string</returns>
        #pragma warning disable 1998
        private async Task<string> _generateTimestampString(DateTime time)
        #pragma warning restore 1998
        {
            _newTimestampString = false;
            // Ensure the StringBuilder is at a size we expect, and there hasn't been a date rollover.
            // The format string used is "{0:yyyy-MM-dd HH:mm:ss.fff}".
            if (cachedTimestampSB.Length != 23 || Timestamp.Date != _cachedTimestampDT.Date)
            {
                cachedTimestampSB.Clear();
                cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture,"{0:yyyy-MM-dd HH:mm:ss.fff}", Timestamp);
                _cachedTimestampDT = Timestamp;
                return cachedTimestampSB.ToString();
            }
        
            //only change the part of the timestamp string that has changed.
            if (Timestamp.Hour != _cachedTimestampDT.Hour)
            {
                cachedTimestampSB.Length = 11;
                cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture,"{0:HH:mm:ss.fff}", Timestamp);
            }
            else if (Timestamp.Minute != _cachedTimestampDT.Minute)
            {
                cachedTimestampSB.Length = 14;
                cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:mm:ss.fff}", Timestamp);
            }
            else if (Timestamp.Second != _cachedTimestampDT.Second)
            {
                cachedTimestampSB.Length = 17;
                cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:ss.fff}", Timestamp);
            }
            else // (timestamp.Millisecond)
            {
                cachedTimestampSB.Length = 20;
                cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:fff}", Timestamp);
            }
            _cachedTimestampDT = Timestamp;
            return cachedTimestampSB.ToString();
        
        }
        
        
        
        private async Task<string> _generateTimestampString()
        #pragma warning restore 1998
        {
            _newTimestampString = false;
            // Ensure the StringBuilder is at a size we expect, and there hasn't been a date rollover.
            // The format string used is "{0:yyyy-MM-dd HH:mm:ss.fff}".
            return await Task.Run(() =>
            {
                if (cachedTimestampSB.Length != 23 || Timestamp.Date != _cachedTimestampDT.Date)
                {
                    cachedTimestampSB.Clear();
                    cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss.fff}",
                        Timestamp);
                    _cachedTimestampDT = Timestamp;
                    return cachedTimestampSB.ToString();
                }

                //only change the part of the timestamp string that has changed.
                if (Timestamp.Hour != _cachedTimestampDT.Hour)
                {
                    cachedTimestampSB.Length = 11;
                    cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:HH:mm:ss.fff}", Timestamp);
                }
                else if (Timestamp.Minute != _cachedTimestampDT.Minute)
                {
                    cachedTimestampSB.Length = 14;
                    cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:mm:ss.fff}", Timestamp);
                }
                else if (Timestamp.Second != _cachedTimestampDT.Second)
                {
                    cachedTimestampSB.Length = 17;
                    cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:ss.fff}", Timestamp);
                }
                else // (timestamp.Millisecond)
                {
                    cachedTimestampSB.Length = 20;
                    cachedTimestampSB.AppendFormat(CultureInfo.InvariantCulture, "{0:fff}", Timestamp);
                }
                _cachedTimestampDT = Timestamp;
                return cachedTimestampSB.ToString();
            });
        }
    
        // Once we LateUpdate, it's time to get new values.
        private bool _newTimestamp = true;
        private bool _newTimestampString = true;
        private void LateUpdate()
        {
            _newTimestamp = true;
            _newTimestampString = true;
            // If we're evaluating every frame, may as well put this on another thread.
            if (isEvaluatedEveryFrame)
            {
                AnticipateTimestampString();
            }
        }
    }
}
