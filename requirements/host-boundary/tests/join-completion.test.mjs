import assert from 'node:assert/strict'
import test from 'node:test'
import * as EventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.value ?? outcome.error ?? '')

test('WHAT[HOST-BOUNDARY-016] EXEC_join_NotifyTerminal_then_late_SubscribeTerminal_replays_sticky', () => {
  const port = EventsSurface.create()
  notify(port, 'ses_sticky_child', { kind: 'Completed', providerRun: 'run-1', value: 'done' })
  const seen = []
  EventsSurface.subscribe(port, (sessionId, outcome) => seen.push({ sessionId, outcome }))
  assert.equal(seen.length, 1)
  assert.equal(seen[0].sessionId, 'ses_sticky_child')
  assert.equal(seen[0].outcome.text, 'done')
})

test('WHAT[HOST-BOUNDARY-016] EXEC_join_Failed_outcomes_are_not_provider_run_deduped', () => {
  const port = EventsSurface.create()
  let count = 0
  EventsSurface.subscribe(port, () => { count += 1 })
  notify(port, 'ses_dedupe', { kind: 'Failed', providerRun: 'run-1', error: 'first' })
  notify(port, 'ses_dedupe', { kind: 'Failed', providerRun: 'run-1', error: 'second' })
  assert.equal(count, 2)
})
