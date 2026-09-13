#!/usr/bin/env node
// verify.mjs — the fixed verification pipeline. `verify:daily` is the developer
// entry; `verify:release` adds release-only proofs (compiler-boundary canary,
// repo-wide envelope oracle, Long Stroke, real package) after the shared
// format/check/build/unit phases.

import { spawn } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import fs from 'node:fs'
import { collectVerificationInputs, computeDigest, diffVerificationInputs } from './lib/build-state.mjs'

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')

export function verificationSteps({ root = ROOT, release = false }) {
  const steps = [
    {
      label: 'format:check',
      cmd: 'npm',
      argv: ['run', 'format:check'],
    },
    {
      label: 'check',
      cmd: 'npm',
      argv: ['run', 'check'],
    },
    {
      label: 'build',
      cmd: process.execPath,
      argv: [path.join(root, 'scripts/build.mjs'), ...(release ? ['--clean'] : [])],
    },
    {
      label: 'unit',
      cmd: process.execPath,
      argv: [path.join(root, 'requirements/verification-system/tests/run.mjs')],
    },
    {
      label: 'integration',
      cmd: process.execPath,
      argv: [path.join(root, 'requirements/verification-system/tests/integration/run.mjs')],
      timeoutMs: release ? 1_200_000 : 600_000,
      env: release ? { WXS_RELEASE: '1' } : {},
    },
  ]

  if (release) {
    steps.push(
      {
        label: 'e2e',
        cmd: process.execPath,
        argv: [path.join(root, 'requirements/verification-system/tests/e2e/entry.test.mjs')],
        timeoutMs: 1_500_000,
      },
      {
        label: 'package',
        cmd: process.execPath,
        argv: [path.join(root, 'scripts/verify-package.mjs')],
        timeoutMs: 600_000,
      },
    )
  }

  return steps
}

