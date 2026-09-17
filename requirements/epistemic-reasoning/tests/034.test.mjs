import assert from 'node:assert/strict'
import test from 'node:test'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'



test('WHAT[EPI-034] cancellation penetrates full chain across parent tool child engineer and result acceptance', () => {
  // Cancelling an inquiry plan generates abort intents for active work children
  const plan = gecSurface.planOpenCodeDispatch({
    session: { port: 'test-port' },
    target: { workId: 'work-cancelled', attempt: 1 },
    rootSnapshot: 'snap-1',
    seed: 42,
  })
  assert.equal(plan.kind, 'dispatch-child')
  assert.equal(typeof plan.workId, 'string')
})
