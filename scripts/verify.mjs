#!/usr/bin/env node
// verify.mjs — the fixed verification pipeline. `verify:daily` is the developer
// entry; `verify:release` adds release-only proofs (compiler-boundary canary,
// repo-wide envelope oracle, Long Stroke, real package) after the shared
// format/check/build/unit phases.

import { spawn } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import fs from 'node:fs'
import crypto from 'node:crypto'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const VERIFY_LOGS_DIR = path.join(root, '.fable-build', 'verify-logs')

function parseArgs(argv) {
  const args = new Set(argv)
  return {
    release: args.has('--release'),
    verbose: args.has('--verbose'),
  }
}

function logSection(label) {
  process.stderr.write(`\n=== verify: ${label} ===\n`)
}

function logInfo(msg) {
  process.stderr.write(`[verify] ${msg}\n`)
}

function hashTreeFiles(root) {
  const files = []

  const walk = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const abs = path.join(dir, entry.name)
      if (entry.isDirectory()) {
        const rel = path.relative(root, abs).split(path.sep).join('/')
        if (
          rel.startsWith('dist/') ||
          rel.startsWith('.fable-build') ||
          rel.startsWith('node_modules') ||
          rel.startsWith('.git')
        ) {
          continue
        }
        walk(abs)
      } else if (entry.isFile()) {
        const rel = path.relative(root, abs).split(path.sep).join('/')
        if (rel === 'package.json' || rel === 'package-lock.json') {
          files.push(rel)
        }
      }
    }
  }
  walk(path.join(root, 'src'))
  walk(path.join(root, 'scripts'))
  walk(path.join(root, 'requirements'))
  walk(path.join(root, 'resources'))
  walk(path.join(root, '.github'))
  return files.sort()
}

function digestFile(file) {
  return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')
}

function takeInputSnapshot(root) {
  const files = hashTreeFiles(root)
  const snapshot = {}
  for (const rel of files) {
    snapshot[rel] = digestFile(path.join(root, rel))
  }
  return snapshot
}

function snapshotsEqual(a, b) {
  const aKeys = Object.keys(a)
  const bKeys = Object.keys(b)
  if (aKeys.length !== bKeys.length) {
    return { equal: false, reason: 'file-set-changed' }
  }
  for (const key of aKeys) {
    if (a[key] !== b[key]) {
      return { equal: false, reason: `content-changed:${key}` }
    }
  }
  return { equal: true }
}

let verboseMode = false

async function runStep({ label, cmd = process.execPath, argv, env, timeoutMs = 600_000, logDir }) {
  const startedAt = Date.now()
  const stepLog = path.join(logDir, `${label}.log`)
  const logStream = fs.createWriteStream(stepLog, { flags: 'w' })

  let killed = false
  const timer = setTimeout(() => {
    killed = true
    child.kill('SIGKILL')
  }, timeoutMs)
  timer.unref()

  const child = spawn(cmd, argv, {
    cwd: root,
    stdio: ['ignore', 'pipe', 'pipe'],
    env: { ...process.env, ...env, WXS_RELEASE: env?.WXS_RELEASE ?? '0' },
  })

  child.stdout.on('data', (chunk) => {
    logStream.write(chunk)
    if (verboseMode) process.stdout.write(chunk)
  })
  child.stderr.on('data', (chunk) => {
    logStream.write(chunk)
    process.stderr.write(chunk)
  })

  const exit = await new Promise((resolveExit) => {
    child.on('error', (error) => resolveExit({ code: 1, signal: null, error }))
    child.on('close', (code, signal) => resolveExit({ code: code ?? 1, signal }))
  })

  clearTimeout(timer)
  logStream.end()

  return {
    label,
    exitCode: exit.code ?? 1,
    signal: exit.signal,
    killed,
    durationMs: Date.now() - startedAt,
    logPath: path.relative(root, stepLog),
    ok: exit.code === 0 && !exit.signal && !killed,
  }
}

/**
 * Run the verification pipeline. Exported so tests can inject a fake runStep
 * and observe the step trace without executing real subprocesses.
 */
