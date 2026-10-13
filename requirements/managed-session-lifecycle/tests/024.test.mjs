import assert from 'node:assert/strict'
import test from 'node:test'
import * as handles from '../../../dist/Execution/Delegation/Handle/Surface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

test('WHAT[managed-session-lifecycle-024] active DevOps handle rejects a second physical binding in the production projection', () => {
  const link = { op: 'link', handle: handles.handleIdAgent('devops'), child: 'session-a', agent: 'devops', role: 'DevOps' }
  const first = handles.apply(handles.empty(), link)
  assert.equal(first.ok, true)
  const second = handles.apply(first.state, { ...link, child: 'session-b' })
  assert.equal(second.ok, false)
  assert.equal(handles.read(first.state, link.handle).child, 'session-a')
})

const surface = lifecycle.LifecycleSurface ?? lifecycle
const eventLine = (eventType, payload) => `${JSON.stringify({ event_type: eventType, payload })}\n`
const boundEvent = (devopsId, target) => eventLine('DevOpsBound', { devopsId, target, workId: 'road_alpha' })

test('WHAT[managed-session-lifecycle-024] road projection retains the supplied logical operator and model binding', () => {
  const opened = surface.encodeOpening('S4', 'road_alpha', '/repo', 1n)
  const bound = boundEvent('devops:road_alpha', 'provider/fixed-model:high')
  const retained = surface.decodeDocument(`${opened}${bound}`)
  assert.equal(retained.ok, true, retained.fault)
  const observed = JSON.parse(`${opened}${bound}`.trim().split('\n').at(-1)).payload
  assert.equal(observed.devopsId, 'devops:road_alpha')
  assert.equal(observed.target, 'provider/fixed-model:high')

  const folded = surface.readProbe(`${opened}${bound}`)
  assert.equal(retained.names.split(',').includes('DevOpsBound'), true)
  assert.equal(folded.position, 'ActiveRebound')
  assert.equal(folded.names.split(',').at(-1), 'DevOpsBound')
})

test.todo('WHAT[managed-session-lifecycle-024] actual DevOps crash recovery keeps one executable authority, fixed Persona/model, avoids command replay and drains real PTYs (GAP-133, GAP-129)')
