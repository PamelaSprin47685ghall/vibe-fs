#!/usr/bin/env node
/**
 * reaper.mjs — Cross-run reaper, concurrency-safe ("sweep your own doorstep").
 *
 * 收割判据：属主 run 已死（存活标记缺失/pid 死亡/startTime 不复用匹配）。
 * 属主活着的 run，其台账与子进程一律不碰——并发套件互不干扰。
 *
 * 1. 台账：属主已死 → 逐条 startTime 核验后收；属主活着 → 整个跳过。
 * 2. /proc 扫描：无台账孤儿（spawn 后未及登记就崩溃的窗口）→ 读其
 *    environ 的 WANXIANG_RUN_ID 判归属；无 env 的裸进程才用 5s 年龄兜底。
 * 3. 内存预检不变。
 */

import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";
import { LEDGER_DIR, RUN_ID } from "./spawn-ledger.js";
import { pidIsAlive, procStartTime } from "./process-lifecycle.js";

function parsePositiveInt(value, fallback, name) {
  const n = Number(value);
  if (!Number.isFinite(n) || Number.isNaN(n) || n <= 1 || n !== Math.floor(n)) {
    console.error(`REAPER: invalid ${name}=${value}; using fallback ${fallback}`);
    return fallback;
  }
  return n;
}

const FORCE = process.argv.includes("--force");
const MIN_FREE_MB = parsePositiveInt(process.env.REAPER_MIN_FREE_MB, 2048, "REAPER_MIN_FREE_MB");
// 只兜底"没继承到 WANXIANG_RUN_ID 的裸进程"；归属判断才是主逻辑，年龄无需长。
const ORPHAN_MIN_AGE_MS = parsePositiveInt(process.env.REAPER_ORPHAN_MIN_AGE_MS, 5000, "REAPER_ORPHAN_MIN_AGE_MS");
const ORPHAN_MARKERS = ["oc-e2e-", "tests-mjs/", "wanxiang-ledger", ".wanxiangshu-next"];

let reaped = 0;

/** 属主 run 是否活着：标记存在 + pid 活着 + startTime 指纹未变（防 PID 复用）。 */
function isRunAlive(runId) {
  if (!runId) return false;
  try {
    const m = JSON.parse(fs.readFileSync(path.join(LEDGER_DIR, `${runId}.run`), "utf8"));
    if (!m.pid || !pidIsAlive(m.pid)) return false;
    if (m.startTime && procStartTime(m.pid) !== m.startTime) return false; // pid 已复用
    return true;
  } catch {
    return false; // 无标记 = 属主已死或从未登记 = 可收
  }
}

/** 进程命令行/环境里登记的属主 runId。 */
function ownerRunId(pid) {
  try {
    const env = fs.readFileSync(`/proc/${pid}/environ`, "utf8");
    const m = env.match(/WANXIANG_RUN_ID=([^\0]+)/);
    return m ? m[1] : null;
  } catch {
    return null;
  }
}

function killGroupVerified(pid, startTime) {
  try {
    if (startTime && procStartTime(pid) !== startTime) return false; // recycled pid
    process.kill(-pid, "SIGKILL");
    process.kill(pid, "SIGKILL");
    reaped++;
    return true;
  } catch {
    return false;
  }
}

function reapLedger() {
  let files = [];
  try { files = fs.readdirSync(LEDGER_DIR).filter((f) => f.endsWith(".jsonl")); } catch { return; }
  for (const file of files) {
    const ownerId = file.replace(/\.jsonl$/, "");
    if (ownerId === RUN_ID) continue;          // 自己的雪自己扫，但不在启动时扫
    if (isRunAlive(ownerId)) continue;         // 属主活着：一根手指不碰

    const full = path.join(LEDGER_DIR, file);
    const exitedPids = new Set();
    const entries = [];
    try {
      for (const line of fs.readFileSync(full, "utf8").split("\n")) {
        if (!line.trim()) continue;
        try {
          const e = JSON.parse(line);
          if (e.exited) exitedPids.add(e.pid);
          else entries.push(e);
        } catch {}
      }
    } catch { continue; }

    for (const e of entries) {
      if (exitedPids.has(e.pid)) continue;
      if (!pidIsAlive(e.pid)) continue;
      if (e.startTime && procStartTime(e.pid) !== e.startTime) continue; // pid recycled
      killGroupVerified(e.pid, e.startTime);
    }
    try { fs.unlinkSync(full); } catch {}
    try { fs.unlinkSync(path.join(LEDGER_DIR, `${ownerId}.run`)); } catch {}
  }
}

function sweepOrphans() {
  if (process.platform === "win32") return;
  let ps;
  try {
    ps = execSync(`ps -eo pid=,etimes=,args= 2>/dev/null`, { encoding: "utf8", maxBuffer: 16 * 1024 * 1024 });
  } catch { return; }
  for (const line of ps.split("\n")) {
    const m = line.trim().match(/^(\d+)\s+(\d+)\s+(.*)$/);
    if (!m) continue;
    const [, pidStr, etimesStr, cmd] = m;
    const pid = Number(pidStr);
    if (pid === process.pid || pid === process.ppid) continue;
    if (!ORPHAN_MARKERS.some((mark) => cmd.includes(mark))) continue;

    const owner = ownerRunId(pid);
    if (owner) {
      if (owner === RUN_ID) continue;        // 本 run 的进程
      if (isRunAlive(owner)) continue;       // 别家活着的 run 的进程：不碰
      killGroupVerified(pid, procStartTime(pid)); // 属主已死：替它收尸
      continue;
    }
    // 无 env 的裸进程：年龄兜底（5s，仅覆盖 spawn 后未及登记的窗口）
    if (Number(etimesStr) * 1000 < ORPHAN_MIN_AGE_MS) continue;
    killGroupVerified(pid, procStartTime(pid));
  }
}

function checkMemory() {
  try {
    const meminfo = fs.readFileSync("/proc/meminfo", "utf8");
    const avail = Number(meminfo.match(/MemAvailable:\s+(\d+)/)?.[1] || 0) / 1024;
    if (avail < MIN_FREE_MB) {
      const msg = `REAPER: only ${Math.round(avail)}MB available (< ${MIN_FREE_MB}MB). ` +
        `Reaped ${reaped} leftovers; free memory or re-run with --force.`;
      if (!FORCE) { console.error(msg); process.exit(2); }
      console.error(msg + " (continuing due to --force)");
    }
  } catch {}
}

reapLedger();
sweepOrphans();
checkMemory();
if (reaped > 0) console.log(`REAPER: killed ${reaped} leftover process(es) from dead runs.`);
