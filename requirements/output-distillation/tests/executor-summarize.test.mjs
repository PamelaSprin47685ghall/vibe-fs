// tests/unit/Execution/executor-summarize.test.mjs — Distillation prompt constants
// + EXEC-023/024 targeted AwaitAgentWithPermit contract for distillSpool.
//
// The prompt is the plain intent only; the chunk/combined content is carried
// by the fork envelope's `content` field (FORK_CHILD_PAYLOAD_payload_renders_as_content).
//
// Proof plan #3: ordered/out-of-order agent completion only returns the target
// agent; each chunk awaits once when Ready (no stash skip); FamilyWaiting
// waits for a readiness signal before one fresh permit check; FamilyBlocked
// (NotFound) hard fails the chunk and cancelOwned without corrupting sibling
// targeted awaits.

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { ForkError } from '../../../dist/Session/ForkTypes.js'
import {
  agentCompletion,
  distillation,
  distillationRuntime,
  errorResult,
  okResult,
  providerLanguage,
} from '../../verification-system/tests/support/domain.mjs'

/** Spool.ChunkSizeBytes — multi-chunk files need size > n-1 full chunks. */
const SPOOL_CHUNK_BYTES = 204_800

function writeSpoolWithChunks(chunkCount) {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-sum-await-'))
  const spoolPath = join(dir, 'spool.bin')
  const size = (chunkCount - 1) * SPOOL_CHUNK_BYTES + 64
  writeFileSync(spoolPath, Buffer.alloc(size, 0x61))
  return { dir, spoolPath }
}

function completedOk(agentId) {
  return okResult(
    agentCompletion.completedRun({
      runId: `run-${agentId}`,
      agentId,
      workRecord: `summary-for-${agentId}`,
    }),
  )
}

test('DISTILLATION_distill_fragment_prompt_is_plain_intent', () => {
  assert.equal(
    distillation.distillFragmentPrompt(providerLanguage.english),
    'Distill this fragment of command output. Preserve errors, decisions, paths, and exact numbers; omit raw code.',
  )
})

test('DISTILLATION_merge_distillations_prompt_is_plain_intent', () => {
  assert.equal(
    distillation.mergeDistillationsPrompt(providerLanguage.english),
    'Merge these command-output distillations into one dense account. Preserve failures and exact facts; do not include raw code.',
  )
})

test('DISTILLATION_prompts_carry_no_chunk_index_or_level', () => {
  assert.ok(!/\bchunk\b/i.test(distillation.distillFragmentPrompt(providerLanguage.english)))
  assert.ok(!/\blevel-\d/i.test(distillation.mergeDistillationsPrompt(providerLanguage.english)))
})

test('EXEC_distill_spool_targeted_await_one_call_per_agent_no_stash', async () => {
  const { dir, spoolPath } = writeSpoolWithChunks(3)
  const forked = []
  const awaitCalls = []

  const { runtime } = distillationRuntime.fake({
    fork: (agentId) => {
      forked.push(agentId)
      return distillationRuntime.forkOk(agentId)
    },
    awaitAgent: (agentId, timeoutMs) => {
      awaitCalls.push({ agentId, timeoutMs })
      return completedOk(agentId)
    },
  })

  const summary = await distillationRuntime.distillSpool(runtime, spoolPath)

  assert.ok(typeof summary === 'string' && summary.length > 0)
  assert.ok(forked.length >= 3, `expected ≥3 forks (3 map ± reduce), got ${forked.length}`)
  assert.equal(awaitCalls.length, forked.length, 'each fork gets exactly one AwaitAgentWithPermit (no stash skip)')

  for (const id of forked) {
    const hits = awaitCalls.filter((c) => c.agentId === id)
    assert.equal(hits.length, 1, `agent ${id} awaited exactly once; hits=${hits.length}`)
  }

  const awaitIds = awaitCalls.map((c) => c.agentId)
  assert.deepEqual([...awaitIds].sort(), [...forked].sort(), 'await targets equal forked set (no cross-agent await)')

  rmSync(dir, { recursive: true, force: true })
})

