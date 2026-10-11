import assert from 'node:assert/strict'
import test from 'node:test'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import { isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

// WHAT[planning-001] fixes the closed input set a Plan runner receives:
// U (verbatim, original order), LWR_prev (materialized, empty on the first
// tenure) and P (the only writable surface, reached through js-plan only).
// The assembly entry below is the same PlanningSurface.assembleTenureMessages
// the transform pipeline calls. The includeOpening=false render parameter of
// LWR_prev belongs to the work-record render side and is not observable
// through this surface; the production materialize wiring is tracked by
// GAP-230.

test('WHAT[planning-001] U messages are preserved verbatim in original order', () => {
  const rawMessages = [
    { id: 'u-1', role: 'user', content: '设计缓存架构\n第二行' },
    { id: 'a-old', role: 'assistant', content: 'previous tenure work', cursor: 3n },
    { id: 'u-2', role: 'user', content: '补充约束: no cache invalidation' },
    { id: 'a-new', role: 'assistant', content: 'current tenure work', cursor: 30n },
  ]
  const tenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: null,
    isFreshHandover: false,
  }
  const result = surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, () => 'unused')
  const userContents = result.messages
    .filter((m) => m.role === 'user' && !String(m.id).startsWith('lwr-prev-'))
    .map((m) => m.content)
  assert.deepEqual(userContents, ['设计缓存架构\n第二行', '补充约束: no cache invalidation'])
})

test('WHAT[planning-001] first tenure receives no LWR_prev materialization', () => {
  const rawMessages = [{ id: 'u-1', role: 'user', content: 'go' }]
  const tenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-1',
    stage: 'S1',
    openingCursor: 0n,
    previousRange: null,
    isFreshHandover: true,
  }
  let materializeCalls = 0
  const result = surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, () => {
    materializeCalls += 1
    return 'must not appear'
  })
  assert.equal(materializeCalls, 0, 'first tenure must not materialize a previous record')
  assert.ok(
    !result.messages.some((m) => String(m.id).startsWith('lwr-prev-')),
    'first tenure must not carry an LWR_prev message',
  )
})

test('WHAT[planning-001] assembled messages come only from U, LWR_prev and current-tenure interactions', () => {
  const rawMessages = [
    { id: 'u-1', role: 'user', content: 'go' },
    { id: 'a-1', role: 'assistant', content: 'current work', cursor: 30n },
  ]
  const tenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [7n, 19n],
    isFreshHandover: false,
  }
  const result = surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, () => 'prev record')
  const inputIds = new Set(rawMessages.map((m) => m.id))
  for (const m of result.messages) {
    assert.ok(
      inputIds.has(m.id) || String(m.id).startsWith('lwr-prev-'),
      `assembled message id ${m.id} must originate from the input messages or the LWR_prev record`,
    )
  }
  // The draft P is reachable only through the js-plan tool surface; the
  // assembled request never carries its content as an injected message.
  assert.equal(result.messages.length, rawMessages.length + 1)
})

test('WHAT[planning-001] the plan runner holds no write capability beyond P', () => {
  for (const perm of ['Write', 'Edit', 'Move', 'Remove', 'Exec', 'Pty']) {
    assert.equal(isAllowed('plan', perm), false, `plan must be denied ${perm}`)
  }
  // P itself is reached through the js-plan programming surface.
  assert.equal(isAllowed('plan', 'JsPlan'), true)
})
