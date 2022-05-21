using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.Net.WebSockets;
using System.IO;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace XRTLogging
{
    public class ScreencapLogger_OBS : ALogger
    {
        // Start is called before the first frame update
        private Process obsProc;
        private bool processStarted = false;
        public int port = 4444;
        private OBS_Remote obsRemote;
        public string obsSceneName = "VR capture";
        public string obsSceneCollectionName = "XRTLogging";
        public string obsProfileName = "XRTLogging";
        public bool debugDoNotManageRunningOBSProcess = false;
        public bool debugLogSocketMessages = false;
        void Start()
        {
            var p = FindRunningOBSProcess();
            if (p != null && !processStarted && !debugDoNotManageRunningOBSProcess) // don't accidentally grab the OBS process we started
            {
                Debug.Log($"[ScreencapLogger_OBS] found OBS Process ({p.ProcessName}) attempting to close...", this);
                p.CloseMainWindow();
                p.Close();
                Debug.Log($"[ScreencapLogger_OBS] closed already running OBS Process.", this);
            }
        }

        void Update()
        {
            
            if (!processStarted)
            {
                if (!debugDoNotManageRunningOBSProcess)
                {
                    obsProc = StartOBSProcess();
                }
                else
                {
                    var p = FindRunningOBSProcess();
                    if (p is null)
                    {
                        Debug.LogWarning(
                            "[ScreencapLogger_OBS] did not find running OBS process. <debugDoNotManageRunningOBSProcess>");
                        processStarted = true;
                        //return;
                    }
                    Debug.Log("ScreencapLogger_OBS] using running OBS process. <debugDoNotManageRunningOBSProcess>", this);
                    processStarted = true;
                    obsProc = p;
                }

                obsRemote = new OBS_Remote(port, doLogSocketMessages:debugLogSocketMessages);//, placeholderPassword);
                Task.Run(async () =>
                {
                    // Connect to the OBS program.
                    await obsRemote.ConnectAsync();
                    
                    // Set scene.
                    
                    /*
                    await obsRemote.SendMessageAsync("{\"op\":6,\"d\":" +
                                                         "{\"requestType\":\"SetCurrentScene\"," +
                                                         "\"requestId\":\"testid\"," +
                                                         "\"requestData\":"+
                                                             "{\"sceneName\":\"VR capture\"}"+
                                                         "}"+
                                                     "}");
                                                     */
                    //var reqStr = @"{""op"": 6, ""d"": { ""requestType"": ""SetCurrentScene"", ""requestId"": ""f819dcf0-89cc-11eb-8f0e-382c4ac93b9c"", ""requestData"": { ""sceneName"": ""Scene 12"" } }}";
                    var reqStr =
                        //@"{""op"": 6, ""d"": { ""requestType"": ""GetSceneList"", ""requestId"": ""f819dcf0-89cc-11eb-8f0e-382c4ac93b9c""}}";//, ""requestData"": { ""sceneName"": ""Scene 12"" } }}";
                        //@"{""op"": 1, ""d"": { ""rpcVersion"": 1 , ""authentication"": """", ""eventSubscriptions"":""""} }";
                        //@"{""request-type"":""GetSceneList"",""message-id"":""helloWorld""}";
                        @"{""request-type"":""SetCurrentScene"",""scene-name"":""VR capture"",""message-id"":""helloWorld""}";
                        //This worked!!!!!!
                    //https://github.com/obsproject/obs-websocket/blob/f3d5cfbd1819edb4bb9f711bba8ea6ecee49bca0/docs/generated/protocol.md#general-1
                    //await obsRemote.SendMessageAsync(reqStr);
                    //await obsRemote.SendCommand("StopRecording");
                    await obsRemote.SendCommand("SetCurrentProfile", new[] { ("profile-name", obsProfileName) });
                    await obsRemote.SendCommand("SetCurrentSceneCollection", new[] { ("sc-name", obsSceneCollectionName) });
                    await obsRemote.SendCommand("SetCurrentScene",new [] {("scene-name",obsSceneName)});
                    //var parameters = new [] { ("rec-folder", loggingDirectory.Replace('\\','/')) };
                    Debug.Log("[ScreencapLogger_OBS] Setup complete: ready to record!");
                });
                    
            }
        }

        private Process FindRunningOBSProcess()
        {
            if (Process.GetProcessesByName("obs64").Length > 0)
            {
                return Process.GetProcessesByName("obs64")[0];
            }
            return null;
        }

        private Process StartOBSProcess()
        {
            // OBS start options: https://obsproject.com/wiki/Launch-Parameters
            // ?possible route based on https://ianmorrish.wordpress.com/2017/05/20/automating-obs-from-powershell-script/
            var process = new Process();
            process.StartInfo.FileName = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe";
            process.StartInfo.Arguments = "--profile \"test_profile\" " +
                                          "--scene \"VR capture\" " +
                                          $"--websocket_port {port} " +
                                          "--websocket_debug 1 " + 
                                          "--websocket_password " + " " +
                                          "--minimize-to-tray ";
                                          //"--startrecording";
            // Do not skip setting working directory for OBS.
            process.StartInfo.WorkingDirectory = @"C:\Program Files\obs-studio\bin\64bit\";
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.OutputDataReceived += (sender, args) => Debug.Log(args);
            process.Start();
            processStarted = true;
            Debug.Log("[ScreencapLogger_OBS] Started OBS process.",this);
            return process;
        }

        private void OnApplicationQuit()
        {
            ForceFileFlush(0f);
        }


        protected override void ForceFileFlush()
        {
            ForceFileFlush(2f);
        }
        protected void ForceFileFlush(float seconds_delay)
        {
            if (hasFlushedToExit) return;
            hasFlushedToExit = true;
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds_delay));
                await obsRemote.SendCommand("StopRecording");
                obsRemote?.Dispose();
                //Need to send a stop recording (and close?) command over websockets here:
                if (obsProc!= null && !obsProc.HasExited && !debugDoNotManageRunningOBSProcess)
                {
                    Debug.Log("[ScreencapLogger_OBS] closing OBS process.");
                    obsProc.CloseMainWindow();
                    obsProc.Close();
                }
                obsProc?.Dispose();
            });
        }

        //Ideally this would grab the extension from the websocket, but right now communication is pretty one-way.
        protected override string filetype => ".mkv";

        public override string loggerNameForMetadata => "Screencap Logger";
        protected override string specificLoggingDirectory => Path.Combine(loggingDirectory,"Screencaps");
        
        public override void StartLogging(string cohort, string participant, string trial)
        {
            this.cohort = cohort;
            this.participant = participant;
            this.trial = trial;
            StartLogging();
        }

        public override void StartLogging()
        {
            //NB: need to compute and pass into task, can't compute in task.
            var recFolder = Path.GetDirectoryName(completeLogFilePath).Replace('\\','/');
            var fileName = Path.GetFileNameWithoutExtension(completeLogFilePath);
            // Order matters here, so all three commands are in one task.
            Task.Run(async () =>
            {
                await obsRemote.SendCommand("SetRecordingFolder",
                    new[] { ("rec-folder", recFolder) }); //loggingDirectory.Replace('\\','/')) });
                await obsRemote.SendCommand("SetFilenameFormatting", new[] { ("filename-formatting", fileName) });
                await obsRemote.SendCommand("StartRecording");
            });
        }
    }

}