test('EXEC_distill_spool_targeted_await_out_of_order_returns_own_agent', async () => {
  const { dir, spoolPath } = writeSpoolWithChunks(3)
  const forked = []
  const awaitCalls = []
  let mapSeq = 0

  const { runtime } = distillationRuntime.fake({
    fork: (agentId) => {
      forked.push(agentId)
      return distillationRuntime.forkOk(agentId)
    },
    awaitAgent: (agentId, timeoutMs) => {
      const seq = mapSeq++
      awaitCalls.push({ agentId, timeoutMs, seq })
      // Later-started map agents resolve first → out-of-order completion.
      const delayMs = Math.max(0, 30 - seq * 10)
      return new Promise((resolve) => {
        setTimeout(() => resolve(completedOk(agentId)), delayMs)
      })
    },
  })

  const summary = await distillationRuntime.distillSpool(runtime, spoolPath)

  assert.ok(typeof summary === 'string' && summary.length > 0)
  assert.ok(!/Condensation incomplete|Most recent raw output/i.test(summary), 'out-of-order success must not degrade to partial')
  assert.ok(forked.length >= 3)
  assert.equal(awaitCalls.length, forked.length, 'each fork awaited once under out-of-order resolve')

  for (const id of forked) {
    const hits = awaitCalls.filter((c) => c.agentId === id)
    assert.equal(hits.length, 1, `targeted await for ${id} exactly once (no cross-agent / stash)`)
  }

  rmSync(dir, { recursive: true, force: true })
})

test('EXEC_distill_spool_await_timeout_fails_chunk_cancels_owned_siblings_still_await', async () => {
  const { dir, spoolPath } = writeSpoolWithChunks(3)
  const forked = []
  const awaitCalls = []
  let mapForkIndex = 0
  /** Fail the second map agent only (index 1). */
  let failAgentId = null

  const { runtime, cancelled } = distillationRuntime.fake({
    fork: (agentId) => {
      if (mapForkIndex < 3) {
        if (mapForkIndex === 1) failAgentId = agentId
        mapForkIndex += 1
      }
      forked.push(agentId)
      return distillationRuntime.forkOk(agentId)
    },
    awaitAgent: (agentId, timeoutMs) => {
      awaitCalls.push({ agentId, timeoutMs })
      // Real join timeout / hard fail → NotFound (not TimedOut/Waiting retry).
      if (agentId === failAgentId) return distillationRuntime.notFound(agentId)
      return completedOk(agentId)
    },
  })

  const summary = await distillationRuntime.distillSpool(runtime, spoolPath)

  assert.ok(typeof summary === 'string')
  assert.match(summary, /Condensation incomplete|Most recent raw output/i, 'map failure yields partial account, not throw')

  const mapAgentIds = forked.slice(0, 3)
  assert.equal(mapAgentIds.length, 3)
  for (const id of mapAgentIds) {
    const hits = awaitCalls.filter((c) => c.agentId === id)
    assert.equal(hits.length, 1, `map agent ${id} still gets one targeted await (siblings not skipped)`)
  }

  assert.ok(failAgentId, 'second map agent identified')
  assert.ok(cancelled.length >= 1, 'cancelOwned runs on map failure')
  for (const id of forked) {
    assert.ok(cancelled.includes(id), `owned agent ${id} cancelled after failure`)
  }

  rmSync(dir, { recursive: true, force: true })
})

test('EXEC_distill_spool_await_not_found_hard_fail_collects_failure', async () => {
  const { dir, spoolPath } = writeSpoolWithChunks(2)
  const forked = []
  const awaitCalls = []
  let firstMapId = null

  const { runtime, cancelled } = distillationRuntime.fake({
    fork: (agentId) => {
      if (firstMapId === null) firstMapId = agentId
      forked.push(agentId)
      return distillationRuntime.forkOk(agentId)
    },
    awaitAgent: (agentId, timeoutMs) => {
      awaitCalls.push({ agentId, timeoutMs })
      // FamilyBlocked / hard fail → NotFound (requirePermit path).
      if (agentId === firstMapId) return errorResult(new ForkError(4, [`blocked:${agentId}`]))
      return completedOk(agentId)
    },
  })

  const summary = await distillationRuntime.distillSpool(runtime, spoolPath)

  assert.ok(typeof summary === 'string')
  assert.match(summary, /Condensation incomplete|Most recent raw output/i, 'NotFound hard fail collects failure, no throw-out')
  assert.equal(
    awaitCalls.filter((c) => c.agentId === firstMapId).length,
    1,
    'failed agent still awaited once',
  )
  assert.ok(cancelled.length >= 1, 'cancelOwned after hard fail')

  rmSync(dir, { recursive: true, force: true })
})