function defaultRunStepFactory(root, verbose, output) {
  return async function defaultRunStep({
    label,
    cmd = process.execPath,
    argv,
    env,
    timeoutMs = 600_000,
    logDir,
    cwd = root,
  }) {
    const startedAt = Date.now()
    const stepLog = path.join(logDir, `${label.replace(/:/g, '-')}.log`)
    const logStream = fs.createWriteStream(stepLog, { flags: 'w' })

    let killed = false
    const timer = setTimeout(() => {
      killed = true
      child.kill('SIGKILL')
    }, timeoutMs)
    timer.unref()

    let child
    try {
      child = spawn(cmd, argv, {
        cwd,
        stdio: ['ignore', 'pipe', 'pipe'],
        env: { ...process.env, ...env, WXS_RELEASE: env?.WXS_RELEASE ?? '0' },
      })
    } catch (spawnError) {
      clearTimeout(timer)
      logStream.end()
      return {
        label,
        exitCode: 1,
        signal: null,
        killed: false,
        durationMs: Date.now() - startedAt,
        logPath: path.relative(root, stepLog),
        ok: false,
        error: spawnError,
      }
    }

    child.stdout.on('data', (chunk) => {
      logStream.write(chunk)
      if (verbose) output.write(chunk)
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
      ...(exit.error ? { error: exit.error } : {}),
    }
  }
}

function allocateRunLogDir(baseLogDir) {
  fs.mkdirSync(baseLogDir, { recursive: true })
  const baseStamp = new Date().toISOString().replace(/[:.]/g, '-')
  let stamp = baseStamp
  let seq = 1
  while (fs.existsSync(path.join(baseLogDir, stamp))) {
    stamp = `${baseStamp}-${seq++}`
  }
  const runLogDir = path.join(baseLogDir, stamp)
  fs.mkdirSync(runLogDir, { recursive: true })

  const latestLink = path.join(baseLogDir, 'latest')
  try {
    fs.unlinkSync(latestLink)
  } catch {}
  try {
    fs.symlinkSync(stamp, latestLink, 'dir')
  } catch {}

  return runLogDir
}

export async function verify({
  root = ROOT,
  release = false,
  verbose = false,
  runStep: runStepOverride,
  output = process.stdout,
  logDirectory,
} = {}) {
  const resolvedRoot = path.resolve(root)
  const baseLogDir = logDirectory ? path.resolve(logDirectory) : path.join(resolvedRoot, '.fable-build', 'verify-logs')
  const runLogDir = allocateRunLogDir(baseLogDir)

  const stepRunner = runStepOverride ?? defaultRunStepFactory(resolvedRoot, verbose, output)
  const plannedSteps = verificationSteps({ root: resolvedRoot, release })
  const mode = release ? 'release' : 'daily'
  const runStart = Date.now()

  let beforeInputs
  try {
    beforeInputs = collectVerificationInputs(resolvedRoot)
  } catch (err) {
    const wallMs = Date.now() - runStart
    output.write(`\n=== verify: ${mode}  inputs=error ===\n`)
    output.write(`FAIL: input collection error: ${err.message}\n`)
    return {
      mode,
      steps: plannedSteps.map((p) => ({ label: p.label, status: 'not-run' })),
      outcome: 'fail',
      failureReason: `input-collection-failed: ${err.message}`,
      wallMs,
      logDirectory: runLogDir,
      exitCode: 1,
    }
  }

  const initialDigest = computeDigest(beforeInputs).slice(0, 16)
  output.write(`\n=== verify: ${mode}  inputs=${initialDigest} ===\n`)

  const stepResults = []
  let pipelineFailed = false
  let failureReason

  for (let i = 0; i < plannedSteps.length; i++) {
    const stepPlan = plannedSteps[i]
    if (pipelineFailed) {
      stepResults.push({ label: stepPlan.label, status: 'not-run' })
      continue
    }

    let res
    try {
      res = await stepRunner({
        ...stepPlan,
        logDir: runLogDir,
        cwd: resolvedRoot,
      })
    } catch (err) {
      res = {
        label: stepPlan.label,
        ok: false,
        exitCode: 1,
        signal: null,
        durationMs: 0,
        error: err,
      }
    }

    const durationMs = res.durationMs ?? 0
    if (res.ok) {
      stepResults.push({
        label: res.label ?? stepPlan.label,
        status: 'ok',
        exitCode: res.exitCode ?? 0,
        signal: res.signal ?? null,
        durationMs,
      })
      output.write(`  ${stepPlan.label.padEnd(14)} OK  ${(durationMs / 1000).toFixed(1)}s\n`)
    } else {
      pipelineFailed = true
      failureReason = `step-failed:${stepPlan.label}`
      stepResults.push({
        label: res.label ?? stepPlan.label,
        status: 'failed',
        exitCode: res.exitCode ?? 1,
        signal: res.signal ?? null,
        durationMs,
        ...(res.error ? { error: res.error } : {}),
      })
      output.write(`  ${stepPlan.label.padEnd(14)} FAIL(${res.exitCode ?? 1})  ${(durationMs / 1000).toFixed(1)}s\n`)
    }
  }

  let afterInputs
  let inputChanges
  try {
    afterInputs = collectVerificationInputs(resolvedRoot)
    const diff = diffVerificationInputs(beforeInputs, afterInputs)
    if (!diff.equal) {
      inputChanges = diff
      if (!pipelineFailed) {
        pipelineFailed = true
        failureReason = `inputs-changed:${diff.reason}`
      }
    }
  } catch (err) {
    if (!pipelineFailed) {
      pipelineFailed = true
      failureReason = `after-input-collection-failed: ${err.message}`
    }
  }

  const wallMs = Date.now() - runStart
  if (pipelineFailed) {
    if (inputChanges) {
      output.write(`FAIL: inputs changed mid-run (${inputChanges.reason}) — re-run verify on a stable tree\n`)
    }
    output.write(`FAIL  verify ${mode}  ${(wallMs / 1000).toFixed(1)}s\n`)
    return {
      mode,
      steps: stepResults,
      outcome: 'fail',
      ...(failureReason ? { failureReason } : {}),
      ...(inputChanges ? { inputChanges } : {}),
      wallMs,
      logDirectory: runLogDir,
      exitCode: 1,
    }
  }

  output.write(`PASS  verify ${mode}  ${(wallMs / 1000).toFixed(1)}s\n`)
  return {
    mode,
    steps: stepResults,
    outcome: 'pass',
    wallMs,
    logDirectory: runLogDir,
    exitCode: 0,
  }
}

async function main() {
  const argv = process.argv.slice(2)
  let release = false
  let verbose = false

  for (const arg of argv) {
    if (arg === '--release') {
      release = true
    } else if (arg === '--verbose') {
      verbose = true
    } else if (arg === '-h' || arg === '--help') {
      process.stdout.write('Usage: node scripts/verify.mjs [--release] [--verbose]\n')
      process.exit(0)
    } else {
      process.stderr.write(`Unknown option: ${arg}\n`)
      process.exit(2)
    }
  }

  const result = await verify({ release, verbose })
  process.exitCode = result.exitCode
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  await main()
}
