// tests/unit/verdict-feed.test.mjs — the classifier that decides what renews the unit suite's
// silence window (VERIFY-004).
//
// A unit test rather than a gate case because the classifier is a pure function of one event object,
// and because the gate cases that drive the real runner cannot distinguish "classified correctly"
// from "classified wrongly but the run happened to finish anyway".

import assert from 'node:assert/strict'
import test from 'node:test'

import { classifyVerdict } from './support/verdict-feed.mjs'

const event = (type, data = {}) => ({ type, data })

test('WHAT[VERIFICATION-SYSTEM-006] a verdict renews the silence window', () => {
  // Whole objects, not truthiness. mjs has no compile-time rename protection, so `blocking` read as
  // `undefined` would be falsy and a truthiness assertion would report the opposite of the truth
  // while passing — the exact failure this repository measured four times in package K9.
  assert.deepEqual(classifyVerdict(event('test:pass', { name: 'a', file: 'x.mjs' })), {
    blocking: true,
    reason: 'test:pass:a',
    lane: 'x.mjs',
  })

  assert.deepEqual(classifyVerdict(event('test:fail', { name: 'b', file: 'y.mjs' })), {
    blocking: true,
    reason: 'test:fail:b',
    lane: 'y.mjs',
  })

  assert.deepEqual(classifyVerdict(event('test:complete', { name: 'c', file: 'z.mjs' })), {
    blocking: true,
    reason: 'test:complete:c',
    lane: 'z.mjs',
  })
})

test('WHAT[VERIFICATION-SYSTEM-006] bytes moving is recorded and does not renew', () => {
  // 「不算进展：…任何『有字节在动』的证据」. `test:stdout` is the load-bearing member: a test that
  // hangs while printing is what turns a verdict feed back into a wall-clock timer, and
  // `hangs-with-handle-and-chatter.fixture.mjs` is built from exactly that shape.
  for (const type of ['test:stdout', 'test:stderr', 'test:diagnostic']) {
    assert.deepEqual(
      classifyVerdict(event(type, { file: 'x.mjs' })),
      { blocking: false, reason: type, lane: 'x.mjs' },
      `${type} must be recorded as background, never as progress`,
    )
  }
})

test('WHAT[VERIFICATION-SYSTEM-006] scheduling noise is not fed at all', () => {
  // `null` rather than a background default. `test:enqueue` and `test:dequeue` fire per test before
  // anything has happened, so defaulting unknown events to background would fill the watchdog dump's
  // "last background progress" line with scheduling noise and point the reader at the wrong lane.
  for (const type of ['test:enqueue', 'test:dequeue', 'test:start', 'test:plan', 'inner:drained']) {
    assert.equal(classifyVerdict(event(type, { file: 'x.mjs' })), null, `${type} must not be fed`)
  }

  assert.equal(classifyVerdict(undefined), null)
  assert.equal(classifyVerdict({}), null)
  assert.equal(classifyVerdict({ type: 42 }), null)
})

test('WHAT[VERIFICATION-SYSTEM-006] a verdict without a name or file still carries attribution', () => {
  // `Watchdog.advance` rejects an empty reason or lane by design — VERIFY-004 makes both part of the
  // timeout dump, and W6 records that a default of 'unattributed' would keep every canary green
  // while the dump lost the one thing the clause requires it to carry. So the classifier must never
  // produce an empty field, even from an event missing both.
  const classified = classifyVerdict(event('test:pass'))

  assert.deepEqual(classified, { blocking: true, reason: 'test:pass:(unnamed)', lane: '(no file)' })
  assert.ok(classified.reason.length > 0 && classified.lane.length > 0)
})
