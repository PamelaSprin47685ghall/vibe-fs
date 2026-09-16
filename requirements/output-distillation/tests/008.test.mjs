import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')

test('WHAT[DISTILL-008] EXEC_distill_spool_waiting_rechecks_same_exact_agent_after_readiness', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-distill-008-'))
  const spoolPath = join(dir, 'spool.bin')
  writeFileSync(spoolPath, Buffer.from('tail'))

  const order = []
  let attempts = 0
  const forked = []
  const runtime = {
    fork: (agentId) => {
      forked.push(agentId)
      return { ok: true, agentId }
    },
    awaitAgent: (agentId) => {
      order.push(`permit:${agentId}`)
      attempts += 1
      return attempts === 1 ? { ok: false, kind: 'waiting' } : { ok: true, runId: `run-${agentId}`, workRecord: `summary-for-${agentId}` }
    },
    awaitRecoveryReadiness: (agentId) => order.push(`readiness:${agentId}`),
    cancel: () => undefined,
  }

  try {
    const summary = await distillSpool(runtime, spoolPath, 'en')
    const id = forked[0]
    assert.ok(summary.includes('summary-for-'))
    assert.deepEqual(order, [`permit:${id}`, `readiness:${id}`, `permit:${id}`])
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
})
