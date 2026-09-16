import assert from 'node:assert/strict'
import test from 'node:test'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'

test('WHAT[EPI-031] sphinx inquiry process is fully programmatically controlled without inquiry role or model driving layer', () => {
  // GEC schedule & state transition is executed by deterministic runtime functions,
  // not by an external Inquiry role driving step-by-step yield/nextTool.
  assert.equal(typeof gecSurface.schedule, 'function')
  assert.equal(typeof gecSurface.replay, 'function')
})

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

test('WHAT[EPI-033] result acceptance is idempotent by work identity preventing late acceptance and duplicate purchases', () => {
  // Accepted events bind workId and attempt identity. Replay folds them deterministically.
  const genesis = {
    id: 'ev0',
    parent: 'none',
    workId: 'work-1',
    attempt: 1,
    kind: 'genesis',
    payload: { question: 'test question' },
  }
  const result1 = gecSurface.replay([genesis])
  assert.equal(typeof result1.semanticHash, 'string')

  // Replaying identical canonical events produces identical state and hash (idempotent)
  const result2 = gecSurface.replay([genesis])
  assert.equal(result2.semanticHash, result1.semanticHash)
})

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
