#!/usr/bin/env node
/**
 * wait-backend.mjs
 * Poll the backend /api/workflows endpoint until it responds or timeout.
 */
import http from 'http';

const HOST = process.env.MOZ_BACKEND_HOST || 'localhost';
const PORT = process.env.MOZ_BACKEND_PORT || '8000';
const PATH = process.env.MOZ_BACKEND_WORKFLOWS_PATH || '/api/workflows';
const TIMEOUT_MS = parseInt(process.env.MOZ_BACKEND_WAIT_TIMEOUT || '45000', 10);
const INTERVAL_MS = parseInt(process.env.MOZ_BACKEND_WAIT_INTERVAL || '1500', 10);

const deadline = Date.now() + TIMEOUT_MS;

function probe() {
  return new Promise((resolve) => {
    const req = http.get({ host: HOST, port: PORT, path: PATH, timeout: 5000 }, (res) => {
      const ok = res.statusCode && res.statusCode >= 200 && res.statusCode < 500; // treat 4xx as reachable
      if (ok) {
        res.resume();
        return resolve({ ok: true, code: res.statusCode });
      }
      res.resume();
      resolve({ ok: false, code: res.statusCode });
    });
    req.on('error', () => resolve({ ok: false }));
    req.on('timeout', () => { req.destroy(); resolve({ ok: false }); });
  });
}

(async () => {
  let attempt = 0;
  while (Date.now() < deadline) {
    attempt++;
    const result = await probe();
    if (result.ok) {
      console.log(`✅ Backend reachable (status ${result.code}) after ${attempt} attempt(s).`);
      process.exit(0);
    }
    console.log(`⏳ Waiting for backend (attempt ${attempt}) ...`);
    await new Promise(r => setTimeout(r, INTERVAL_MS));
  }
  console.error(`❌ Backend not reachable within ${TIMEOUT_MS}ms.`);
  process.exit(1);
})();