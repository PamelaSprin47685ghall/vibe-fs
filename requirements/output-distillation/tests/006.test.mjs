import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')
const { formatSpooledOutcome } = await import('../../../dist/OpenCode/Tools/ExecutorToolSurface.js')
const SPOOL_CHUNK_BYTES = 204_800

test('WHAT[DISTILL-006] EXEC_distill_spool_failure_returns_bounded_raw_tail_and_cancels_once', async () => {
  const marker = 'LATEST_RAW_FAILURE_4d1c'
  const first = Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61)
  const last = Buffer.from(marker)
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-006-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.concat([first, last]))

  const forked = []
  const cancelled = []
  const runtime = {
    fork: (agentId) => {
      forked.push(agentId)
      return { ok: true, agentId }
    },
    awaitAgent: (agentId) => ({ ok: false, kind: 'not-found', error: `blocked:${agentId}` }),
    awaitRecoveryReadiness: () => undefined,
    cancel: (agentId) => cancelled.push(agentId),
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    assert.match(summary, /Condensation failed:/)
    assert.match(summary, /Earlier command output was truncated before distillation/)
    assert.ok(summary.includes(marker))
    assert.equal(forked.length, 1)
    assert.deepEqual(cancelled, [forked[0]])
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})

test('WHAT[DISTILL-006] RUN_spooled_outcome_preserves_condensation_failure_as_toml_comment', () => {
  const failure = 'Condensation failed: error\n\nMost recent raw output:\nfoo'
  const text = formatSpooledOutcome(1, failure)
  assert.match(text, /# Condensation failed: error/)
  assert.match(text, /exit_code = 1/)
})

test('WHAT[DISTILL-006] EXEC_distillation_cancel_single_owned_distiller_once_on_failure', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-sum-cancel-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.from('chunk-body-for-summarize'))

  const forked = []
  const cancelled = []
  const runtime = {
    fork: (agentId) => {
      forked.push(agentId)
      return { ok: true, agentId }
    },
    awaitAgent: () => ({ ok: false, kind: 'not-found', error: 'join-not-found' }),
    awaitRecoveryReadiness: () => undefined,
    cancel: (agentId) => cancelled.push(agentId),
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    assert.ok(typeof summary === 'string')
    assert.equal(forked.length, 1)
    assert.deepEqual(cancelled, forked)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
