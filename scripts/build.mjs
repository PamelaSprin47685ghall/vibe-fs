#!/usr/bin/env node
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'

import { run as runSurfaceManifest } from './checks/js-surface-manifest.mjs'
import {
  writeLoopDetectorEnvelopeArtifact,
} from './lib/derive-loop-detector-envelope.mjs'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const dist = path.join(root, 'dist')
const daemonDir = path.join(root, '.fable-daemon')
const pidFile = path.join(daemonDir, 'pid')
const logFile = path.join(daemonDir, 'log')
const ackJson = path.join(daemonDir, 'ack.json')
const buildLockFile = path.join(daemonDir, 'build.lock')
const startLockFile = path.join(daemonDir, 'start.lock')
const barrierFs = path.join(root, 'src/Wanxiangshu/FableBarrier.fs')

const LOCK_STALE_TIMEOUT_MS = 150_000
const COLD_START_TIMEOUT_MS = 120_000
const WARM_BUILD_TIMEOUT_MS = 60_000

// ── Diagnostics & Output ─────────────────────────────────────────────────────

function formatBanner(title, color = '\x1b[31m') {
  const line = '═'.repeat(80)
  return `${color}${line}\n  ${title}\n${line}\x1b[0m`
}

function fail(message, details = null) {
  console.error(formatBanner('BUILD FAILED'))
  if (message) console.error(message)
  if (details) console.error(`\n${details}`)
  process.exit(1)
}

function logInfo(msg) {
  console.log(`\x1b[36m[build]\x1b[0m ${msg}`)
}

async function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

// ── Process & Lock Management ────────────────────────────────────────────────

function isPidRunning(pid) {
  if (!pid || typeof pid !== 'number' || isNaN(pid) || pid <= 0) return false
  try {
    process.kill(pid, 0)
    return true
  } catch (err) {
    return err.code === 'EPERM'
  }
}

function isFableDaemonProcess(pid) {
  if (!isPidRunning(pid)) return false
  if (process.platform === 'linux') {
    try {
      const cmdline = fs.readFileSync(`/proc/${pid}/cmdline`, 'utf8')
      return cmdline.includes('fable') || cmdline.includes('dotnet')
    } catch {
      return true
    }
  }
  return true
}

class CrossProcessMutex {
  constructor(lockPath, name = 'lock', timeoutMs = LOCK_STALE_TIMEOUT_MS) {
    this.lockPath = lockPath
    this.name = name
    this.timeoutMs = timeoutMs
    this.held = false
  }

  async acquire(waitTimeoutMs = 180_000) {
    fs.mkdirSync(path.dirname(this.lockPath), { recursive: true })
    const deadline = Date.now() + waitTimeoutMs
    let attempt = 0

    while (Date.now() < deadline) {
      attempt++
      try {
        const payload = JSON.stringify({
          pid: process.pid,
          createdAt: Date.now(),
          token: crypto.randomBytes(8).toString('hex'),
        })
        fs.writeFileSync(this.lockPath, payload, { flag: 'wx', encoding: 'utf8' })
        this.held = true
        return true
      } catch (err) {
        if (err.code !== 'EEXIST') throw err

        // Check if existing lock is dead or stale
        try {
          const raw = fs.readFileSync(this.lockPath, 'utf8')
          const info = JSON.parse(raw)
          const isStale = Date.now() - (info.createdAt || 0) > this.timeoutMs
          const isDead = !isPidRunning(info.pid)

          if (isDead || isStale) {
            try {
              fs.unlinkSync(this.lockPath)
              continue
            } catch {}
          }
        } catch {
          try {
            fs.unlinkSync(this.lockPath)
            continue
          } catch {}
        }

        const delay = Math.min(50 + attempt * 25 + Math.floor(Math.random() * 50), 300)
        await sleep(delay)
      }
    }

    throw new Error(`Failed to acquire ${this.name} after ${waitTimeoutMs}ms (lock at ${this.lockPath})`)
  }

  release() {
    if (!this.held) return
    try {
      if (fs.existsSync(this.lockPath)) {
        const raw = fs.readFileSync(this.lockPath, 'utf8')
        const info = JSON.parse(raw)
        if (info.pid === process.pid) {
          fs.unlinkSync(this.lockPath)
        }
      }
    } catch {}
    this.held = false
  }
}

