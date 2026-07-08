WebSocket test server for superHeli

Prerequisites:
- Node.js (14+)

Install dependencies and run:

  cd tools/test-server
  npm init -y
  npm install ws
  node server.js

By default the server listens on ws://localhost:8765 and will echo a periodic empty Frame JSON every 200ms. It will also log messages received from clients.

Use PORT environment variable to change the port, e.g. PORT=9000 node server.js
