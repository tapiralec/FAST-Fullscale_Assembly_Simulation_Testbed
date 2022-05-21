using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace XRTLogging
{
    public abstract class ABufferedLogger : ALogger
    {
        [Tooltip("how long to wait between file writes (ms)")]
        [SerializeField] protected int bufferedWriterDumpFrequency = 2000;
        protected bool useBufferedWriter;
        protected SemaphoreSlim dumpMutex = new SemaphoreSlim(1, 1);
        //protected Queue<string> dumpStringQueue = new Queue<string>();
        protected Queue<object[]> dumpObjectQueue = new Queue<object[]>();
        protected abstract string totalFormatString { get; }
        protected void LaunchBufferedWriter()
        {
            useBufferedWriter = true;
            Debug.Log("launching buffered writer");
            Task.Run(async () =>
                {
                    while (isLogging && useBufferedWriter)
                    {
                        if (dumpObjectQueue.Count > 0)
                        {
                            //Debug.Log($"Dumping {dumpObjectQueue.Count} lines to file.");
                            await dumpMutex.WaitAsync();
                            File.AppendAllLines(completeLogFilePath, dumpObjectQueue.ToArray().Select(o=>string.Format(totalFormatString,o)));
                            dumpObjectQueue.Clear();
                            dumpMutex.Release();
                            //dumpStringQueue.Clear();
                        }
                        await Task.Delay(bufferedWriterDumpFrequency);
                    }
                    //Debug.Log("buffered writer closing");
                }
            );
        }

        /// <summary>
        /// Asynchronously writes the provided line to the dumpBuffer
        /// </summary>
        /// <param name="line">the line to write</param>
        protected override async void WriteToFile(string line)
        {
            // if the mutex is necessary, move this into an async task that waits for mutex to be free.
            if (useBufferedWriter)
            {
                //await Task.Run(()=> { dumpStringQueue.Enqueue(line); });
            }
        }

        protected void AddToDataQueue(object[] data)
        {
            dumpMutex.Wait();
            dumpObjectQueue.Enqueue(data);
            dumpMutex.Release();
        }

        //protected async void AddToDataQueue(object[] data)
        //{
            //await Task.Run(() => { dumpObjectQueue.Enqueue(data); });
        //}


        /// <summary>
        /// Awaits on a FlushAsync to make sure nothing remains in the buffer. (might not be necessary?)
        /// </summary>
        ///
        protected override async void ForceFileFlush()
        {
            if (hasFlushedToExit) return;
            if (!File.Exists(completeLogFilePath)) return;
            await Task.Run(()=>
            {
                dumpMutex.Wait();
                File.AppendAllLines(completeLogFilePath, dumpObjectQueue.ToArray().Select(o=>string.Format(totalFormatString,o)));
                dumpObjectQueue.Clear();
                dumpMutex.Release();
            });
            // get the task to stop running
            useBufferedWriter = false;
            hasFlushedToExit = true;
        }
        
        /// <summary>
        /// Start logging the tracking data.
        /// </summary>
        public override void StartLogging()
        {
            isLogging = true;
            LaunchBufferedWriter();
            Debug.Log("Starting to log motion data", this);
        }
        

    }
}