// ── Daemon Lifecycle ─────────────────────────────────────────────────────────

function getDaemonPid() {
  if (!fs.existsSync(pidFile)) return null
  try {
    const raw = fs.readFileSync(pidFile, 'utf8').trim()
    const pid = parseInt(raw, 10)
    if (isFableDaemonProcess(pid)) return pid
    try {
      fs.unlinkSync(pidFile)
    } catch {}
    return null
  } catch {
    return null
  }
}

async function stopDaemon() {
  const pid = getDaemonPid()
  if (pid) {
    logInfo(`Stopping Fable daemon (PID ${pid})...`)
    try {
      process.kill(pid, 'SIGTERM')
    } catch {}

    const deadline = Date.now() + 3000
    while (Date.now() < deadline && isPidRunning(pid)) {
      await sleep(100)
    }

    if (isPidRunning(pid)) {
      try {
        process.kill(pid, 'SIGKILL')
      } catch {}
    }
  }

  try {
    if (fs.existsSync(pidFile)) fs.unlinkSync(pidFile)
  } catch {}
  try {
    if (fs.existsSync(buildLockFile)) fs.unlinkSync(buildLockFile)
  } catch {}
  try {
    if (fs.existsSync(startLockFile)) fs.unlinkSync(startLockFile)
  } catch {}
}

async function startDaemon() {
  const startMutex = new CrossProcessMutex(startLockFile, 'daemon start lock', 30_000)
  await startMutex.acquire(45_000)

  try {
    // Re-check if another process just started it while we were waiting
    const existingPid = getDaemonPid()
    if (existingPid) return existingPid

    fs.mkdirSync(daemonDir, { recursive: true })
    fs.mkdirSync(dist, { recursive: true })

    if (!fs.existsSync(barrierFs)) {
      fs.writeFileSync(
        barrierFs,
        `namespace Wanxiangshu\n\nmodule FableBarrier =\n    let token = "init"\n`,
        'utf8',
      )
    }

    // Truncate or initialize log file
    const logFd = fs.openSync(logFile, 'a')
    const child = spawn(
      'dotnet',
      [
        'tool',
        'run',
        'fable',
        'watch',
        'src/Wanxiangshu/Wanxiangshu.fsproj',
        '-o',
        'dist',
        '--noGitignore',
        '--runWatch',
        'node',
        'scripts/fable-ack.mjs',
      ],
      {
        cwd: root,
        detached: true,
        stdio: ['ignore', logFd, logFd],
      },
    )

    child.unref()
    fs.closeSync(logFd)

    const pid = child.pid
    fs.writeFileSync(pidFile, String(pid), 'utf8')
    logInfo(`Spawned Fable watch daemon (PID ${pid})`)
    return pid
  } finally {
    startMutex.release()
  }
}

// ── Log Inspection & Compilation Error Extraction ────────────────────────────

function readLogSlice(fromOffset) {
  if (!fs.existsSync(logFile)) return { text: '', newOffset: fromOffset }
  try {
    const fd = fs.openSync(logFile, 'r')
    const stat = fs.fstatSync(fd)
    const currentSize = stat.size
    if (currentSize <= fromOffset) {
      fs.closeSync(fd)
      return { text: '', newOffset: fromOffset }
    }
    const length = currentSize - fromOffset
    const buffer = Buffer.alloc(length)
    fs.readSync(fd, buffer, 0, length, fromOffset)
    fs.closeSync(fd)
    return { text: buffer.toString('utf8'), newOffset: currentSize }
  } catch {
    return { text: '', newOffset: fromOffset }
  }
}

function extractCompilerErrors(logText) {
  const errorLines = []
  const lines = logText.split('\n')
  let inException = false

  for (const line of lines) {
    if (
      line.includes('error FSHARP:') ||
      line.includes('error FABLE:') ||
      line.includes('Compilation failed') ||
      line.includes('error EXCEPTION:') ||
      line.includes('Error:')
    ) {
      errorLines.push(line)
      if (line.includes('error EXCEPTION:')) inException = true
    } else if (inException) {
      if (line.startsWith('   at ') || line.trim().length === 0) {
        errorLines.push(line)
      } else {
        inException = false
      }
    }
  }

  return errorLines.length > 0 ? errorLines.join('\n') : null
}