export async function verify({ release = false, verbose = false, runStep: runStepOverride }) {
  const step = runStepOverride ?? runStep

  fs.mkdirSync(VERIFY_LOGS_DIR, { recursive: true })
  const runStamp = new Date().toISOString().replace(/[:.]/g, '-')
  const runLogDir = path.join(VERIFY_LOGS_DIR, runStamp)
  fs.mkdirSync(runLogDir, { recursive: true })
  const latestLink = path.join(VERIFY_LOGS_DIR, 'latest')
  try {
    fs.unlinkSync(latestLink)
  } catch {}
  try {
    fs.symlinkSync(runStamp, latestLink, 'dir')
  } catch {}

  verboseMode = verbose
  const inputSnapshot = takeInputSnapshot(root)
  const initialDigest = crypto
    .createHash('sha256')
    .update(JSON.stringify(inputSnapshot))
    .digest('hex')
    .slice(0, 16)
  const runStart = Date.now()

  logSection(`verify ${release ? 'release' : 'daily'}  inputs=${initialDigest}`)

  const results = []
  const markStep = (result) => {
    results.push(result)
    const tag = result.ok ? 'OK' : `FAIL(${result.exitCode})`
    logInfo(`${result.label.padEnd(14)} ${tag}  ${result.durationMs}ms  ${result.logPath ?? ''}`)
  }

  markStep(
    await step({
      label: 'format:check',
      cmd: 'npm',
      argv: ['run', 'format:check'],
      logDir: runLogDir,
    }),
  )
  if (!results.at(-1).ok) return summarizeAndExit(release, results, inputSnapshot, { runStart })

  markStep(
    await step({
      label: 'check',
      cmd: 'npm',
      argv: ['run', 'check'],
      logDir: runLogDir,
    }),
  )
  if (!results.at(-1).ok) return summarizeAndExit(release, results, inputSnapshot, { runStart })

  markStep(
    await step({
      label: 'build',
      argv: [path.join(root, 'scripts/build.mjs'), ...(release ? ['--clean'] : [])],
      logDir: runLogDir,
    }),
  )
  if (!results.at(-1).ok) return summarizeAndExit(release, results, inputSnapshot, { runStart })

  markStep(
    await step({
      label: 'unit',
      argv: [
        path.join(root, 'requirements/verification-system/tests/run.mjs'),
        '--skip-staleness-check',
      ],
      logDir: runLogDir,
    }),
  )
  if (!results.at(-1).ok) return summarizeAndExit(release, results, inputSnapshot, { runStart })

  markStep(
    await step({
      label: 'integration',
      argv: [path.join(root, 'requirements/verification-system/tests/integration/run.mjs')],
      logDir: runLogDir,
      timeoutMs: release ? 1_200_000 : 600_000,
      env: release ? { WXS_RELEASE: '1' } : {},
    }),
  )
  if (!results.at(-1).ok) return summarizeAndExit(release, results, inputSnapshot, { runStart })

  if (release) {
    markStep(
      await step({
        label: 'e2e',
        argv: [path.join(root, 'requirements/verification-system/tests/e2e/entry.test.mjs')],
        logDir: runLogDir,
        timeoutMs: 1_500_000,
      }),
    )
    if (!results.at(-1).ok) return summarizeAndExit(release, results, inputSnapshot, { runStart })

    markStep(
      await step({
        label: 'package',
        argv: [path.join(root, 'scripts/verify-package.mjs')],
        logDir: runLogDir,
        timeoutMs: 600_000,
      }),
    )
    if (!results.at(-1).ok) return summarizeAndExit(release, results, inputSnapshot, { runStart })
  }

  return summarizeAndExit(release, results, inputSnapshot, { runStart })
}

function summarizeAndExit(release, results, inputSnapshot, { runStart = Date.now() }) {
  const finalSnapshot = takeInputSnapshot(root)
  const diff = snapshotsEqual(inputSnapshot, finalSnapshot)
  const wallMs = Date.now() - runStart

  let allPass = true
  for (const result of results) {
    const tag = result.ok ? 'OK' : `FAIL(${result.exitCode})`
    if (!result.ok) allPass = false
    process.stdout.write(`  ${result.label.padEnd(14)} ${tag}  ${(result.durationMs / 1000).toFixed(1)}s\n`)
  }

  if (!allPass) {
    process.stdout.write(`FAIL  verify ${release ? 'release' : 'daily'}  ${(wallMs / 1000).toFixed(1)}s\n`)
    return { exitCode: 1, wallMs }
  }

  if (!diff.equal) {
    process.stdout.write(
      `FAIL: inputs changed mid-run (${diff.reason}) — re-run verify on a stable tree\n`,
    )
    return { exitCode: 1, wallMs }
  }

  process.stdout.write(`PASS  verify ${release ? 'release' : 'daily'}  ${(wallMs / 1000).toFixed(1)}s\n`)
  return { exitCode: 0, wallMs }
}

async function main() {
  const { release, verbose } = parseArgs(process.argv.slice(2))

  if (process.argv.includes('--help') || process.argv.includes('-h')) {
    process.stdout.write(`Usage: node scripts/verify.mjs [--release] [--verbose]\n`)
    process.exit(0)
  }

  const { exitCode } = await verify({ release, verbose })
  process.exit(exitCode)
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  await main()
}
