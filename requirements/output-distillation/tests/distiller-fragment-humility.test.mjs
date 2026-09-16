// requirements/output-distillation/tests/distiller-fragment-humility.test.mjs
// Owner: output-distillation. Truncation is explicit and the single Distiller
// may only speak about the bounded tail it actually received.

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')

const SPOOL_CHUNK_BYTES = 204_800

async function runTruncatedTailScenario() {
  const earlyMarker = 'EARLY_CONTEXT_NOT_OBSERVED_91aa'
  const tailMarker = 'LATEST_PTY_CRASH_7f3a'
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-tail-'))
  const spoolPath = join(dir, 'spool.bin')
  const first = Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61)
  first.write(earlyMarker, 0, 'utf8')
  const last = Buffer.alloc(256, 0x62)
  last.write(tailMarker, 0, 'utf8')
  writeFileSync(spoolPath, Buffer.concat([first, last]))

  let payload = null
  const runtime = {
    fork: (agentId, _prompt, body) => {
      payload = body
      return { ok: true, agentId }
    },
    awaitAgent: (agentId) => ({
      ok: true,
      runId: `run-${agentId}`,
      workRecord: payload.includes(tailMarker)
        ? `Observed exact failure marker ${tailMarker}`
        : 'tail marker missing',
    }),
    awaitRecoveryReadiness: () => undefined,
    cancel: () => undefined,
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    return { summary, payload, earlyMarker, tailMarker }
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

test('WHAT[DISTILL-001] truncation_produces_nonempty_bounded_observation', async () => {
  const { summary, payload } = await runTruncatedTailScenario()
  assert.ok(summary.length > 0)
  assert.ok(Buffer.byteLength(payload, 'utf8') <= SPOOL_CHUNK_BYTES)
})

test('WHAT[DISTILL-002] bounded_tail_keeps_recent_judgment_changing_marker', async () => {
  const { summary, tailMarker } = await runTruncatedTailScenario()
  assert.ok(summary.includes(tailMarker))
})

test('WHAT[DISTILL-003] truncated_tail_is_explicitly_not_the_whole_run', async () => {
  const { summary } = await runTruncatedTailScenario()
  assert.match(summary, /Earlier command output was truncated before distillation/)
  assert.match(summary, /only the most recent 200 KiB/)
})

test('WHAT[DISTILL-005] unseen_reader_gets_locator_plus_visible_truncation_boundary', async () => {
  const { summary, tailMarker, earlyMarker } = await runTruncatedTailScenario()
  assert.ok(summary.includes(tailMarker), 'recent locator remains usable')
  assert.ok(!summary.includes(earlyMarker), 'discarded earlier bytes are not fabricated back into the account')
  assert.match(summary, /truncated before distillation/)
})
