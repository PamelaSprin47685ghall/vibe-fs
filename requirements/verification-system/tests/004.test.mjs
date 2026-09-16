import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

import { parseConcurrency, assertConcurrency } from '../../../scripts/lib/concurrency-cap.mjs'
import { duplicateClauseDefinitions } from '../../../scripts/lib/spec-rules.mjs'
import { createVirtualClock } from './support/temporal-harness.mjs'
import { Watchdog } from './e2e/support/watchdog.js'
import { WATCHDOG_TIMEOUT_MS } from './e2e/support/time-budget.js'
import { assessIntegrationEntryCoverage } from './support/integration-entry-coverage.mjs'

const assess = (discoveredTests, wiredTests, childOwnedTests = []) =>
  assessIntegrationEntryCoverage({ discoveredTests, wiredTests, childOwnedTests })

// ── Concurrency cap parsing ──────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-004] parseConcurrency accepts unset, empty, and boolean values', () => {
  assert.equal(parseConcurrency(undefined), true)
  assert.equal(parseConcurrency(null), true)
  assert.equal(parseConcurrency(''), true)
  assert.equal(parseConcurrency(true), true)
  assert.equal(parseConcurrency('true'), true)
})

test('WHAT[VERIFICATION-SYSTEM-004] parseConcurrency treats false as 1 alias', () => {
  assert.equal(parseConcurrency(false), 1)
  assert.equal(parseConcurrency('false'), 1)
})

test('WHAT[VERIFICATION-SYSTEM-004] parseConcurrency accepts positive integers', () => {
  assert.equal(parseConcurrency(1), 1)
  assert.equal(parseConcurrency('1'), 1)
  assert.equal(parseConcurrency(4), 4)
  assert.equal(parseConcurrency('8'), 8)
})

test('WHAT[VERIFICATION-SYSTEM-004] parseConcurrency throws input error on non-positive, NaN, or non-integer', () => {
  assert.throws(() => parseConcurrency(0), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('0'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(-1), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('-5'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(1.5), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('2.7'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(NaN), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('invalid'), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency(Infinity), /Invalid NODE_TEST_CONCURRENCY/)
  assert.throws(() => parseConcurrency('-Infinity'), /Invalid NODE_TEST_CONCURRENCY/)
})

test('WHAT[VERIFICATION-SYSTEM-004] assertConcurrency forwards to parseConcurrency', () => {
  assert.equal(assertConcurrency('4'), 4)
  assert.throws(() => assertConcurrency('bad'), /Invalid NODE_TEST_CONCURRENCY/)
})

// ── Spec gate red capability ─────────────────────────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-004] spec gate rejects duplicate CHATEXEC identifiers', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'spec-duplicate-id-'))

  try {
    const entries = []
    for (const packageName of ['chat-execution-a', 'chat-execution-b']) {
      const packageDirectory = join(fixture, 'requirements', packageName)
      mkdirSync(packageDirectory, { recursive: true })
      const file = join(packageDirectory, 'WHAT.md')
      writeFileSync(
        file,
        `# ${packageName} — WHAT\n\n## CHATEXEC-001: duplicate fixture clause\n`,
      )
      entries.push({ file, pkg: packageName, text: readFileSync(file, 'utf8') })
    }

    const findings = duplicateClauseDefinitions(entries)
    assert.ok(
      findings.length > 0,
      'duplicateClauseDefinitions accepted duplicate CHATEXEC-001 definitions',
    )
    assert.match(
      findings.map((finding) => finding.msg).join('\n'),
      /条款 ID 重复定义：CHATEXEC-001/,
    )

    assert.deepEqual(
      duplicateClauseDefinitions([entries[0]]),
      [],
      'a single CHATEXEC-001 definition must not fail closed',
    )
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})

// ── Watchdog virtual-time verification ───────────────────────────────────────

function createVirtualTimers(clock) {
  const handles = new Map()
  let nextId = 1
  return {
    schedule(ms, cb) {
      const id = nextId++
      const h = clock.port.delay(ms)
      let active = true
      handles.set(id, {
        cancel: () => {
          active = false
          h.cancel()
        },
      })
      h.delay().then(() => {
        if (active) {
          handles.delete(id)
          cb()
        }
      })
      return id
    },
    cancel(id) {
      const entry = handles.get(id)
      if (entry) {
        handles.delete(id)
        entry.cancel()
      }
    },
  }
}

function createHarness(opts = {}) {
  const clock = createVirtualClock()
  const timers = createVirtualTimers(clock)
  const diagnostics = []
  let terminated = false
  let terminateCount = 0

  const deps = {
    clock: { nowMs: () => clock.nowMs() },
    timers,
    diagnostic: {
      write: (msg) => {
        diagnostics.push(msg)
      },
    },
    terminate: () => {
      terminated = true
      terminateCount += 1
    },
  }

  const watchdog = new Watchdog({
    timeoutMs: opts.timeoutMs ?? 1000,
    label: opts.label ?? 'test-dog',
    onTimeout: opts.onTimeout,
    deps,
  })

  return {
    clock,
    timers,
    diagnostics,
    watchdog,
    get terminated() {
      return terminated
    },
    get terminateCount() {
      return terminateCount
    },
    async advance(ms, flushRounds = 3) {
      clock.advance(ms)
      for (let r = 0; r < flushRounds; r += 1) await Promise.resolve()
      await Promise.resolve()
    },
  }
}

test('WHAT[VERIFICATION-SYSTEM-004] watchdog fires on silence after exact timeoutMs', async () => {
  const h = createHarness({ timeoutMs: 500, label: 'dog-silence' })

  await h.advance(499)
  assert.equal(h.terminated, false, 'must not fire before timeout')
  assert.equal(h.diagnostics.length, 0)

  await h.advance(1)
  assert.equal(h.terminated, true, 'must terminate at timeout')
  assert.equal(h.terminateCount, 1)
  assert.ok(h.diagnostics.some((d) => d.includes("WATCHDOG: 'dog-silence' silent for 500ms")))
})

test('WHAT[VERIFICATION-SYSTEM-004] blocking renew pushes timeout forward indefinitely', async () => {
  const h = createHarness({ timeoutMs: 300, label: 'dog-renew' })

  await h.advance(200)
  assert.equal(h.terminated, false)
  h.watchdog.advance({ reason: 'step-1', lane: 'worker', blocking: true })

  // Another 200ms passed; total 400ms > initial 300ms, but reset at 200ms
  await h.advance(200)
  assert.equal(h.terminated, false)
  h.watchdog.advance({ reason: 'step-2', lane: 'worker', blocking: true })

  await h.advance(250)
  assert.equal(h.terminated, false)

  // Now let it stay silent for 300ms from last progress
  await h.advance(50)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes('2 blocking progress update(s)')))
  assert.ok(h.diagnostics.some((d) => d.includes('last progress: step-2 lane=worker')))
})

