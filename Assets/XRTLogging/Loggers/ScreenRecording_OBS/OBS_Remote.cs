using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Security.Cryptography.SHA256;
using Random = UnityEngine.Random;


namespace XRTLogging
{
    public class OBS_Remote
    {
        // Information: https://stackoverflow.com/questions/30523478/connecting-to-websocket-using-c-sharp-i-can-connect-using-javascript-but-c-sha
        public int port;
        public string password;
        public ClientWebSocket socket;
        public bool debugLogSocketMessages;
        private CancellationTokenSource cancellationTokenSource;
        private CancellationTokenSource retryCancellationTokenSource;
        private int retryMilliseconds = 2500;
        public static int ReceiveBufferSize { get; set; } = 8192;
        private byte[] receiveBuffer = new byte[ReceiveBufferSize];
        
        private delegate void OBSResponseEvent(string s);
        private OBSResponseEvent responseEvent;

        public bool isCurrentlyConnected => socket.State == WebSocketState.Open;
        
        public OBS_Remote(int port, string password = null, bool doLogging = false, bool doLogSocketMessages = false)
        {
            this.port = port;
            this.password = password;
            if (doLogging) responseEvent += Debug.Log;
            debugLogSocketMessages = doLogSocketMessages;
        }

        public async Task ConnectAsync(bool doNotRetry = false)
        {
            // If there's already a socket, clear it if it's no longer open:
            if (socket != null)
            {
                if (socket.State == WebSocketState.Open) return;
                socket.Dispose();
            }

            if (!string.IsNullOrEmpty(password))
            {
                Debug.LogWarning("[OBS_Remote] Password connections are not implemented!");
            }
            
            socket = new ClientWebSocket();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = new CancellationTokenSource();
            UriBuilder uriBuilder = new UriBuilder();
            uriBuilder.Host = "localhost";
            uriBuilder.Port = port;
            uriBuilder.Scheme = "ws";
            Debug.Log($"[OBS_Remote] Attempting to connect to OBS websocket at {uriBuilder.Uri}");
            if (doNotRetry) await socket.ConnectAsync(uriBuilder.Uri, cancellationTokenSource.Token);
            else
            {
                retryCancellationTokenSource?.Dispose();
                retryCancellationTokenSource = new CancellationTokenSource();
                await Task.Run(() =>
                {
                    // Retry until cancellation token or connected.
                    while (true)
                    {
                        if (socket.ConnectAsync(uriBuilder.Uri, cancellationTokenSource.Token)
                            .Wait(retryMilliseconds)) break;
                        retryCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        socket.Abort();
                        socket = new ClientWebSocket();
                        Debug.Log( $"[OBS_Remote] {uriBuilder.Uri} - connection timed out, retrying");
                    }
                    Debug.Log( $"[OBS_Remote] {uriBuilder.Uri} - connected");
                    return Task.CompletedTask;
                },retryCancellationTokenSource.Token);
            }
            Debug.Log($"[OBS_Remote] Connected to {uriBuilder.Uri}");
            Debug.Log($"[OBS_Remote] Starting receive loop.");
            await Task.Factory.StartNew(ReceiveLoop,
                cancellationTokenSource.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            Debug.Log($"[OBS_Remote] Receive loop started.");
        }

        private async Task ReceiveLoop()
        {
            var loopToken = cancellationTokenSource.Token;
            WebSocketReceiveResult receiveResult;
            MemoryStream outputStream = null;
            var bufferSegment = new ArraySegment<byte>(receiveBuffer);
            try
            {
                while (!loopToken.IsCancellationRequested)
                {
                    outputStream = new MemoryStream(ReceiveBufferSize);
                    do
                    {
                        //Debug.Log("awaiting receiveasync");
                        receiveResult = await socket.ReceiveAsync(bufferSegment, cancellationTokenSource.Token);
                        //Debug.Log("received result");
                        if (receiveResult.MessageType != WebSocketMessageType.Close)
                            outputStream.Write(receiveBuffer, 0, receiveResult.Count);
                    } while (!receiveResult.EndOfMessage);

                    if (receiveResult.MessageType == WebSocketMessageType.Close) break;
                    outputStream.Position = 0;
                    ResponseReceived(outputStream);
                }

                Debug.Log("stopping because cancellation requested...");
            }
            catch (TaskCanceledException)
            {
                Debug.LogError("[OBS_Remote] receive task cancelled");
            }
            finally
            {
                outputStream?.Dispose();
            }
        }
        
        public async Task DisconnectAsync()
        {
            Debug.Log("[OBS_Remote] Disconnecting OBS Websocket.");
            retryCancellationTokenSource?.Cancel();
            if (socket is null) return;
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.Connecting)
            {
                cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
                await socket.CloseOutputAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,"",CancellationToken.None);
            }
        }

        public async Task SendMessageAsync(string message)
        {
            if (socket.State != WebSocketState.Open)
            {
                Debug.Log($"[OBS_Remote] Cannot send: state = {socket.State} (message = {message}).");
                return;
            }
            //handle serializing requests and deserializing responses, handle matching responses to requests.
            if (debugLogSocketMessages) Debug.Log($"[OBS_Remote] Send: {message}");
            ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            await socket.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);

        }

        private void ResponseReceived(Stream inputStream)
        {
            if (inputStream is null) return;
            //handle deserializing responses and matching them to the requests.
            inputStream.Position = 0;
            using (StreamReader reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                //Debug.Log("[OBS_remote] Response: " + reader.ReadToEnd());
                var resp = reader.ReadToEnd();
                if (resp.Contains("error"))
                {
                    Debug.LogError($"[OBS_Remote] Receive: {resp}");
                }
                else
                {
                    if (debugLogSocketMessages) Debug.Log($"[OBS_Remote] Receive: {resp}");
                }
                //responseEvent?.Invoke("[OBS_remote] Response: " + reader.ReadToEnd());
            }
            // Make sure to dispose the inputStream!
            inputStream?.Dispose();
        }

        public async Task SendCommand(string requestType, (string, string)[] parameters = null)
        {
            var message = buildOBSMessage(requestType, parameters);
            Debug.Log($"[OBS_Remote] Send: {message}");
            SendMessageAsync(message);
        }

        //Placeholder - once OBS Websocket 5.0.0 is out of beta, will need to switch to a fully different
        // API, and at that time, ideally just use a JSON lib.
        // There are a lot of opportunities to get really fancy here - I just needed something that worked.
        // If you can set up asyncs that wait for the server response OK or throw an error, then that'd be sweet.
        private int messageNumber = 0;
        private string buildOBSMessage(string requestType, (string, string)[] parameters = null)
        {
            var sb = new StringBuilder();
            sb.Append(@"{""request-type"":""");
            sb.Append(requestType);
            sb.Append(@""",");
            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    /*
                    if (int.TryParse(p.Item2, out int iVal))
                    {
                        sb.Append($"\"{p.Item1}\":{iVal},");
                    }

                    else if (bool.TryParse(p.Item2, out bool bVal))
                    {
                        sb.Append($"\"{p.Item1}\":{bVal},");
                    }
                    else
                    {
                        sb.Append($"\"{p.Item1}\":\"{p.Item2}\",");
                    }
                    */
                    sb.Append($"\"{p.Item1}\":\"{p.Item2}\",");
                }
            }
            sb.Append(@"""message-id"":""XRT-OBSRemote-");
            sb.Append(++messageNumber);
            sb.Append(@"""}");
            return sb.ToString();
        }

        public Task Dispose() => Task.Run(async () => await DisconnectAsync());

    }
}