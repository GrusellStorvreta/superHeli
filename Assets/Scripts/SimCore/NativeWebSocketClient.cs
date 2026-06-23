#if NATIVEWEBSOCKET
using System;
using UnityEngine;
using NativeWebSocket;

namespace SimCore
{
    public class NativeWebSocketClient : IWebSocketClient
    {
        private WebSocket ws;
        public Action<string> OnMessage { get; set; }

        public bool IsConnected => ws != null && ws.State == WebSocketState.Open;

        public async void Connect(string url)
        {
            try
            {
                ws = new WebSocket(url);

                ws.OnOpen += () => { Debug.Log("NativeWebSocket: Connected to " + url); };
                ws.OnError += (e) => { Debug.LogError("NativeWebSocket error: " + e); };
                ws.OnClose += (e) => { Debug.Log("NativeWebSocket closed: " + e); };
                ws.OnMessage += (bytes) => {
                    var txt = System.Text.Encoding.UTF8.GetString(bytes);
                    OnMessage?.Invoke(txt);
                };

                await ws.Connect();
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to connect NativeWebSocket: " + ex.Message);
            }
        }

        public void Send(string message)
        {
            if (ws != null)
            {
                ws.SendText(message);
            }
        }

        public void Close()
        {
            if (ws != null)
            {
                ws.Close();
            }
        }

        public void DispatchMessageQueue()
        {
            WebSocket.DispatchMessageQueue();
        }
    }
}
#endif
