// tests/unit/process/pty-types.test.mjs — PtyTypes DU surface: signal codec,
// PtyId, PtyCommand/PtyHandle/PtyRead/ReadPlan shapes.

import assert from 'node:assert/strict'
import test from 'node:test'

import { caseOf, payloadOf, resultOf } from '../support/domain.mjs'

const {
  PtySignal,
  PtySignalModule_tryParse,
  PtyCommand,
  PtyId,
  PtyId_Create_Z721C83C5,
  PtyId__get_Value,
  PtyHandle,
  PtyRead,
  ReadPlan,
} = await import('../../../dist/Process/PtyTypes.js')

// ── PtySignal.tryParse ───────────────────────────────────────────────────────

test('PTY_TYPES_tryParse_accepts_every_supported_signal_name', () => {
  const expected = [
    ['TERM', 'Terminate'],
    ['KILL', 'Kill'],
    ['INT', 'Interrupt'],
    ['HUP', 'Hangup'],
    ['QUIT', 'Quit'],
    ['USR1', 'User1'],
    ['USR2', 'User2'],
  ]
  for (const [wire, caseName] of expected) {
    const parsed = resultOf(PtySignalModule_tryParse(wire))
    assert.equal(parsed.ok, true, wire)
    assert.equal(caseOf(parsed.value), caseName, wire)
  }
})

test('PTY_TYPES_tryParse_rejects_unknown_and_prefixed_names', () => {
  for (const bad of ['SIGTERM', 'term', '', 'SIGKILL', 'STOP']) {
    const parsed = resultOf(PtySignalModule_tryParse(bad))
    assert.equal(parsed.ok, false, bad)
    assert.match(String(parsed.error), /Unsupported PTY signal/)
    if (bad !== '') assert.ok(String(parsed.error).includes(bad), `${bad} echoed in error`)
  }
})

test('PTY_TYPES_tryParse_returns_the_canonical_static_cases', () => {
  // The static singletons are what Signal commands carry; the codec must produce them.
  assert.equal(payloadOf(PtySignalModule_tryParse('TERM')), PtySignal.Terminate)
  assert.equal(payloadOf(PtySignalModule_tryParse('KILL')), PtySignal.Kill)
  assert.equal(payloadOf(PtySignalModule_tryParse('INT')), PtySignal.Interrupt)
  assert.equal(payloadOf(PtySignalModule_tryParse('HUP')), PtySignal.Hangup)
  assert.equal(payloadOf(PtySignalModule_tryParse('QUIT')), PtySignal.Quit)
  assert.equal(payloadOf(PtySignalModule_tryParse('USR1')), PtySignal.User1)
  assert.equal(payloadOf(PtySignalModule_tryParse('USR2')), PtySignal.User2)
})

// ── PtyId ────────────────────────────────────────────────────────────────────

test('PTY_TYPES_pty_id_roundtrips_its_value', () => {
  const id = PtyId_Create_Z721C83C5('pty-deadbeef')
  assert.equal(PtyId__get_Value(id), 'pty-deadbeef')
  assert.equal(caseOf(id), 'PtyId')
})

// ── PtyCommand / PtyHandle / PtyRead / ReadPlan shapes ───────────────────────

test('PTY_TYPES_pty_command_cases_carry_their_fields', () => {
  const spawn = new PtyCommand(0, ['sh -c ls', '/tmp'])
  assert.equal(caseOf(spawn), 'Spawn')
  assert.deepEqual(payloadOf(spawn), ['sh -c ls', '/tmp'])

  const bytes = new TextEncoder().encode('abc')
  const write = new PtyCommand(1, [bytes])
  assert.equal(caseOf(write), 'Write')
  assert.deepEqual([...payloadOf(write)], [97, 98, 99])

  assert.equal(caseOf(PtyCommand.Read), 'Read')

  const signal = new PtyCommand(3, [PtySignal.Hangup])
  assert.equal(caseOf(signal), 'Signal')
  assert.equal(caseOf(payloadOf(signal)), 'Hangup')

  const resize = new PtyCommand(4, [120, 40])
  assert.equal(caseOf(resize), 'Resize')
  assert.deepEqual(payloadOf(resize), [120, 40])
})

test('PTY_TYPES_pty_handle_and_read_records_expose_fields', () => {
  const agent = { Name: 'fast-executor' }
  const id = PtyId_Create_Z721C83C5('pty-1')
  const handle = new PtyHandle(id, 'sleep 1', new Date(), agent)
  assert.equal(handle.Command, 'sleep 1')
  assert.equal(PtyId__get_Value(handle.Id), 'pty-1')
  assert.equal(handle.Agent.Name, 'fast-executor')

  const read = new PtyRead(id, 'partial output', true)
  assert.equal(read.Output, 'partial output')
  assert.equal(read.Closed, true)
})

test('PTY_TYPES_read_plan_cases_exist_for_buffered_read', () => {
  assert.equal(caseOf(new ReadPlan(0, ['no such id'])), 'Unknown')
  assert.equal(caseOf(new ReadPlan(1, [])), 'AlreadyInProgress')
  assert.equal(caseOf(new ReadPlan(2, [])), 'ClosedImmediate')
  assert.equal(caseOf(new ReadPlan(3, [null])), 'Park')
})
