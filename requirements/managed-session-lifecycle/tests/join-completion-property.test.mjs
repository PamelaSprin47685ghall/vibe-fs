import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'

const completionKind = fc.constantFrom('Terminal', 'SendFailure', 'Cancelled')
const token = fc
  .array(fc.constantFrom(...'abcdefghijklmnopqrstuvwxyz0123456789'), { minLength: 1, maxLength: 24 })
  .map((characters) => characters.join(''))
const handleKind = fc.constantFrom('agent', 'pty', 'manager-job')
const completionRace = fc.array(completionKind, { minLength: 2, maxLength: 64 })
const propertyOptions = { seed: 0x4d534c07, numRuns: 1_000 }

const makeHandle = (kind, value) => {
  if (kind === 'agent') return handles.handleIdAgent(value)
  if (kind === 'pty') return handles.handleIdPty(value)
  return handles.handleIdManagerJob(value)
}

const link = (state, handle, child) => {
  const result = handles.apply(state, {
    op: 'link',
    handle,
    child,
    agent: 'fast-coder',
    role: 'Coder',
  })
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result.state
}

const assertLateCompletionRejected = (result) => {
  assert.deepEqual(result, {
    ok: false,
    error: { kind: 'TransitionRejected', reason: 'AlreadyCompleted' },
  })
}

test('WHAT[MANAGED-SESSION-007] every completion race preserves the first production winner', () => {
  const empty = handles.empty()

  fc.assert(
    fc.property(handleKind, token, completionRace, (kind, suffix, arrivals) => {
      const winnerHandle = makeHandle(kind, `winner-${suffix}`)
      const decoyHandle = makeHandle(kind, `decoy-${suffix}`)
      let initial = link(empty, winnerHandle, `winner-child-${suffix}`)
      initial = link(initial, decoyHandle, `decoy-child-${suffix}`)
      const initialWinner = handles.read(initial, winnerHandle)
      const initialDecoy = handles.read(initial, decoyHandle)

      const first = handles.apply(initial, {
        op: 'complete',
        handle: winnerHandle,
        kind: arrivals[0],
      })
      assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
      assert.deepEqual(
        {
          lifecycle: handles.read(first.state, winnerHandle).lifecycle,
          completion: handles.read(first.state, winnerHandle).completion,
        },
        { lifecycle: 'CompletedAwaitingJoin', completion: arrivals[0] },
      )
      assert.deepEqual(handles.read(initial, winnerHandle), initialWinner)
      assert.deepEqual(handles.read(first.state, decoyHandle), initialDecoy)

      for (const late of arrivals.slice(1)) {
        assertLateCompletionRejected(
          handles.apply(first.state, {
            op: 'complete',
            handle: winnerHandle,
            kind: late,
          }),
        )
        assert.equal(handles.read(first.state, winnerHandle).completion, arrivals[0])
        assert.deepEqual(handles.read(first.state, decoyHandle), initialDecoy)
      }
    }),
    propertyOptions,
  )
})

test('WHAT[MANAGED-SESSION-007] first-wins property rejects a last-wins mutant with a replayable shrink path', () => {
  const empty = handles.empty()
  const mutationResult = fc.check(
    fc.property(handleKind, token, completionKind, completionKind, (kind, suffix, firstKind, lateKind) => {
      const handle = makeHandle(kind, `mutant-${suffix}`)
      const linked = link(empty, handle, `mutant-child-${suffix}`)
      const first = handles.apply(linked, { op: 'complete', handle, kind: firstKind })
      assert.equal(first.ok, true, first.ok ? '' : JSON.stringify(first.error))
      const productionLate = handles.apply(first.state, {
        op: 'complete',
        handle,
        kind: lateKind,
      })
      const lastWinsMutant = productionLate.ok ? productionLate : { ok: true, state: first.state }
      assertLateCompletionRejected(lastWinsMutant)
    }),
    { seed: 0x4d534c08, numRuns: 100 },
  )

  assert.equal(mutationResult.failed, true)
  assert.equal(mutationResult.seed, 0x4d534c08)
  assert.notEqual(mutationResult.counterexamplePath, '')
  assert.ok(mutationResult.counterexample.length > 0)
})
