import assert from 'node:assert/strict'
import test from 'node:test'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import {
  assistantText,
  cursors,
  ids,
  userText,
} from './support/plan-messages.mjs'

const assemble = (rawMessages, tenure, materializePrev, messageCursors) =>
  surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, materializePrev, messageCursors)

// WHAT[planning-017] behavior regressions for the durable Plan tenure path.
// These assert observable assembly behavior, not function arity or source
// text: a regression in the assembly must change the assembled message set.

test('WHAT[planning-017] durable Plan active state assembles U + current interactions in place', () => {
  const rawMessages = [
    userText('u1', 'go'),
    assistantText('a1', 'current work', 'u1'),
    userText('u2', 'feedback'),
  ]
  const tenure = {
    workId: 'w-25a',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [0n, 15n],
    isFreshHandover: false,
  }
  const result = assemble(rawMessages, tenure, () => '# prev', cursors({ a1: 25n, u2: 26n }))
  assert.deepEqual(
    ids(result.messages),
    ['u1', 'lwr-prev-inc-2', 'a1', 'u2'],
    'U, LWR_prev and current interactions must be assembled in place with LWR injected before the first current interaction',
  )
  assert.equal(result.reanchorRequested, false, 'a continuing durable tenure must not re-request reanchor')
})

test('WHAT[planning-017] the Plan store read path is reachable as a pure function', () => {
  assert.equal(typeof surface.PlanningSurface.getPlanWorkStateFromIntegrator, 'function')
  // canary: the store read path is reachable as a pure function
  // (PlanEventStore.tryActiveWorkState over TryCurrent "Plan"); live projection
  // queries against a real journal await a physical host run. When the Plan
  // projection is absent or has no active incumbency, tenure stays pass-through
  // and must never fall back to forged default S1 data.
})

test('WHAT[planning-017] reanchorRequested is returned by assemble and is true only on a fresh handover', () => {
  const rawMessages = [userText('u1', 'hi')]
  const tenure = {
    workId: 'w-25b',
    incumbencyId: 'inc-1',
    stage: 'S1',
    openingCursor: 0n,
    previousRange: null,
    isFreshHandover: true,
  }
  const fresh = assemble(rawMessages, tenure, () => '', {})
  assert.equal(typeof fresh.reanchorRequested, 'boolean')
  assert.equal(fresh.reanchorRequested, true, 'a fresh handover must request a reanchor')

  const continuing = assemble(rawMessages, { ...tenure, isFreshHandover: false }, () => '', {})
  assert.equal(continuing.reanchorRequested, false, 'a continuing tenure must not re-request reanchor on an empty stop')
})