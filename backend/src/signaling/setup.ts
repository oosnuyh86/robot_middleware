import { Server } from "http";
import { WebSocketServer, WebSocket } from "ws";

export function setupRelay(server: Server) {
  const wss = new WebSocketServer({ server, path: "/ws" });

  const clients = new Map<WebSocket, { role: string }>();

  wss.on("connection", (ws, req) => {
    const url = new URL(req.url!, `http://${req.headers.host}`);
    const role = url.searchParams.get("role") || "unknown";
    clients.set(ws, { role });

    console.log(`[Relay] ${role} connected (${clients.size} total)`);

    broadcast(wss, ws, JSON.stringify({ type: "CONNECTED", role }));

    ws.on("message", (data) => {
      const msg = data.toString();
      console.log(`[Relay] ${role} → ${msg.substring(0, 100)}`);
      broadcast(wss, ws, msg);
    });

    ws.on("close", () => {
      clients.delete(ws);
      console.log(`[Relay] ${role} disconnected (${clients.size} remaining)`);
      broadcast(wss, ws, JSON.stringify({ type: "DISCONNECTED", role }));
    });
  });

  console.log("[Relay] WebSocket relay server attached at /ws");
}

function broadcast(wss: WebSocketServer, sender: WebSocket, msg: string) {
  for (const client of wss.clients) {
    if (client !== sender && client.readyState === WebSocket.OPEN) {
      client.send(msg);
    }
  }
}
