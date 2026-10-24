#!/usr/bin/env node
// CUT-36 — opt-in REAL omp session smoke.
//
// What this does: starts a REAL `omp` (18.12.4) process with the built product
// extension bundle plus a smoke-owned read-only observer extension, lets the
// parent turn drive the product's delegation tool (which forks a child session
// and drives it to completion inside that same tool call), then asserts on the
// facts the HOST produced (provider payloads, session files, tool executions)
// and on the product's ON-DISK state artifacts. The product writes no smoke
// report and none is consulted: every fact is host-observed or on disk.
//
// Opt-in: the run aborts unless CUT36_OMP_REAL_SMOKE=1 is set. Nothing in the
// default gates runs this script; default gates stay offline.
//
// Usage:
//   CUT36_OMP_REAL_SMOKE=1 \
//   CUT36_OMP_BUNDLE=<repo>/dist/Composition/Omp/OmpExtension.js \
//   CUT36_OMP_WORKSPACE=<repo> \
//   node proposals/host-neutral-interface/smoke/run-smoke.mjs
//
// Env:
//   CUT36_OMP_REAL_SMOKE=1   required opt-in flag (real model call)
//   CUT36_OMP_BUNDLE         path to the built omp extension bundle (default: <repo>/dist/Composition/Omp/OmpExtension.js)
//   CUT36_OMP_WORKSPACE      workspace root handed to omp --cwd (default: repo root)
//   CUT36_OMP_OBSERVER_OUT   observer JSONL path (default: <tmp>/cut36-omp-observer.jsonl)
//   CUT36_OMP_PROMPT         prompt template for the real turn; {nonce} is substituted
//   CUT36_OMP_TIMEOUT_MS     hard timeout for the omp run (default: 600000)

import { spawn } from "node:child_process"
import crypto from "node:crypto"
import fs from "node:fs"
import os from "node:os"
import path from "node:path"
import { fileURLToPath } from "node:url"
import { assertSmoke, listSessionFiles, readJsonl, snapshotJournal } from "./assert-smoke.mjs"

const HERE = path.dirname(fileURLToPath(import.meta.url))
const REPO = path.resolve(HERE, "../../..")

const OPT_IN = "CUT36_OMP_REAL_SMOKE"
const bundle = process.env.CUT36_OMP_BUNDLE ?? path.join(REPO, "dist/Composition/Omp/OmpExtension.js")
const workspace = process.env.CUT36_OMP_WORKSPACE ?? REPO
const observerOut = process.env.CUT36_OMP_OBSERVER_OUT ?? path.join(os.tmpdir(), "cut36-omp-observer.jsonl")
const observerExtension = path.join(HERE, "observer-extension.mjs")
const timeoutMs = Number(process.env.CUT36_OMP_TIMEOUT_MS ?? 600_000)
// Which host tools count as "the product's own tools". The product's tool is
// `wxs_deliver` (HostPlugin.fs:191) — nothing else does. A broad pattern
// would let an unrelated built-in host tool (e.g. `task`) satisfy the
// product-artifact fact, so the default is the product's exact name.
const toolPattern = new RegExp(process.env.CUT36_OMP_TOOL_PATTERN ?? "^wxs_deliver$", "i")

// A run-specific nonce: it must appear in the child's DELIVERED user message
// and in the join result the parent observes. Nothing else can fake it.
const nonce = process.env.CUT36_OMP_NONCE ?? `cut36-${crypto.randomBytes(8).toString("hex")}`
const promptTemplate =
  process.env.CUT36_OMP_PROMPT ??
  "Use the product's delegation tool once to deliver the token {nonce} to a child session, then reply with the single word CANARY_DONE."
const prompt = promptTemplate.replaceAll("{nonce}", nonce)

const notes = []

// ── gate: explicit opt-in, real model spend ─────────────────────────────────
if (process.env[OPT_IN] !== "1") {
  console.error(
    `${OPT_IN} is not set. This smoke starts a REAL omp session and spends model quota.\n` +
      `It is opt-in by design. Re-run with ${OPT_IN}=1 (see root AGENTS.md).`,
  )
  process.exit(2)
}

if (!fs.existsSync(bundle)) {
  console.error(
    `bundle not found: ${bundle}\n` +
      `This smoke never compiles. Build the bundle first, through the build lock:\n` +
      `  flock /home/hermes/work/vfs-build.lock node scripts/build.mjs`,
  )
  process.exit(2)
}
if (!fs.existsSync(observerExtension)) {
  console.error(`observer extension missing: ${observerExtension}`)
  process.exit(2)
}

// ── prepare artefacts ────────────────────────────────────────────────────────
fs.rmSync(observerOut, { force: true })

const sessionsRoot = path.join(os.homedir(), ".omp", "agent", "sessions")
const sessionsBefore = new Set(listSessionFiles(sessionsRoot))

// The PRE-RUN journal snapshot. Ownership and durable work facts are only
// evidence for THIS run when they are newer than this snapshot; a stream
// left behind by an earlier run must not satisfy the run-scoped facts.
const journalBaseline = snapshotJournal(workspace)
notes.push(`journal baseline: ${journalBaseline.error ? `unavailable (${journalBaseline.error})` : JSON.stringify(journalBaseline.streams)}`)

// ── launch the REAL host ─────────────────────────────────────────────────────
const argv = ["-p", prompt, "-e", bundle, "-e", observerExtension, "--cwd", workspace, "--mode", "json"]
const env = { ...process.env, OMP_SMOKE_OBSERVER_OUT: observerOut }