test('WHAT[VERIFICATION-SYSTEM-004] background advance records info but does not renew timeout', async () => {
  const h = createHarness({ timeoutMs: 400, label: 'dog-background' })

  await h.advance(100)
  h.watchdog.advance({ reason: 'bg-step', lane: 'sidecar', blocking: false })
  assert.equal(h.terminated, false)

  await h.advance(200)
  h.watchdog.advance({ reason: 'bg-step-2', lane: 'sidecar', blocking: false })
  assert.equal(h.terminated, false)

  // Total elapsed 400ms: background advances did not push timeoutMs
  await h.advance(100)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes('0 blocking progress update(s)')))
  assert.ok(h.diagnostics.some((d) => d.includes('background progress 100ms ago: bg-step-2 lane=sidecar (2 background update(s), none of them renewals)')))
})

test('WHAT[VERIFICATION-SYSTEM-004] stop permanently disarms watchdog with no subsequent fire', async () => {
  const h = createHarness({ timeoutMs: 200, label: 'dog-stopped' })

  await h.advance(100)
  h.watchdog.stop()

  await h.advance(500)
  assert.equal(h.terminated, false, 'stopped watchdog must never terminate')
  assert.equal(h.diagnostics.length, 0)

  // advance and setWindow after stop must be safe no-ops
  h.watchdog.advance({ reason: 'post-stop', lane: 'worker', blocking: true })
  h.watchdog.setWindow(50)
  await h.advance(500)
  assert.equal(h.terminated, false)
})

