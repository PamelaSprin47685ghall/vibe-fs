// requirements/output-distillation/tests/distiller-fragment-humility.test.mjs
//
// Oracle 2 (HANDOFF §29): fragment humility is a behavioral fixture, not just
// Distiller Role Law prose. When one map fragment cannot be condensed, the
// distilled summary must (1) admit the condensation is incomplete, (2) keep the
// last raw chunk verbatim so a reader who never saw the original can still find
// the distinguishing marker, and (3) never fabricate a summary for the failed
// fragment or report whole-run success.
//
// Trace migration (REQUIREMENT-SYSTEM-018): the single Oracle-2 scenario is
// split into one test per WHAT proposition (DISTILL-001..006) — one test, one
// WHAT, all sharing the same failed-second-chunk setup; the assertion set of
// the original test is conserved across the split.
//
// Uses distillationRuntime.fake + distillSpool from tests/unit/support/domain/execution.mjs.

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  agentCompletion,
  distillationRuntime,
  okResult,
  providerLanguage,
} from '../../verification-system/tests/support/domain.mjs'

/** Spool.ChunkSizeBytes — a spool of this size + 64 bytes splits into 2 chunks. */
const SPOOL_CHUNK_BYTES = 204_800

/**
 * Two-chunk spool: chunk 0 is quiet filler; the distinct marker lives in
 * chunk 1 — the last raw chunk distillSpool keeps verbatim. The chunk-1 map
 * agent hard-fails (ForkError.NotFound — FamilyBlocked / real join timeout),
 * so every shared assertion below runs against the same partial-account shape.
 */
async function runFailedSecondChunkScenario() {
  const marker = 'MARKER_DISTINCT_PTY_CRASH_7f3a'
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-humility-'))
  const spoolPath = join(dir, 'spool.bin')
  const buffer = Buffer.alloc(SPOOL_CHUNK_BYTES + 64, 0x61)
  buffer.write(marker, SPOOL_CHUNK_BYTES, 'utf8')
  writeFileSync(spoolPath, buffer)

  const forked = []
  let failedMapAgentId = null

  const { runtime, cancelled } = distillationRuntime.fake({
    fork: (agentId) => {
      forked.push(agentId)
      // First two forks are the two map agents in chunk order: the second one
      // (chunk 1 — the chunk that carries the distinct marker) is the failure.
      if (forked.length === 2) failedMapAgentId = agentId
      return distillationRuntime.forkOk(agentId)
    },
    awaitAgent: (agentId) => {
      // Hard fail with ForkError.NotFound — FamilyBlocked / real join timeout —
      // which must not be retried and must not report success.
      if (agentId === failedMapAgentId) return distillationRuntime.notFound(agentId)
      // Successful fragments return a work record the caller can distinguish:
      // `summary-for-<agentId>` must appear for survivors and never for the failed one.
      return okResult(
        agentCompletion.completedRun({
          runId: `run-${agentId}`,
          agentId,
          workRecord: `summary-for-${agentId}`,
        }),
      )
    },
  })

  try {
    const summary = await distillationRuntime.distillSpool(runtime, spoolPath, providerLanguage.english)
    return { summary, cancelled, forked, failedMapAgentId, marker }
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

test('WHAT[DISTILL-001] fragment_humility_compression_is_lossy_but_honest_not_a_silent_empty_success', async () => {
  const { summary } = await runFailedSecondChunkScenario()
  assert.ok(
    typeof summary === 'string' && summary.length > 0,
    'oversized output compresses into a bounded observation, never a silent empty success',
  )
  assert.match(
    summary,
    /Condensation incomplete|Most recent raw output/,
    'each loss is an honest choice — the failure is admitted, not truncated into success',
  )
})

test('WHAT[DISTILL-002] fragment_humility_keeps_the_judgment_changing_distinguishing_marker', async () => {
  const { summary, marker } = await runFailedSecondChunkScenario()
  assert.ok(
    summary.includes(marker),
    'the specific imprint that distinguishes this failure from a generic failure story survives compression',
  )
})

test('WHAT[DISTILL-003] fragment_humility_admits_fragment_boundary_and_never_fabricates_failed_summary', async () => {
  const { summary, failedMapAgentId } = await runFailedSecondChunkScenario()
  assert.match(
    summary,
    /Condensation incomplete|Most recent raw output/,
    'fragment failure must admit incompleteness, not report whole-run success',
  )
  assert.ok(
    !summary.includes(`summary-for-${failedMapAgentId}`),
    'a fabricated summary for the failed fragment must not appear',
  )
})

test('WHAT[DISTILL-004] fragment_humility_failed_fragment_is_not_outvoted_by_quiet_chunks', async () => {
  const { summary, failedMapAgentId } = await runFailedSecondChunkScenario()
  assert.match(
    summary,
    /Condensation incomplete|Most recent raw output/,
    'one concrete failure is not voted away by many quiet chunks',
  )
  assert.ok(
    summary.includes('summary-for-'),
    'surviving chunk accounts are merged in — but quiet chunks do not make the failure unreal',
  )
  assert.ok(
    !summary.includes(`summary-for-${failedMapAgentId}`),
    'the failed fragment is honestly kept as a failure, never upgraded to a success record',
  )
})

test('WHAT[DISTILL-005] fragment_humility_raw_tail_keeps_the_locator_for_an_unseen_reader', async () => {
  const { summary, marker } = await runFailedSecondChunkScenario()
  assert.ok(
    summary.includes(marker),
    'the raw tail of the last chunk survives verbatim so a reader who never saw the original can locate the scene',
  )
})

test('WHAT[DISTILL-006] fragment_humility_failed_second_chunk_keeps_raw_tail_and_admits_incompleteness', async () => {
  const { summary, marker, cancelled } = await runFailedSecondChunkScenario()
  assert.ok(typeof summary === 'string' && summary.length > 0)
  assert.match(
    summary,
    /Condensation incomplete|Most recent raw output/,
    'map failure yields a partial account with the last raw chunk, not a throw',
  )
  assert.ok(summary.includes(marker), 'the partial account keeps the last chunk raw tail verbatim')
  assert.ok(cancelled.length >= 1, 'cancelOwned must cancel the owned map/reduce agents on fragment failure')
})
