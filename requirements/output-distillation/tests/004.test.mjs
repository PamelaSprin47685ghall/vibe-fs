import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')
const SPOOL_CHUNK_BYTES = 204_800

test('WHAT[DISTILL-004] EXEC_distill_spool_never_fans_out_or_reduces_when_spool_grows', async () => {
  const chunks = Array.from({ length: 12 }, (_, index) => Buffer.alloc(SPOOL_CHUNK_BYTES, 0x61 + (index % 20)))
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-004-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.concat(chunks))

  const forked = []
  const awaitCalls = []
  const runtime = {
    fork: (agentId) => {
      forked.push(agentId)
      return { ok: true, agentId }
    },
    awaitAgent: (agentId, timeoutMs) => {
      awaitCalls.push({ agentId, timeoutMs })
      return { ok: true, runId: `run-${agentId}`, workRecord: 'summary' }
    },
    awaitRecoveryReadiness: () => undefined,
    cancel: () => undefined,
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    assert.ok(summary.length > 0)
    assert.equal(forked.length, 1, 'spool size must not create map/reduce Distiller fan-out')
    assert.equal(awaitCalls.length, 1, 'the single Distiller is the only awaited child')
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