test('EXEC_distill_spool_family_waiting_waits_for_readiness_before_one_fresh_permit_check', async () => {
  const { dir, spoolPath } = writeSpoolWithChunks(1)
  const awaitCalls = []
  const callOrder = []
  const attemptsByAgent = new Map()
  const readinessSignals = []

  const { runtime } = distillationRuntime.fake({
    fork: (agentId) => distillationRuntime.forkOk(agentId),
    awaitAgent: (agentId, timeoutMs) => {
      callOrder.push(`permit:${agentId}`)
      awaitCalls.push({ agentId, timeoutMs })
      const n = (attemptsByAgent.get(agentId) ?? 0) + 1
      attemptsByAgent.set(agentId, n)
      if (n === 1) return distillationRuntime.timedOut()
      return completedOk(agentId)
    },
    // Contract required from IDistillationRuntime: this resolves only when the
    // recovery owner publishes a readiness signal for the waiting family.
    awaitRecoveryReadiness: (agentId) => {
      callOrder.push(`readiness:${agentId}`)
      readinessSignals.push(agentId)
    },
  })

  const summary = await distillationRuntime.distillSpool(runtime, spoolPath)

  assert.ok(typeof summary === 'string' && summary.length > 0)
  assert.ok(
    !/Condensation incomplete|Most recent raw output|unavailable/i.test(summary),
    'FamilyWaiting→Ready must yield full account, not partial',
  )
  assert.ok(
    summary.includes('summary-for-'),
    'Ready completion work record must appear in full account',
  )
  const targetId = awaitCalls[0]?.agentId
  assert.ok(targetId, 'map agent id recorded')
  assert.deepEqual(
    readinessSignals,
    [targetId],
    'RECOVERY_WAITING waits for the family readiness signal',
  )
  assert.deepEqual(
    callOrder,
    [`permit:${targetId}`, `readiness:${targetId}`, `permit:${targetId}`],
    'after readiness, exactly one fresh permit check/await occurs without timer-driven re-probes',
  )

  rmSync(dir, { recursive: true, force: true })
})

test('EXEC_distill_spool_family_waiting_timed_out_not_reported_as_success', async () => {
  const { dir, spoolPath } = writeSpoolWithChunks(2)
  const awaitCalls = []

  const { runtime, cancelled } = distillationRuntime.fake({
    fork: (agentId) => distillationRuntime.forkOk(agentId),
    // FamilyBlocked hard fail → ForkError.NotFound (requirePermit path).
    // Instant fail → partial + cancelOwned. Must not hang on Waiting retry budget
    // (always-TimedOut would spin until AwaitAgentTimeoutMs once Waiting retries).
    awaitAgent: (agentId, timeoutMs) => {
      awaitCalls.push({ agentId, timeoutMs })
      return errorResult(new ForkError(4, [`blocked:${agentId}`]))
    },
  })

  const summary = await distillationRuntime.distillSpool(runtime, spoolPath)

  assert.ok(typeof summary === 'string')
  assert.match(
    summary,
    /Condensation incomplete|Most recent raw output|unavailable/i,
    'FamilyBlocked (NotFound) hard fail must not report full success',
  )
  assert.ok(!summary.includes('summary-for-'), 'no fabricated success work records')
  assert.ok(awaitCalls.length >= 2, 'each map chunk still triggers AwaitAgentWithPermit')
  assert.ok(cancelled.length >= 1, 'cancelOwned after hard fail')

  rmSync(dir, { recursive: true, force: true })
})
