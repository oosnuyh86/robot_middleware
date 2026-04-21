import { createServer } from "http";
import express from "express";
import cors from "cors";
import mongoose from "mongoose";

import { env } from "./config/env.js";
import { errorHandler } from "./middleware/errorHandler.js";
import { setupRelay } from "./signaling/setup.js";

import companiesRouter from "./routes/companies.js";
import professionalsRouter from "./routes/professionals.js";
import jobsRouter from "./routes/jobs.js";
import recordsRouter from "./routes/records.js";
import uploadsRouter from "./routes/uploads.js";
import alignmentRouter from "./routes/alignment.js";

const app = express();
const server = createServer(app);

// Middleware
app.use(cors());
app.use(express.json());

// API Routes
app.use("/api/companies", companiesRouter);
app.use("/api/professionals", professionalsRouter);
app.use("/api/jobs", jobsRouter);
app.use("/api/records", recordsRouter);
app.use("/api/uploads", uploadsRouter);
app.use("/api/alignment", alignmentRouter);

// Health check
app.get("/api/health", (_req, res) => {
  res.json({ status: "ok" });
});

// API index
app.get("/api", (_req, res) => {
  res.json({
    name: "robot_middleware backend",
    status: "ok",
    endpoints: [
      "/api/health",
      "/api/companies",
      "/api/professionals",
      "/api/jobs",
      "/api/records",
      "/api/uploads",
      "/api/alignment",
    ],
    ws: "/ws",
  });
});

// Error handler (must be last)
app.use(errorHandler);

// WebSocket Relay
setupRelay(server);

// Start
async function start() {
  try {
    console.log(`[MongoDB] Connecting to ${env.MONGO_URI}...`);
    await mongoose.connect(env.MONGO_URI, {
      serverSelectionTimeoutMS: 5000,
    });
    console.log("[MongoDB] Connected");

    server.listen(env.PORT, () => {
      console.log(`[Server] Listening on port ${env.PORT}`);
      console.log(`[API] http://localhost:${env.PORT}/api`);
    });
  } catch (err) {
    console.error("[Startup] Failed:", err);
    process.exit(1);
  }
}

start();
