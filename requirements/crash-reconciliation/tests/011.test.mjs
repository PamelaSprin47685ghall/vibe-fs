import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

const work = session => ({ kind: 'work', session })
const child = (parent, session, handle) => ({ kind: 'child', parent, child: session, handle })

test('WHAT[crash-reconciliation-011] actual permit membership rejects loss and admits monotone growth', () => {
  const original = [work('root'), child('root', 'child', 'handle')]
  assert.deepEqual(recovery.missingMembers(original, [work('root')]), ['A:root>child:handle'])
  assert.deepEqual(recovery.missingMembers(original, [...original, child('child', 'grandchild', 'second')]), [])
  assert.deepEqual(recovery.missingMembers(original, [work('root'), child('root', 'child', 'different-handle')]), ['A:root>child:handle'])
})

test.todo('WHAT[crash-reconciliation-011] actual join and targeted await validate root journal sequence and membership again at every effect boundary (GAP-149)')
