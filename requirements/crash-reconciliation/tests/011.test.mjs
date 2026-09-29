import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

const work = session => ({ kind: 'work', session })
const child = (parent, session, handle) => ({ kind: 'child', parent, child: session, handle })

test('WHAT[crash-reconciliation-011] actual permit membership rejects loss and admits monotone growth', () => {
  const members = nodes => nodes.map(recovery.token)
  const original = members([work('root'), child('root', 'child', 'handle')])
  assert.deepEqual(recovery.missingMembers(original, members([work('root')])), ['A:root>child:handle'])
  assert.deepEqual(recovery.missingMembers(original, [...original, recovery.token(child('child', 'grandchild', 'second'))]), [])
  assert.deepEqual(recovery.missingMembers(original, members([work('root'), child('root', 'child', 'different-handle')])), ['A:root>child:handle'])
})

test.todo('WHAT[crash-reconciliation-011] actual join and targeted await validate root journal sequence and membership again at every effect boundary (GAP-149)')
