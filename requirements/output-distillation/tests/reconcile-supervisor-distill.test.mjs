// Split from tests/unit/execution/reconcile-supervisor.test.mjs (cutover Wave 2a);
// owner: output-distillation. DISTILL-006：唯一 Distiller 失败时，物理取消
// 至多一次并返回 bounded raw tail，不把异常抛穿 host-boundary。

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

const { distillSpool } = await import('../../../dist/OpenCode/Tools/DistillationSurface.js')

test('WHAT[DISTILL-006] EXEC_distillation_cancel_single_owned_distiller_once_on_failure', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-sum-'))
  const spoolPath = join(dir, 'spool.bin')
  // One small spool → one Distiller; Join NotFound → failure → cancelOwned.
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

  const summary = await distillSpool(runtime, spoolPath, 'en')
  assert.ok(typeof summary === 'string', 'distillSpool returns partial text, not throw')
  assert.equal(forked.length, 1, 'exactly one Distiller is forked')
  assert.deepEqual(cancelled, forked, 'the one owned Distiller is cancelled exactly once')

  rmSync(dir, { recursive: true, force: true })
})
