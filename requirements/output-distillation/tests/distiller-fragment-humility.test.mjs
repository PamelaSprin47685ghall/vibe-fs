// requirements/output-distillation/tests/distiller-fragment-humility.test.mjs
//
// Oracle 2 (HANDOFF §29): fragment humility is a behavioral fixture, not just
// Distiller Role Law prose. When one map fragment cannot be condensed, the
// distilled summary must (1) admit the condensation is incomplete, (2) keep the
// last raw chunk verbatim so a reader who never saw the original can still find
// the distinguishing marker, and (3) never fabricate a summary for the failed
// fragment or report whole-run success.
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

test('DISTILLER_fragment_humility_failed_second_chunk_keeps_raw_tail_and_admits_incompleteness', async () => {
  // Two-chunk spool: chunk 1 (bytes [0, SPOOL_CHUNK_BYTES)) is quiet filler; the
  // distinct marker lives in chunk 2 — the last raw chunk distillSpool keeps verbatim.
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
    awaitAgent: (agentId, _timeoutMs) => {
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

  const summary = await distillationRuntime.distillSpool(runtime, spoolPath, providerLanguage.english)

  assert.ok(typeof summary === 'string' && summary.length > 0)
  assert.match(summary, /Condensation incomplete|Most recent raw output/, 'fragment failure must admit incompleteness, not report whole-run success')
  assert.ok(summary.includes(marker), 'raw tail of the last chunk must survive verbatim for a reader who never saw the original')
  assert.ok(
    !summary.includes(`summary-for-${failedMapAgentId}`),
    'a fabricated summary for the failed fragment must not appear',
  )
  assert.ok(cancelled.length >= 1, 'cancelOwned must cancel the owned map/reduce agents on fragment failure')

  rmSync(dir, { recursive: true, force: true })
})
