import assert from 'node:assert/strict'
import test from 'node:test'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'



test('WHAT[EPI-032] internal engineer investigation is read-only synchronous budgeted cancellable without fission or devops', () => {
  // Dispatch planning describes blind child execution for semantic investigation:
  // depth is strictly 1 (no recursive subagents, no fission, no devops delegation).
  const plan = gecSurface.planOpenCodeDispatch({
    session: { port: 'test-port' },
    target: { workId: 'work-1', attempt: 1 },
    rootSnapshot: 'snap-1',
    seed: 42,
  })
  assert.equal(plan.kind, 'dispatch-child')
  assert.equal(plan.depth, 1)
  assert.equal(plan.workId, 'work-1')
})
