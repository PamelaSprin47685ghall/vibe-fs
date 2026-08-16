// Split from tests/unit/temporal/until-signal-or-deadline.test.mjs (cutover Wave 2a); owner: time-capability
//
// TIME-006 — deadline is a causal-wait optional escape: IDeadlineHandle as the
// wait escape. Without material, the deadline verdict is WaitTimedOut; the
// CausalAwait vocabulary assertions moved to causal-wait (CAUSAL-005).

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  causalAwait,
  causalWait,
  CausalWaitRegistry,
  caseOf,
  listItems,
  timerPort,
} from '../../verification-system/tests/support/domain.mjs'

const owner = () => causalWait.owner('test-workflow', [['id', 'until-signal']])

const descriptor = () =>
  causalWait.create({
    waitKind: 'until-signal-or-deadline',
    owner: owner(),
    subject: [['name', 'coverage']],
    producer: causalWait.externalProducer('journal', [['rev', 'n']]),
    escapes: [causalWait.escape.openEndedExternal()],
    source: 'until-signal-or-deadline.test',
  })

const activeCount = (registry) => listItems(registry.Snapshot().Active).length

test('WHAT[TIME-006] THEOREM_untilSignalOrDeadline_deadline_without_material_is_WaitTimedOut', async () => {
  const registry = new CausalWaitRegistry()
  const { rawPort, advance } = timerPort.createVirtual()
  const handle = rawPort.Delay(100)
  const pending = causalAwait.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => null,
    () => new Promise(() => {}),
  )
  await new Promise((r) => setImmediate(r))
  advance(100)
  const result = await pending
  assert.equal(caseOf(result), 'Error')
  assert.equal(caseOf(result.fields[0]), 'WaitTimedOut')
  assert.equal(activeCount(registry), 0)
})
