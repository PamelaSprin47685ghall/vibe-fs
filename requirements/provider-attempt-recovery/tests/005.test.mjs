import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdirSync, mkdtempSync, readFileSync, realpathSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { runVerificationToolProbe } from '../../../scripts/lib/verification-tool-probe.mjs'
import { withFinitePhysicalWait } from '../../verification-system/tests/support/finite-physical-wait.mjs'
import { writeRoutingHome } from '../../verification-system/tests/support/routing-home.mjs'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as owner from '../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js'
import { current, run } from './support/retry.mjs'

const { budget, providerFailureProjection: projection } = owner
const exhaustedState = () => projection.applyFailure(
  budget.attemptIdentity('session-retry', 'logical-retry', 'root-retry', 'last-failure'),
  1, current(), { version: 1, limit: 1 }, 'provider_error',
).value

test('WHAT[provider-attempt-recovery-005] finite budget boundaries stop automatic permission at the configured count', () => {
  assert.ok(Number.isSafeInteger(budget.defaultBudget) && budget.defaultBudget > 0)
  for (const limit of [1, 3, budget.defaultBudget]) {
    assert.equal(budget.verdict(limit, { failures: limit - 1 }), 'MayRetry')
    assert.equal(budget.verdict(limit, { failures: limit }), 'Exhausted')
    assert.equal(budget.verdict(limit, { failures: limit + 1 }), 'Exhausted')
  }
  assert.equal(budget.verdict(0, budget.initial), 'Exhausted')
})

test('WHAT[provider-attempt-recovery-005] durable exhaustion reads only the persisted limit', () => {
  const exhausted = exhaustedState()
  assert.equal(projection.read(exhausted).exhausted, true)
  assert.deepEqual(projection.read(exhausted).budgetSpec, { version: 1, limit: 1 })
  assert.equal(projection.mayRetry(exhausted), false)
  assert.equal(projection.applyExhausted(1, exhausted).ok, true)
  assert.equal(projection.applyExhausted(2, exhausted).ok, false)
})

test('WHAT[provider-attempt-recovery-005] real retry engine calls neither admission nor redispatch after exhaustion', async () => {
  const attempt = run('ProviderTransient', exhaustedState())
  assert.equal(await attempt.completed, 'Terminal')
  assert.deepEqual(attempt.events, [])
})

test('WHAT[provider-attempt-recovery-005] ledger records exhaustion exactly at its current finite limit and preserves it on reopen', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-retry-limit-'))
  let opened = await journal.JournalSurface_bootWithWriterId(directory, 'writer-limit', 'runtime-limit', 1, '2026-01-01T00:00:00Z')
  assert.equal(opened.ok, true)
  try {
    assert.equal((await owner.acceptHumanRoot(opened.journal, 'session-limit', 'physical-root', 'engineer')).ok, true)
    for (let count = 1; count <= budget.defaultBudget; count++) {
      const recorded = await owner.recordConfirmedFailure(opened.journal, 'session-limit', `run-${count}`, 'provider failure')
      assert.deepEqual(recorded, { ok: true, outcome: count === budget.defaultBudget ? 'RetryExhausted' : 'RetryAuthorized' })
    }
    const before = owner.snapshot(opened.journal, 'session-limit')
    assert.equal(before.failures, budget.defaultBudget)
    assert.equal(before.exhausted, true)
    journal.JournalSurface_dispose(opened.journal)
    opened = null
    opened = await journal.JournalSurface_bootWithWriterId(directory, 'writer-reopened-limit', 'runtime-limit-reopened', 2, '2026-01-01T00:00:01Z')
    assert.equal(opened.ok, true)
    assert.deepEqual(owner.snapshot(opened.journal, 'session-limit'), before)
    assert.deepEqual(await owner.recordConfirmedFailure(opened.journal, 'session-limit', 'later-run', 'provider failure'), { ok: true, outcome: 'RetryExhausted' })
    assert.deepEqual(owner.snapshot(opened.journal, 'session-limit'), before)
  } finally {
    if (opened?.ok) journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
})

test.todo('WHAT[provider-attempt-recovery-005] full recovery workflow reaches exhaustion without an extra physical Host send (GAP-139)')

for (const scenario of ['closed', 'before-physical', 'after-release', 'valid']) {
  test(`WHAT[provider-attempt-recovery-005] actual recovery ${scenario} has no send before durable admission and cold reads the exact committed budget`, async t => {
    const root = realpathSync(mkdtempSync(join(tmpdir(), 'budget-physical-writer-')))
    const packageRoot = fileURLToPath(new URL('../../../', import.meta.url))
    const physicalChild = fileURLToPath(new URL('./support/provider-failure-writer.fixture.mjs', import.meta.url))
    const coldChild = fileURLToPath(new URL('./support/provider-failure-cold.fixture.mjs', import.meta.url))
    const receiptPath = join(root, 'physical.json')
    const coldPath = join(root, 'cold.json')
    mkdirSync(join(root, 'store'))
    const home = writeRoutingHome(join(root, 'home'))
    const env = { ...process.env, HOME: home, USERPROFILE: home }
    delete env.NODE_TEST_CONTEXT
    delete env.NODE_OPTIONS
    delete env.WANXIANGSHU_NO_FATAL_EXIT
    let completed = false
    try {
      await withFinitePhysicalWait(t, { operation: `provider failure writer ${scenario} and cold read`, limitMs: 15000 }, async () => {
        await runVerificationToolProbe(process.execPath, [physicalChild, packageRoot, join(root, 'store'), scenario, receiptPath], { cwd: root, env, signal: t.signal })
        const measured = JSON.parse(readFileSync(receiptPath, 'utf8'))
        assert.equal(measured.accepted, true)
        assert.equal(measured.closed, true)
        assert.equal(measured.sendDelta, scenario === 'valid' ? 1 : 0)
        assert.equal(measured.failureFactCount, scenario === 'valid' || scenario === 'after-release' ? 1 : 0)
        await runVerificationToolProbe(process.execPath, [coldChild, packageRoot, receiptPath, coldPath], { cwd: root, env, signal: t.signal })
        const cold = JSON.parse(readFileSync(coldPath, 'utf8'))
        assert.notEqual(cold.pid, measured.pid)
        assert.notEqual(cold.pid, process.pid)
        assert.equal(cold.originalBytesPreserved, true)
        assert.equal(cold.writerCreated, false)
        assert.deepEqual(cold.actual, measured.after)
        t.diagnostic(JSON.stringify({ scenario, physicalPid: measured.pid, coldPid: cold.pid, sendDelta: measured.sendDelta, counts: measured.counts }))
      })
      completed = true
    } finally {
      if (completed) rmSync(root, { recursive: true, force: true })
      else t.diagnostic('FAILURE_BUDGET_EVIDENCE: retained ' + root)
    }
  })
}
