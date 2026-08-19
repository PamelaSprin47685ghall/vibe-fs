import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'

import { run as runSurfaceManifest } from './checks/js-surface-manifest.mjs'
import {
  calibrateFromRepository,
  writeLoopDetectorCalibrationArtifact,
} from './lib/calibrate-loop-detector.mjs'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const dist = path.join(root, 'dist')
const daemonDir = path.join(root, '.fable-daemon')
const pidFile = path.join(daemonDir, 'pid')
const logFile = path.join(daemonDir, 'log')
const ackJson = path.join(daemonDir, 'ack.json')
const barrierFs = path.join(root, 'src/Wanxiangshu/FableBarrier.fs')

function fail(message) {
  console.error(message)
  process.exit(1)
}

function isPidRunning(pid) {
  if (!pid || isNaN(pid)) return false
  try {
    process.kill(pid, 0)
    return true
  } catch {
    return false
  }
}

function getDaemonPid() {
  if (!fs.existsSync(pidFile)) return null
  try {
    const raw = fs.readFileSync(pidFile, 'utf8').trim()
    const pid = parseInt(raw, 10)
    if (isPidRunning(pid)) return pid
    try {
      fs.unlinkSync(pidFile)
    } catch {}
    return null
  } catch {
    return null
  }
}

function stopDaemon() {
  const pid = getDaemonPid()
  if (pid) {
    try {
      process.kill(pid, 'SIGTERM')
    } catch {}
    const deadline = Date.now() + 2000
    while (Date.now() < deadline && isPidRunning(pid)) {
      const waitEnd = Date.now() + 50
      while (Date.now() < waitEnd) {}
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
}

function startDaemon() {
  fs.mkdirSync(daemonDir, { recursive: true })
  fs.mkdirSync(dist, { recursive: true })

  if (!fs.existsSync(barrierFs)) {
    fs.writeFileSync(
      barrierFs,
      `namespace Wanxiangshu\n\nmodule FableBarrier =\n    let token = "init"\n`,
      'utf8',
    )
  }

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
  fs.writeFileSync(pidFile, String(child.pid), 'utf8')
  return child.pid
}

async function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

function readNewLogs(fromOffset) {
  if (!fs.existsSync(logFile)) return ''
  try {
    const fd = fs.openSync(logFile, 'r')
    const stat = fs.fstatSync(fd)
    const length = stat.size - fromOffset
    if (length <= 0) {
      fs.closeSync(fd)
      return ''
    }
    const buffer = Buffer.alloc(length)
    fs.readSync(fd, buffer, 0, length, fromOffset)
    fs.closeSync(fd)
    return buffer.toString('utf8')
  } catch {
    return ''
  }
}

async function barrierSync() {
  let pid = getDaemonPid()
  const isCold = !pid
  if (!pid) {
    pid = startDaemon()
  }

  const token = `barrier-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`
  const initialLogOffset = fs.existsSync(logFile) ? fs.statSync(logFile).size : 0

  const barrierContent = `namespace Wanxiangshu\n\nmodule FableBarrier =\n    let token = "${token}"\n`
  fs.writeFileSync(barrierFs, barrierContent, 'utf8')

  const timeoutMs = isCold ? 120_000 : 45_000
  const deadline = Date.now() + timeoutMs

  while (Date.now() < deadline) {
    if (!isPidRunning(pid)) {
      const logs = readNewLogs(initialLogOffset)
      fail(`fable watch daemon died unexpectedly.\n${logs}`)
    }

    if (fs.existsSync(ackJson)) {
      try {
        const raw = fs.readFileSync(ackJson, 'utf8')
        const ack = JSON.parse(raw)
        if (ack.token === token && ack.status === 'ok') {
          return
        }
      } catch {}
    }

    const newLogs = readNewLogs(initialLogOffset)
    if (
      newLogs.includes('Compilation failed') ||
      (newLogs.includes('error FSHARP:') && !/Watching\s+src\/Wanxiangshu/.test(newLogs))
    ) {
      await sleep(100)
      const fullLogs = readNewLogs(initialLogOffset)
      fail(`fable compilation failed:\n${fullLogs}`)
    }

    await sleep(50)
  }

  const logs = readNewLogs(initialLogOffset)
  fail(`fable barrier timed out after ${timeoutMs}ms.\nLogs:\n${logs}`)
}

if (process.argv.includes('--stop')) {
  stopDaemon()
  console.log('fable daemon stopped')
  process.exit(0)
}

if (process.argv.includes('--restart')) {
  stopDaemon()
}

await barrierSync()

// DG-004: calibration is build input, never tracked source. Compute once from
// the current repository corpus; materialize values only as generated dist JS.
const loopDetectorCalibration = calibrateFromRepository(root)

writeLoopDetectorCalibrationArtifact(root, loopDetectorCalibration)

const entry = path.join(root, 'dist/OpenCode/Plugin/Plugin.js')
if (!fs.existsSync(entry)) fail(`missing entry: ${entry}`)

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

const sphinxEntry = path.join(dist, 'Sphinx', 'McpServer.js')
if (!fs.existsSync(sphinxEntry)) fail(`missing sphinx entry: ${sphinxEntry}`)

// JS-SEMANTIC-SURFACE-003/005: dist surface manifest validation (post-compile).
// This gate requires emitted dist/ surfaces, so it runs here after fable
// precompile — not in the pre-build check.mjs (which must pass with partial or
// missing dist).
if (runSurfaceManifest({ root }) !== 0) fail('js-surface-manifest: dist surface manifest validation failed')

console.log('build ok')