notes.push(`omp argv: omp ${argv.map((a) => (a === prompt ? "<prompt>" : a)).join(" ")}`)
notes.push(`bundle:   ${bundle}`)
notes.push(`observer: ${observerOut}`)
notes.push(`nonce:    ${nonce}`)

// The host's own log file, when it wrote one: `~/.omp/logs/omp.<date>.<pid>.log`.
// The pid of OUR host process is preferred; otherwise the newest log file is
// the one the failing run wrote.
function findHostLog(pid) {
  const logsDir = path.join(os.homedir(), ".omp", "logs")
  if (!fs.existsSync(logsDir)) return null
  let files
  try {
    files = fs.readdirSync(logsDir).filter((f) => f.startsWith("omp.") && f.endsWith(".log"))
  } catch {
    return null
  }
  if (files.length === 0) return null
  const byPid = files.filter((f) => pid && f.includes(`.${pid}.`))
  const pool = byPid.length > 0 ? byPid : files
  const newest = pool
    .map((f) => ({ f, mtime: fs.statSync(path.join(logsDir, f)).mtimeMs }))
    .sort((a, b) => b.mtime - a.mtime)[0]
  return path.join(logsDir, newest.f)
}

function tailLines(text, count) {
  return String(text ?? "")
    .split("\n")
    .filter((l, i, arr) => l.trim() !== "" || i < arr.length - 1)
    .slice(-count)
    .join("\n")
}
const started = Date.now()
const result = await new Promise((resolve) => {
  const child = spawn("omp", argv, { env, stdio: ["ignore", "pipe", "pipe"] })
  let stdout = ""
  let stderr = ""
  child.stdout.on("data", (d) => {
    stdout += d.toString()
  })
  child.stderr.on("data", (d) => {
    stderr += d.toString()
  })
  const timer = setTimeout(() => child.kill("SIGKILL"), timeoutMs)
  child.on("close", (code, signal) => {
    clearTimeout(timer)
    resolve({ code, signal, stdout, stderr, pid: child.pid })
  })
})
const elapsedMs = Date.now() - started

const stdoutPath = path.join(os.tmpdir(), "cut36-omp-stdout.jsonl")
fs.writeFileSync(stdoutPath, result.stdout)
notes.push(`omp exit code=${result.code} signal=${result.signal ?? "none"} in ${elapsedMs} ms`)
notes.push(`omp stdout: ${stdoutPath}`)

// ── this run's parent session id (from the host's own session_start) ─────────
const observed = readJsonl(observerOut)
const parentSessionId = observed.find((o) => o.kind === "session_start")?.sessionId
if (!parentSessionId) {
  console.error("the host produced no session_start event — the product bundle did not load")
}
// ── host diagnostics ────────────────────────────────────────────────────────
// The bundle EXISTS (the missing-bundle case already exited 2 above), so any
// failure from here on is the HOST's failure. The host's own stderr tail and
// the tail of the host's log file are part of the failure MESSAGE, because
// the assertion list alone never says WHY the host did not do the thing
// (a slow first activation, a failed extension load, a provider error).
const hostFailed = result.code !== 0 || !!result.signal
const stderrTail = tailLines(result.stderr, 20)
const hostLog = findHostLog(result.pid)
let hostLogTail = ""
if (hostLog) {
  try {
    hostLogTail = tailLines(fs.readFileSync(hostLog, "utf8"), 20)
  } catch {
    hostLogTail = ""
  }
}
const hostDiagnostics = [
  `host exit code=${result.code} signal=${result.signal ?? "none"}`,
  stderrTail ? `--- omp stderr (last 20 lines) ---\n${stderrTail}` : "--- omp stderr (last 20 lines) ---\n(empty)",
  hostLog
    ? `--- ${hostLog} (last 20 lines) ---\n${hostLogTail}`
    : "--- host log (last 20 lines) ---\n(no host log file found under ~/.omp/logs)",
].join("\n")

// ── assertions over HOST-OBSERVED facts ──────────────────────────────────────
const newSessionFiles = listSessionFiles(sessionsRoot).filter((f) => !sessionsBefore.has(f))
const verdict = assertSmoke({
  observerOut,
  sessionsRoot,
  newSessionFiles,
  parentSessionId,
  nonce,
  workspace,
  repo: REPO,
  toolPattern,
  journalBaseline,
})
// A failed run must carry the host diagnostics in its failure message.
if (verdict.result === "FAIL" || hostFailed) {
  verdict.failures = [`FAIL the host run did not satisfy the facts\n${hostDiagnostics}`, ...verdict.failures]
  verdict.result = "FAIL"
  notes.push(hostDiagnostics)
}

const summary = {
  optIn: true,
  bundle,
  observer: observerOut,
  stdout: stdoutPath,
  nonce,
  parentSessionId,
  elapsedMs,
  ompExit: { code: result.code, signal: result.signal ?? null },
  newSessionFiles,
  hostDiagnostics,
  hostLog: hostLog || null,
  journalBaseline,
  ...verdict,
  notes: [...notes, ...verdict.notes],
}
const summaryPath = path.join(os.tmpdir(), "cut36-omp-smoke-summary.json")
fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2))

for (const n of summary.notes) console.log(n)
console.log("---- observations ----")
console.log(JSON.stringify(verdict.observations))
if (verdict.childSession) console.log(`child session: ${JSON.stringify(verdict.childSession)}`)
console.log("---- result ----")
for (const f of verdict.failures) console.log(f)
console.log(`CUT-36 real smoke: ${verdict.result}`)
console.log(`summary: ${summaryPath}`)
if (!hostFailed && result.stderr && result.stderr.trim())
  console.log(`omp stderr (tail):\n${tailLines(result.stderr, 10)}`)

process.exit(verdict.result === "PASS" ? 0 : 1)
