import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'

const linked = (pairs, start = assoc.empty) =>
  pairs.reduce((state, pair) => {
    const result = assoc.link(pair, state)
    assert.equal(result.ok, true, result.message)
    return result.value
  }, start)

test('WHAT[session-ontology-009] independent Work associations retain distinct Companions', () => {
  const state = linked([
    { main: 'work-a', blogger: 'companion-a' },
    { main: 'work-b', blogger: 'companion-b' },
  ])
  assert.deepEqual(assoc.ids(state), ['work-a', 'companion-a', 'work-b', 'companion-b'])
  assert.equal(assoc.bloggerOf('work-a', state), 'companion-a')
  assert.equal(assoc.bloggerOf('work-b', state), 'companion-b')
})

test('WHAT[session-ontology-009] COMPANION_003_unlinking_frees_work_session_for_fresh_companion', () => {
  const state = linked([{ main: 'ses_x', blogger: 'ses_y1' }])
  const unlinked = assoc.unlink('ses_x', state)
  assert.equal(assoc.bloggerOf('ses_x', unlinked), null)
  assert.equal(assoc.entry('ses_x', unlinked).kind, 'WorkSession')
  assert.equal(assoc.entry('ses_y1', unlinked), null)
  assert.equal(assoc.isCompanion('ses_y1', unlinked), false)
  assert.deepEqual(assoc.ids(unlinked), ['ses_x'])
  assert.equal(assoc.bloggerOf('ses_x', linked([{ main: 'ses_x', blogger: 'ses_y2' }], unlinked)), 'ses_y2')
})

test('WHAT[session-ontology-009] COMPANION_003_unlinking_is_total_and_idempotent', () => {
  assert.deepEqual(assoc.ids(assoc.unlink('ses_never_seen', assoc.empty)), [])
  const once = assoc.unlink('ses_x', linked([{ main: 'ses_x', blogger: 'ses_y' }]))
  const twice = assoc.unlink('ses_x', once)
  assert.deepEqual(assoc.ids(twice), assoc.ids(once))
  assert.deepEqual(assoc.entry('ses_x', twice), assoc.entry('ses_x', once))
})
