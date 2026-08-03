/**
 * spawn-ledger.js — Append-only spawn registry + run-liveness marker.
 *
 * 每个套件根进程 import 本模块时写入 <runId>.run 存活标记（独占创建，
 * 先到先得）。收割器据此判断台账属主是否活着：活着则不碰，死了才收。
 */

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { procStartTime } from "./process-lifecycle.js";
import { LEDGER_ENTRY_TTL_MS } from "../e2e/time-budget.js";

const SAFE_ID_RE = /^[A-Za-z0-9_.-]+$/;
function sanitizeRunId(raw) {
  if (typeof raw === "string" && SAFE_ID_RE.test(raw) && raw.length <= 128) return raw;
  return `${Date.now()}-${process.pid}-${Math.random().toString(36).slice(2, 8)}`;
}

export const LEDGER_DIR = path.join(os.tmpdir(), "wanxiang-ledger");
export const RUN_ID = sanitizeRunId(process.env.WANXIANG_RUN_ID);

const LEDGER_FILE = path.join(LEDGER_DIR, `${RUN_ID}.jsonl`);

function ensureDir() {
  try { fs.mkdirSync(LEDGER_DIR, { recursive: true }); } catch {}
}

// 运行存活标记：wx 独占创建，同一 runId 下第一个 import 的进程（即套件根）获胜。
// 后续同 runId 的 import 不覆盖——根进程的生死才是属主生死。
try {
  ensureDir();
  const marker = {
    pid: process.pid,
    startTime: process.platform === "linux" ? procStartTime(process.pid) : null,
    runId: RUN_ID,
    startedAt: Date.now(),
  };
  fs.writeFileSync(path.join(LEDGER_DIR, `${RUN_ID}.run`), JSON.stringify(marker) + "\n", { flag: "wx" });
} catch {}

export function recordSpawn(pid, cmd, ttlMs = LEDGER_ENTRY_TTL_MS) {
  if (!pid) return;
  ensureDir();
  const entry = {
    pid,
    pgid: pid, // all our spawns are detached => pid is group leader
    startTime: process.platform === "linux" ? procStartTime(pid) : null,
    cmd: String(cmd).slice(0, 300),
    runId: RUN_ID,
    expiresAt: Date.now() + ttlMs,
  };
  try { fs.appendFileSync(LEDGER_FILE, JSON.stringify(entry) + "\n"); } catch {}
}

export function recordExit(pid) {
  if (!pid) return;
  ensureDir();
  try { fs.appendFileSync(LEDGER_FILE, JSON.stringify({ pid, runId: RUN_ID, exited: true }) + "\n"); } catch {}
}