test('WHAT[VERIFICATION-SYSTEM-004] setWindow(null) restores centralized default WATCHDOG_TIMEOUT_MS', async () => {
  const h = createHarness({ timeoutMs: 500, label: 'dog-window' })

  // Widen to 2000ms
  h.watchdog.setWindow(2000)
  await h.advance(1000)
  assert.equal(h.terminated, false, 'widened window allows 1000ms')

  // setWindow(null) restores WATCHDOG_TIMEOUT_MS (e.g. 5000ms)
  h.watchdog.setWindow(null)
  assert.equal(h.watchdog._timeoutMs, WATCHDOG_TIMEOUT_MS)

  // Advance by WATCHDOG_TIMEOUT_MS - 1
  await h.advance(WATCHDOG_TIMEOUT_MS - 1)
  assert.equal(h.terminated, false)

  // 1ms more fires at the centralized default limit
  await h.advance(1)
  assert.equal(h.terminated, true)
  assert.ok(h.diagnostics.some((d) => d.includes(`(limit ${WATCHDOG_TIMEOUT_MS}ms)`)))
})

test('WHAT[VERIFICATION-SYSTEM-004] timeout fires at most once even if clock continues to advance', async () => {
  const h = createHarness({ timeoutMs: 300, label: 'dog-once' })

  await h.advance(300)
  assert.equal(h.terminateCount, 1)

  await h.advance(1000)
  assert.equal(h.terminateCount, 1, 'terminate must not be called repeatedly')
})

test('WHAT[VERIFICATION-SYSTEM-004] diagnostic is flushed before terminate and onTimeout is bounded', async () => {
  const executionOrder = []
  const clock = createVirtualClock()
  const timers = createVirtualTimers(clock)

  let onTimeoutResolved = false
  const onTimeout = async () => {
    executionOrder.push('onTimeout:start')
    onTimeoutResolved = true
    executionOrder.push('onTimeout:done')
  }

  const deps = {
    clock: { nowMs: () => clock.nowMs() },
    timers,
    diagnostic: {
      write: (msg) => {
        executionOrder.push(`diagnostic:${msg.slice(0, 8)}`)
      },
    },
    terminate: () => {
      executionOrder.push('terminate')
    },
  }

  const watchdog = new Watchdog({
    timeoutMs: 200,
    label: 'dog-order',
    onTimeout,
    deps,
  })

  clock.advance(200)
  // Flush microtask ticks through Promise.race and onTimeout async completion
  for (let i = 0; i < 5; i += 1) await Promise.resolve()
  await Promise.resolve()

  assert.equal(onTimeoutResolved, true)
  assert.deepEqual(executionOrder, [
    'diagnostic:WATCHDOG',
    'onTimeout:start',
    'onTimeout:done',
    'terminate',
  ])
})

// ── Integration entry coverage red capabilities ─────────────────────────────

test('WHAT[VERIFICATION-SYSTEM-004] integration entry coverage goes red for an unwired integration test', () => {
  assert.deepEqual(
    assess(
      ['requirements/a/tests/integration/a.test.mjs', 'requirements/b/tests/integration/b.test.mjs'],
      ['requirements/a/tests/integration/a.test.mjs'],
    ),
    {
      ok: false,
      missingFromEntry: ['requirements/b/tests/integration/b.test.mjs'],
      staleEntry: [],
      duplicateWiring: [],
    },
  )
})

test('WHAT[VERIFICATION-SYSTEM-004] integration entry coverage goes red for stale or duplicate wiring', () => {
  assert.deepEqual(
    assess(
      ['requirements/a/tests/integration/a.test.mjs'],
      [
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/missing/tests/integration/missing.test.mjs',
      ],
    ),
    {
      ok: false,
      missingFromEntry: [],
      staleEntry: ['requirements/missing/tests/integration/missing.test.mjs'],
      duplicateWiring: ['requirements/a/tests/integration/a.test.mjs'],
    },
  )
})

test('WHAT[VERIFICATION-SYSTEM-004] integration entry coverage goes red when a child-owned test is not declared', () => {
  assert.deepEqual(
    assess(
      [
        'requirements/a/tests/integration/a.test.mjs',
        'requirements/distribution/tests/integration/package/layout.test.mjs',
        'requirements/distribution/tests/integration/package/contents.test.mjs',
      ],
      ['requirements/a/tests/integration/a.test.mjs'],
      ['requirements/distribution/tests/integration/package/layout.test.mjs'],
    ),
    {
      ok: false,
      missingFromEntry: ['requirements/distribution/tests/integration/package/contents.test.mjs'],
      staleEntry: [],
      duplicateWiring: [],
    },
  )
})
