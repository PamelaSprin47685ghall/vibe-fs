import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')
const SPOOL_CHUNK_BYTES = 204_800

test('WHAT[DISTILL-003] truncated_tail_is_explicitly_not_the_whole_run', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-003-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.alloc(SPOOL_CHUNK_BYTES + 256, 0x61))

  const runtime = {
    fork: (agentId) => ({ ok: true, agentId }),
    awaitAgent: (agentId) => ({ ok: true, runId: `run-${agentId}`, workRecord: 'summary' }),
    awaitRecoveryReadiness: () => undefined,
    cancel: () => undefined,
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    assert.match(summary, /Earlier command output was truncated before distillation/)
    assert.match(summary, /only the most recent 200 KiB/)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
