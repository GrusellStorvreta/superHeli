using System;
using UnityEngine;

namespace SimCore
{
    // Lightweight websocket client abstraction (global to SimCore)
    public interface IWebSocketClient
    {
        void Connect(string url);
        void Send(string message);
        void Close();
        bool IsConnected { get; }
        // Optional callback to receive text messages
        Action<string> OnMessage { get; set; }
        // Called each Update to allow platform-specific dispatching (WebGL / NativeWebSocket requires this)
        void DispatchMessageQueue();
    }

    // No-op implementation used until a real WebSocket package is imported.
    // Keeps the code Unity-compile-safe.
    public class NoopWebSocketClient : IWebSocketClient
    {
        public Action<string> OnMessage { get; set; }
        public bool IsConnected => false;
        public void Connect(string url) { Debug.Log("NoopWebSocketClient: Connect called — no websocket package installed."); }
        public void Send(string message) { /* no-op */ }
        public void Close() { /* no-op */ }
        public void DispatchMessageQueue() { /* no-op */ }
    }
}
