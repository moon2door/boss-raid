using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BossRaid
{
    public sealed class BossRaidWebSocketClient : MonoBehaviour
    {
        [SerializeField] private string bridgeUrl = "ws://127.0.0.1:8765/ws";
        [SerializeField] private string bridgeStateUrl = "http://127.0.0.1:8765/state";
        [SerializeField] private string bridgeCommandUrl = "http://127.0.0.1:8765/command";
        [SerializeField] private float reconnectDelaySeconds = 2f;
        [SerializeField] private float httpPollSeconds = 1f;

        public event Action<string> StatusChanged;

        public string BridgeUrl => bridgeUrl;
        public string StatusLabel { get; private set; } = "DISCONNECTED";

        private readonly ConcurrentQueue<string> pendingMessages = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> pendingStatuses = new ConcurrentQueue<string>();
        private BossRaidStateStore stateStore;
        private CancellationTokenSource cancellation;
        private ClientWebSocket socket;
        private bool loggedHttpSuccess;
        private float ignoreIncomingUntil;

        private void Awake()
        {
            stateStore = GetComponent<BossRaidStateStore>();
        }

        private void Start()
        {
            Debug.Log($"BossRaid bridge client starting. WebSocket={bridgeUrl}, HTTP={bridgeStateUrl}");
            cancellation = new CancellationTokenSource();
            _ = RunConnectionLoop(cancellation.Token);
            StartCoroutine(PollStateLoop());
        }

        private void Update()
        {
            while (pendingStatuses.TryDequeue(out var status))
            {
                StatusLabel = status;
                StatusChanged?.Invoke(StatusLabel);
            }

            while (pendingMessages.TryDequeue(out var json))
            {
                if (ShouldIgnoreIncomingState())
                {
                    continue;
                }

                stateStore.ApplyJson(json);
            }
        }

        public void HoldIncomingState(float seconds)
        {
            ignoreIncomingUntil = Mathf.Max(ignoreIncomingUntil, Time.realtimeSinceStartup + Mathf.Max(0f, seconds));
        }

        public void SendCommandJson(string commandJson)
        {
            if (string.IsNullOrWhiteSpace(commandJson))
            {
                return;
            }

            StartCoroutine(PostCommand(commandJson));
        }

        private async Task RunConnectionLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var opened = false;
                try
                {
                    SetStatus("CONNECTING");
                    using (socket = new ClientWebSocket())
                    {
                        await socket.ConnectAsync(new Uri(bridgeUrl), token);
                        opened = true;
                        SetStatus("CONNECTED");
                        await ReceiveLoop(socket, token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    SetStatus($"RETRYING: {exception.GetType().Name}");
                    await DelayBeforeReconnect(token);
                }
                finally
                {
                    socket = null;
                    if (!token.IsCancellationRequested && opened)
                    {
                        SetStatus("DISCONNECTED");
                    }
                }
            }
        }

        private async Task ReceiveLoop(ClientWebSocket activeSocket, CancellationToken token)
        {
            var buffer = new byte[8192];

            while (activeSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using (var stream = new MemoryStream())
                {
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await activeSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await activeSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", token);
                            return;
                        }

                        stream.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        pendingMessages.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
                    }
                }
            }
        }

        private async Task DelayBeforeReconnect(CancellationToken token)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Mathf.Max(0.5f, reconnectDelaySeconds)), token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private IEnumerator PollStateLoop()
        {
            var wait = new WaitForSeconds(Mathf.Max(0.25f, httpPollSeconds));
            while (enabled)
            {
                using (var request = UnityWebRequest.Get(bridgeStateUrl))
                {
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success && !string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        if (!ShouldIgnoreIncomingState())
                        {
                            stateStore.ApplyJson(request.downloadHandler.text);
                        }

                        if (!loggedHttpSuccess)
                        {
                            Debug.Log("BossRaid state received via HTTP polling.");
                            loggedHttpSuccess = true;
                        }

                        if (StatusLabel != "CONNECTED")
                        {
                            SetStatus("HTTP POLLING");
                        }
                    }
                }

                yield return wait;
            }
        }

        private IEnumerator PostCommand(string commandJson)
        {
            var body = Encoding.UTF8.GetBytes(commandJson);
            using (var request = new UnityWebRequest(bridgeCommandUrl, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"BossRaid bridge command failed: {request.error}");
                }
            }
        }

        private bool ShouldIgnoreIncomingState()
        {
            return Time.realtimeSinceStartup < ignoreIncomingUntil;
        }

        private void SetStatus(string status)
        {
            pendingStatuses.Enqueue(status);
        }

        private void OnDestroy()
        {
            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            cancellation.Dispose();
            cancellation = null;
        }
    }
}