// ── Barrier Synchronization ──────────────────────────────────────────────────

async function barrierSync() {
  const buildMutex = new CrossProcessMutex(buildLockFile, 'build lock')
  await buildMutex.acquire(180_000)

  try {
    let pid = getDaemonPid()
    const isCold = !pid
    if (!pid) {
      pid = await startDaemon()
    }

    const token = `barrier-${process.pid}-${Date.now()}-${crypto.randomBytes(6).toString('hex')}`
    const startLogOffset = fs.existsSync(logFile) ? fs.statSync(logFile).size : 0
    let logOffset = startLogOffset

    const barrierContent = `namespace Wanxiangshu\n\nmodule FableBarrier =\n    let token = "${token}"\n`
    fs.writeFileSync(barrierFs, barrierContent, 'utf8')

    const timeoutMs = isCold ? COLD_START_TIMEOUT_MS : WARM_BUILD_TIMEOUT_MS
    const deadline = Date.now() + timeoutMs
    let accumulatedLogs = ''

    while (Date.now() < deadline) {
      if (!isFableDaemonProcess(pid)) {
        const { text } = readLogSlice(startLogOffset)
        const errorDetails = extractCompilerErrors(text) || text.slice(-2000)
        fail(`Fable watch daemon (PID ${pid}) exited unexpectedly.`, errorDetails)
      }

      // Check ack.json for matching token
      if (fs.existsSync(ackJson)) {
        try {
          const raw = fs.readFileSync(ackJson, 'utf8')
          const ack = JSON.parse(raw)
          if (ack.token === token && ack.status === 'ok') {
            return
          }
        } catch {}
      }

      // Read new log bytes incrementally
      const { text: newChunk, newOffset } = readLogSlice(logOffset)
      if (newChunk) {
        accumulatedLogs += newChunk
        logOffset = newOffset

        // Immediate fail-fast detection:
        // When Fable finishes a cycle with errors, it prints error FSHARP / error EXCEPTION
        // and ends with "Watching src/Wanxiangshu" or "Fable compilation finished".
        const hasErrors =
          accumulatedLogs.includes('error FSHARP:') ||
          accumulatedLogs.includes('error FABLE:') ||
          accumulatedLogs.includes('error EXCEPTION:') ||
          accumulatedLogs.includes('Compilation failed')

        const hasFinishedCycle =
          accumulatedLogs.includes('Fable compilation finished') ||
          (accumulatedLogs.includes('Watching ') && accumulatedLogs.includes('src/Wanxiangshu'))

        if (hasErrors && hasFinishedCycle) {
          // Give a tiny moment for full flush
          await sleep(80)
          const { text: finalChunk } = readLogSlice(startLogOffset)
          const fullErrors = extractCompilerErrors(finalChunk) || finalChunk.slice(-3000)
          fail(`Fable compilation failed with compiler errors:`, fullErrors)
        }
      }

      await sleep(60)
    }

    // Timeout expired
    const { text: timeoutLogs } = readLogSlice(startLogOffset)
    const errorDetails = extractCompilerErrors(timeoutLogs) || timeoutLogs.slice(-2000)
    fail(`Fable barrier synchronization timed out after ${timeoutMs}ms (token: ${token})`, errorDetails)
  } finally {
    buildMutex.release()
  }
}

// ── Resource & Artifact Verification ─────────────────────────────────────────

