import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const scores = ['PERFECT', 'REVISE', 'PERFECT', 'REVISE', 'PERFECT', 'PERFECT', 'REVISE', 'PERFECT']

const open = (state, snapshot = 'snapshot-1') =>
  relay.openIncumbency(state, 'road-1', 'inc-1', snapshot, 'authority-1')

test('WHAT[relay-assessment-003] assessment binds exact execution identity and rejects mismatched authority or iteration', () => {
  const opened = open(relay.empty())
  assert.deepEqual(
    relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-X', ...scores),
    { ok: false, error: 'AuthorityRevisionStale' },
  )
  assert.deepEqual(
    relay.assess(opened.state, 'road-1', 'inc-X', 'assessment-1', 'snapshot-1', 'authority-1', ...scores),
    { ok: false, error: 'IncumbencyNotActive' },
  )
})
