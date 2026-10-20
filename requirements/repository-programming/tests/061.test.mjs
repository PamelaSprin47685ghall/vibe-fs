// LC-006: 交付格按 provider run 实施精确围栏，防止旧终态错领新任务交付
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  clearDeliveredValue,
  clearDeliveredReturned,
  recordFencedDeliveredView,
  takeDeliveredHandoffFor,
  deliveredFenceOf,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { clearAll, numberMessage, applyView } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'

test.beforeEach(() => clearAll())

test('WHAT[repository-programming-061] LC-006: fenced delivered view answers only the owning run', () => {
  const session = 'session-lc006'
  const runA = 'run-A'
  const callA = 'call-A'
  const runB = 'run-B'
  const view = { prefixUntil: 1, thenMap: {} }

  clearDeliveredValue(session)
  clearDeliveredReturned(session)
  try {
    const rows = [{ id: 'msg-0', text: 'summary-text' }]
    rows.forEach((row) => numberMessage(session, row.id))
    applyView(session, rows)

    recordFencedDeliveredView(session, runA, callA, view)

    // runB 取交付必须返回 null，且 deliveredFenceOf 保持为 runA
    assert.equal(takeDeliveredHandoffFor(session, runB, null), null)
    assert.deepEqual(deliveredFenceOf(session), { providerRun: runA, toolCallId: callA })

    // runA 才能取走
    const handoffA = takeDeliveredHandoffFor(session, runA, null)
    assert.notEqual(handoffA, null)
    assert.match(String(handoffA), /summary-text/)

    // 取走后交付格已被消费
    assert.equal(deliveredFenceOf(session), null)
    assert.equal(takeDeliveredHandoffFor(session, runA, null), null)
  } finally {
    clearDeliveredValue(session)
    clearDeliveredReturned(session)
  }
})
