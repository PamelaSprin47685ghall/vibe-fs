// Split from tests/unit/execution/reconcile-supervisor.test.mjs (cutover Wave 2a);
// owner: output-distillation. DISTILL-007：任一 map 失败 → cancelOwned 取消全部
// owned map agents，distillSpool 返回 partial text 不抛（reconcile machinery →
// host-boundary）。

import assert from 'node:assert/strict'
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { distillationRuntime } from '../../verification-system/tests/support/domain.mjs'

test('WHAT[DISTILL-007] EXEC_distillation_cancel_owned_on_failure', async () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-sum-'))
  const spoolPath = join(dir, 'spool.bin')
   // One small chunk → one map agent; Join NotFound → map failure → cancelOwned.
  writeFileSync(spoolPath, Buffer.from('chunk-body-for-summarize'))

  const forked = []
  const { runtime, cancelled } = distillationRuntime.fake({
    fork: (agentId) => {
      forked.push(agentId)
      return distillationRuntime.forkOk(agentId)
    },
     join: () => distillationRuntime.notFound(),
  })

  const summary = await distillationRuntime.distillSpool(runtime, spoolPath)
  assert.ok(typeof summary === 'string', 'distillSpool returns partial text, not throw')
  assert.ok(forked.length >= 1, 'at least one map agent forked')
  assert.ok(
    cancelled.length >= 1,
    `CancelAgent must run for owned forked ids on map failure; forked=${forked.join(',')} cancelled=${cancelled.join(',')}`,
  )
  for (const id of forked) {
    assert.ok(cancelled.includes(id), `owned agent ${id} must be cancelled`)
  }

  rmSync(dir, { recursive: true, force: true })
})