function verifyArtifacts() {
  // DG-004: repository is the SSOT. Derive the current envelope on every build;
  // materialize it only as an ephemeral runtime import.
  try {
    writeLoopDetectorEnvelopeArtifact(root)
  } catch (err) {
    fail(`Failed to derive loop detector repository envelope: ${err.message}`)
  }

  const entry = path.join(root, 'dist/OpenCode/Plugin/Plugin.js')
  if (!fs.existsSync(entry)) fail(`missing entry artifact: ${entry}`)

  const sphinxEntry = path.join(dist, 'Sphinx', 'McpServer.js')
  if (!fs.existsSync(sphinxEntry)) fail(`missing sphinx entry artifact: ${sphinxEntry}`)

  const enforcerRoot = path.join(root, 'resources/enforcer')
  if (!fs.existsSync(enforcerRoot)) fail(`missing rulebook root: ${enforcerRoot}`)
  const ruleDirs = fs
    .readdirSync(enforcerRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
  if (ruleDirs.length < 1) fail(`enforcer rulebook has no rule directories under ${enforcerRoot}`)
  const catalogJson = path.join(enforcerRoot, 'catalog.json')
  if (fs.existsSync(catalogJson)) fail(`catalog.json must be removed after folder cutover: ${catalogJson}`)

  for (const name of ['primitive-obsession', ruleDirs[0]]) {
    const enforcerMd = path.join(enforcerRoot, name, 'enforcer.md')
    const mainMd = path.join(enforcerRoot, name, 'main.md')
    if (!fs.existsSync(enforcerMd)) fail(`missing rulebook file: ${enforcerMd}`)
    if (!fs.existsSync(mainMd)) fail(`missing rulebook file: ${mainMd}`)
  }

  const providerRoles = [
    'manager',
    'coder',
    'devops',
    'inspector',
    'reviewer',
    'browser',
    'inquiry',
    'orchestrator',
    'distiller',
    'blogger',
    'bookkeeper',
  ]
  for (const name of providerRoles) {
    for (const locale of ['en.md', 'zh-CN.md']) {
      const rolePath = path.join(root, 'resources/provider/role', name, locale)
      if (!fs.existsSync(rolePath)) fail(`missing Role Law: ${rolePath}`)
    }
  }

  for (const leaf of ['world/common-law', 'library/ingress', 'library/closing']) {
    for (const locale of ['en.md', 'zh-CN.md']) {
      const asset = path.join(root, 'resources/provider', leaf, locale)
      if (!fs.existsSync(asset)) fail(`missing provider asset: ${asset}`)
    }
  }

  // JS-SEMANTIC-SURFACE-003/005: dist surface manifest validation (post-compile).
  if (runSurfaceManifest({ root }) !== 0) {
    fail('js-surface-manifest: dist surface manifest validation failed')
  }
}

// ── Clean Signal & Exit Handlers ─────────────────────────────────────────────

function registerSignalHandlers() {
  const cleanup = () => {
    try {
      if (fs.existsSync(buildLockFile)) {
        const raw = fs.readFileSync(buildLockFile, 'utf8')
        const info = JSON.parse(raw)
        if (info.pid === process.pid) fs.unlinkSync(buildLockFile)
      }
    } catch {}
    try {
      if (fs.existsSync(startLockFile)) {
        const raw = fs.readFileSync(startLockFile, 'utf8')
        const info = JSON.parse(raw)
        if (info.pid === process.pid) fs.unlinkSync(startLockFile)
      }
    } catch {}
  }

  process.on('SIGINT', () => {
    cleanup()
    process.exit(130)
  })
  process.on('SIGTERM', () => {
    cleanup()
    process.exit(143)
  })
  process.on('exit', cleanup)
}

// ── Main Entrypoint ──────────────────────────────────────────────────────────

async function main() {
  registerSignalHandlers()

  if (process.argv.includes('--help') || process.argv.includes('-h')) {
    console.log(`
Usage: node scripts/build.mjs [options]

Options:
  --stop       Stop the background Fable watch daemon
  --restart    Restart the Fable watch daemon before building
  --status     Print daemon status and exit
  --help, -h   Show this help message
`)
    process.exit(0)
  }

  if (process.argv.includes('--status')) {
    const pid = getDaemonPid()
    if (pid) {
      console.log(`Fable daemon: RUNNING (PID ${pid})`)
    } else {
      console.log('Fable daemon: STOPPED')
    }
    process.exit(0)
  }

  if (process.argv.includes('--stop')) {
    await stopDaemon()
    logInfo('Fable daemon stopped')
    process.exit(0)
  }

  if (process.argv.includes('--restart')) {
    await stopDaemon()
  }

  await barrierSync()
  verifyArtifacts()
  logInfo('build ok')
}

await main()
