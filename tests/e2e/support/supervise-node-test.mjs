/**
 * Out-of-process supervisor for a node:test child (VERIFY-004 silence criterion).
 *
 * Shared by unit / integration / package runners. The silence window is fed only by
 * test verdicts; stdout/stderr/diagnostics are background and never renew.
 */

import { spawn } from 'node:child_process'
import { relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { Watchdog } from './watchdog.js'
import { classifyVerdict } from '../../unit/support/verdict-feed.mjs'

export const NODE_TEST_INNER = fileURLToPath(new URL('../../unit/support/run-inner.mjs', import.meta.url))

/**
 * @param {{
 *   files: string[],
 *   label: string,
 *   silenceMs: number,
 *   env?: NodeJS.ProcessEnv,
 *   logPrefix?: string,
 *   inner?: string,
 * }} opts
 * @returns {Promise<never>}
 */
export async function superviseNodeTest({
  files,
  label,
  silenceMs,
  env = process.env,
  logPrefix = 'runner',
  inner = NODE_TEST_INNER,
}) {
  if (!Array.isArray(files) || files.length === 0) {
    console.error(`${logPrefix}: no test files given`)
    process.exit(1)
  }
  if (!Number.isFinite(silenceMs) || silenceMs <= 0) {
    console.error(`${logPrefix}: silenceMs must be a positive number, got ${silenceMs}`)
    process.exit(1)
  }

  console.error(`${logPrefix}: ${files.length} test file(s), ${silenceMs}ms verdict-silence window`)

  // Absolute paths: test:complete reports absolute `data.file`.
  const outstanding = new Set(files.map((file) => resolve(file)))
  let passed = 0
  let failed = 0
  /** Per-test wall times, so every tier reports its distribution rather than just a count. */
  const durations = []
  let drained = false
  let child

  const watchdog = new Watchdog({
    timeoutMs: silenceMs,
    label,
    onTimeout: () => {
      if (failed === 0 && outstanding.size === 0) {
        console.error(
          `${logPrefix}: every verdict passed but the child would not exit — ` +
            'a handle the suite created is still open',
        )
      } else {
        console.error(
          `${logPrefix}: ${outstanding.size} file(s) had not reported completion: ` +
            `${[...outstanding].map((file) => relative(process.cwd(), file)).join(', ')}`,
        )
      }
      console.error(`${logPrefix}: ${passed} passed, ${failed} failed before the silence`)
      try {
        process.stderr.write('')
      } catch {}
      try {
        if (child?.pid) process.kill(-child.pid, 'SIGKILL')
      } catch {}
    },
  })

  child = spawn(process.execPath, [inner, ...files], {
    stdio: ['ignore', 'inherit', 'inherit', 'ipc'],
    detached: true,
    env,
  })

  child.on('message', (event) => {
    if (event?.type === 'inner:drained') {
      drained = true
      return
    }
    if (event?.type === 'test:pass') passed += 1
    if (event?.type === 'test:fail') failed += 1
    if (event?.type === 'test:pass' || event?.type === 'test:fail') {
      const ms = Number(event?.data?.durationMs)
      if (Number.isFinite(ms)) durations.push({ name: String(event?.data?.name ?? '<test>'), ms })
    }
    if (event?.type === 'test:complete' && typeof event?.data?.file === 'string') {
      outstanding.delete(resolve(event.data.file))
    }

    const progress = classifyVerdict(event)
    if (progress !== null) watchdog.advance(progress)
  })

  const exit = await new Promise((resolveExit) => {
    child.on('exit', (code, signal) => resolveExit({ code, signal }))
    child.on('error', (error) => {
      console.error(`${logPrefix}: could not start the inner runner: ${error.message}`)
      resolveExit({ code: 1, signal: null })
    })
  })

  watchdog.stop()

  reportDurationDistribution(logPrefix, durations)

  console.error(
    `\n${logPrefix}: ${passed} passed, ${failed} failed (authoritative; the spec reporter undercounts on timeout)`,
  )

  if (failed > 0) process.exit(1)

  if (exit.signal !== null) {
    console.error(
      `${logPrefix}: the inner runner died by ${exit.signal}; its verdicts describe an incomplete run`,
    )
    process.exit(1)
  }

  if (exit.code !== 0) {
    console.error(`${logPrefix}: inner runner exited ${exit.code}`)
    process.exit(exit.code ?? 1)
  }

  if (!drained) {
    console.error(`${logPrefix}: the inner runner exited without draining its result stream`)
    process.exit(1)
  }
}

/**
 * The tier's timing distribution, printed on every run.
 *
 * A suite that reports only pass/fail hides where its wall clock goes, and the first step of every
 * performance investigation then has to rebuild the measurement — which is why most never start.
 * Percentiles separate "uniformly slow" from "one slow tail", and the named top five say which
 * test to open.
 */
function reportDurationDistribution(logPrefix, durations) {
  if (durations.length === 0) return
  const ranked = [...durations].sort((left, right) => right.ms - left.ms)
  const total = durations.reduce((sum, entry) => sum + entry.ms, 0)
  const at = (fraction) => ranked[Math.min(ranked.length - 1, Math.floor(ranked.length * fraction))].ms
  const ms = (value) => `${Math.round(value)}ms`

  console.error(
    `${logPrefix}: ${durations.length} test(s), ${(total / 1000).toFixed(1)}s of test time; ` +
      `max ${ms(ranked[0].ms)}, p90 ${ms(at(0.1))}, median ${ms(at(0.5))}`,
  )
  // The named list only earns its lines when there is a tail worth opening: a tier whose whole
  // test time is under a second has no slowest test, it has noise.
  if (total < 1000) return
  for (const entry of ranked.slice(0, 5)) {
    console.error(`${logPrefix}:   ${ms(entry.ms).padStart(7)}  ${entry.name}`)
  }
}
