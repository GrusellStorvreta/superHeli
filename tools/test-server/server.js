const WebSocket = require('ws');
const port = process.env.PORT || 8765;

const wss = new WebSocket.Server({ port });

wss.on('connection', (ws) => {
  console.log('client connected');

  ws.on('message', (message) => {
    try {
      console.log('from client:', message.toString());
    } catch (e) {
      console.log('from client (binary)');
    }
  });

  const interval = setInterval(() => {
    const frame = {
      version: '1.0',
      t: Date.now() / 1000,
      entities: []
    };
    if (ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(frame));
  }, 200);

  ws.on('close', () => {
    clearInterval(interval);
    console.log('client disconnected');
  });
});

wss.on('listening', () => console.log('WebSocket test server listening on port', port));

wss.on('error', (err) => console.error('WebSocket server error:', err));
