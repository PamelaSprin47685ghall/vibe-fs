// TIME-006 — a deadline escape produces a plain timeout reason.

import assert from 'node:assert/strict'
import test from 'node:test'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')
const process = await import('../../../dist/Process/Surface.js')

const descriptor = () =>
  causal.createWait({
    waitKind: 'until-signal-or-deadline',
    owner: causal.owner('test-workflow', { id: 'until-signal' }),
    subject: { name: 'coverage' },
    producer: causal.externalProducer('journal', { rev: 'n' }),
    escapes: [causal.escape('openEndedExternal')],
    source: 'until-signal-or-deadline.test',
  })

test('WHAT[TIME-006] THEOREM_untilSignalOrDeadline_deadline_without_material_is_WaitTimedOut', async () => {
  const registry = causal.createRegistry()
  const timer = process.createVirtualTimer()
  const handle = process.timerDelay(timer, 100)
  const pending = causal.untilSignalOrDeadline(
    registry,
    descriptor(),
    handle,
    () => null,
    () => new Promise(() => {}),
  )

  await new Promise((resolve) => setImmediate(resolve))
  process.timerAdvance(timer, 100)
  assert.deepEqual(await pending, { ok: false, reason: 'WaitTimedOut' })
  assert.equal(causal.snapshot(registry).active.length, 0)
  process.timerDispose(timer)
})
