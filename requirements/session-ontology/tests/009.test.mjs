import assert from 'node:assert/strict'
import test from 'node:test'
import * as assoc from '../../../dist/Execution/Session/AssociationSurface.js'

const linked = (pairs, start = assoc.empty) =>
  pairs.reduce((state, pair) => {
    const result = assoc.link(pair, state)
    assert.equal(result.ok, true, result.message)
    return result.value
  }, start)

test('WHAT[session-ontology-009] COMPANION_001_every_work_session_may_have_a_companion', () => {
  const roles = ['orchestrator', 'manager', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'devops', 'distiller']
  const state = linked(roles.map((role) => ({ main: `ses_${role}`, blogger: `ses_${role}_y` })))
  for (const role of roles) {
    assert.equal(assoc.bloggerOf(`ses_${role}`, state), `ses_${role}_y`)
    assert.equal(assoc.isCompanion(`ses_${role}_y`, state), true)
  }
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
