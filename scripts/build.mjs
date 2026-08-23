#!/usr/bin/env node
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'

import { run as runSurfaceManifest } from './checks/js-surface-manifest.mjs'
import {
  writeLoopDetectorEnvelopeArtifact,
} from './lib/derive-loop-detector-envelope.mjs'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const dist = path.join(root, 'dist')
const buildStateDir = path.join(root, '.fable-build')
const buildLockFile = path.join(buildStateDir, 'build.lock')

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

// ── Build Serialization ──────────────────────────────────────────────────────

function isPidRunning(pid) {
  if (!pid || typeof pid !== 'number' || isNaN(pid) || pid <= 0) return false
  try {
    process.kill(pid, 0)
    return true
  } catch (err) {
    return err.code === 'EPERM'
  }
}

class CrossProcessMutex {
  constructor(lockPath, name = 'lock') {
    this.lockPath = lockPath
    this.name = name
    this.held = false
  }

  async acquire(waitTimeoutMs = 180_000) {
    fs.mkdirSync(path.dirname(this.lockPath), { recursive: true })
    const deadline = Date.now() + waitTimeoutMs
    while (Date.now() < deadline) {
      try {
        const payload = JSON.stringify({ pid: process.pid })
        fs.writeFileSync(this.lockPath, payload, { flag: 'wx', encoding: 'utf8' })
        this.held = true
        return true
      } catch (err) {
        if (err.code !== 'EEXIST') throw err

        // Check if existing lock is dead or stale
        try {
          const raw = fs.readFileSync(this.lockPath, 'utf8')
          const info = JSON.parse(raw)
          const isDead = !isPidRunning(info.pid)

          if (isDead) {
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

        await sleep(100)
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

// ── Fable Compile ─────────────────────────────────────────────────────────────

async function compileFable() {
  fs.mkdirSync(dist, { recursive: true })
  logInfo('Compiling F# with Fable...')

  const child = spawn(
    'dotnet',
    [
      'tool',
      'run',
      'fable',
      '--',
      'src/Wanxiangshu/Wanxiangshu.fsproj',
      '-c',
      'Debug',
      '-o',
      'dist',
      '--noGitignore',
    ],
    { cwd: root, stdio: 'inherit' },
  )

  const result = await new Promise((resolve, reject) => {
    child.once('error', reject)
    child.once('exit', (code, signal) => resolve({ code, signal }))
  })

  if (result.code !== 0) {
    fail(
      `Fable compilation failed${result.signal ? ` by signal ${result.signal}` : ` with exit code ${result.code}`}`,
    )
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
Usage: node scripts/build.mjs

Options:
  --help, -h   Show this help message
`)
    process.exit(0)
  }

  const buildMutex = new CrossProcessMutex(buildLockFile, 'build lock')
  await buildMutex.acquire()

  try {
    await compileFable()
    verifyArtifacts()
    logInfo('build ok')
  } finally {
    buildMutex.release()
  }
}

await main()
