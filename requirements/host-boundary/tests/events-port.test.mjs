// EVT: the one HostEventPort — per-run Completed dedupe (HOST-012), sticky
// replay for late subscribers, and listener disposal.

import assert from 'node:assert/strict'
import test from 'node:test'

const { Events_HostEventPort, TerminalOutcome } = await import('../../../dist/OpenCode/Host/Events.js')
const { AgentRunResult } = await import('../../../dist/Foundation/Outcome.js')
const { Role } = await import('../../../dist/Foundation/Roles.js')
const { providerRun, sessionId } = await import('../../verification-system/tests/support/domain.mjs')

const completed = (sid, run) =>
  new TerminalOutcome(0, [
    new AgentRunResult(sessionId(sid), undefined, run ? providerRun(run) : undefined, Role.Coder, undefined, 'wide', 'turn'),
  ])
const failed = (error) => new TerminalOutcome(2, [error])
const aborted = (reason) => new TerminalOutcome(1, [reason])

test('EVT_duplicate_completed_for_the_same_provider_run_is_absorbed', () => {
  const port = new Events_HostEventPort()
  const received = []
  port.SubscribeTerminalListener((sid, outcome) => received.push([sid.fields[0], outcome.tag]))

  const first = port.NotifyTerminal(sessionId('ses_dup'), completed('ses_dup', 'run-1'))
  assert.equal(first, true, 'a delivered terminal reports the live listener')

  // HOST-012: root and worktree instances both reconcile the child and both
  // notify; the second Completed for the same run must not complete a fresh
  // run with the previous run's outcome.
  const second = port.NotifyTerminal(sessionId('ses_dup'), completed('ses_dup', 'run-1'))
  assert.equal(second, false, 'the same-run duplicate is absorbed')
  assert.equal(received.length, 1, 'the listener sees exactly one Completed per provider run')

  const third = port.NotifyTerminal(sessionId('ses_dup'), completed('ses_dup', 'run-2'))
  assert.equal(third, true, 'a new provider run is a fresh terminal')
  assert.equal(received.length, 2)
})

test('EVT_completed_without_provider_run_is_never_a_duplicate', () => {
  const port = new Events_HostEventPort()
  const received = []
  port.SubscribeTerminalListener((_sid, outcome) => received.push(outcome.tag))

  port.NotifyTerminal(sessionId('ses_norun'), completed('ses_norun', null))
  port.NotifyTerminal(sessionId('ses_norun'), completed('ses_norun', null))
  assert.equal(received.length, 2, 'run-less completions always go through')
})

test('EVT_failed_and_aborted_outcomes_are_not_deduped', () => {
  const port = new Events_HostEventPort()
  const received = []
  port.SubscribeTerminalListener((_sid, outcome) => received.push(outcome.tag))

  port.NotifyTerminal(sessionId('ses_f'), failed('e1'))
  port.NotifyTerminal(sessionId('ses_f'), failed('e1'))
  port.NotifyTerminal(sessionId('ses_f'), aborted('gone'))
  assert.deepEqual(received, [2, 2, 1], 'non-Completed outcomes always reach listeners')
})

test('EVT_late_subscriber_replays_the_last_sticky_outcome_per_session', () => {
  const port = new Events_HostEventPort()
  const sidA = sessionId('ses_replay_a')
  const sidB = sessionId('ses_replay_b')

  assert.equal(port.NotifyTerminal(sidA, failed('a-first')), false, 'no listeners yet')
  port.NotifyTerminal(sidA, failed('a-last'))
  port.NotifyTerminal(sidB, aborted('b-only'))

  const replayed = []
  port.SubscribeTerminalListener((sid, outcome) => replayed.push([sid.fields[0], outcome.tag, outcome.fields[0]]))
  assert.deepEqual(
    replayed.sort(),
    [
      ['ses_replay_a', 2, 'a-last'],
      ['ses_replay_b', 1, 'b-only'],
    ].sort(),
    'sticky replay hands the LAST outcome of every live session to the late subscriber',
  )
})

test('EVT_disposed_listener_stops_delivery_and_listener_count_reporting', () => {
  const port = new Events_HostEventPort()
  const received = []
  const subscription = port.SubscribeTerminalListener((_sid, outcome) => received.push(outcome.tag))

  port.NotifyTerminal(sessionId('ses_dispose'), failed('before'))
  assert.equal(received.length, 1)

  subscription.Dispose()
  const delivered = port.NotifyTerminal(sessionId('ses_dispose'), failed('after'))
  assert.equal(received.length, 1, 'a disposed listener receives nothing more')
  assert.equal(delivered, false, 'NotifyTerminal reports zero live listeners')
})
