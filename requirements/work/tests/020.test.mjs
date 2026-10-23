import assert from 'node:assert/strict'
import test from 'node:test'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import { isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import {
  assistantText,
  cursors,
  ids,
  msgId,
  msgRole,
  msgText,
  syntheticPredecessor,
  userText,
} from './support/plan-messages.mjs'

// WHAT[work-020] fixes the closed input set a Plan runner receives:
// U (verbatim, original order), current-tenure interactions, and the draft
// (the only writable surface, reached through todowrite-for-plan only); no
// synthetic predecessor message is ever injected.

const assemble = (rawMessages, tenure, messageCursors) =>
  surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, messageCursors)

test('WHAT[work-020] U messages are preserved verbatim in original order', () => {
  const rawMessages = [
    userText('u-1', '设计缓存架构\n第二行'),
    assistantText('a-old', 'previous tenure work', 'u-1'),
    userText('u-2', '补充约束: no cache invalidation'),
    assistantText('a-new', 'current tenure work', 'u-2'),
  ]
  const tenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    isFreshHandover: false,
  }
  const result = assemble(
    rawMessages,
    tenure,
    cursors({ 'a-old': 15n, 'a-new': 30n }),
  )
  const userContents = result.messages
    .filter((m) => msgRole(m) === 'user')
    .map(msgText)
  assert.deepEqual(userContents, ['设计缓存架构\n第二行', '补充约束: no cache invalidation'])
})

test('WHAT[work-020] neither initial nor later tenures ever inject a synthetic-prev-* message', () => {
  const rawMessages = [
    userText('u-1', 'go'),
    assistantText('a-old', 'prior work', 'u-1'),
    assistantText('a-new', 'current work', 'u-1'),
  ]
  const firstTenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-1',
    stage: 'S1',
    openingCursor: 0n,
    isFreshHandover: true,
  }
  const laterTenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    isFreshHandover: true,
  }
  const firstResult = assemble([userText('u-1', 'go')], firstTenure, {})
  const laterResult = assemble(rawMessages, laterTenure, cursors({ 'a-old': 10n, 'a-new': 25n }))
  assert.ok(
    !firstResult.messages.some((m) => msgId(m).startsWith('synthetic-prev-') || m.info.synthetic === true),
    'first tenure must not carry any synthetic-prev-* message',
  )
  assert.ok(
    !laterResult.messages.some((m) => msgId(m).startsWith('synthetic-prev-') || m.info.synthetic === true),
    'later tenure must not carry any synthetic-prev-* message',
  )
  assert.deepEqual(ids(firstResult.messages), ['u-1'])
  assert.deepEqual(ids(laterResult.messages), ['u-1', 'a-new'])
})

test('WHAT[work-020] assembled messages come only from U and current-tenure interactions', () => {
  const rawMessages = [
    userText('u-1', 'go'),
    assistantText('a-1', 'current work', 'u-1'),
  ]
  const tenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    isFreshHandover: false,
  }
  const result = assemble(
    rawMessages,
    tenure,
    cursors({ 'a-1': 30n }),
  )
  const inputIds = new Set(rawMessages.map(msgId))
  for (const m of result.messages) {
    assert.ok(
      inputIds.has(msgId(m)) && !msgId(m).startsWith('synthetic-prev-'),
      `assembled message id ${msgId(m)} must originate from the physical input messages`,
    )
  }
  // The draft is reachable only through the todowrite-for-plan tool surface; the
  // assembled request never carries its content as an injected message.
  assert.equal(result.messages.length, rawMessages.length)
})

test('WHAT[work-020] the plan runner holds no write capability beyond the draft', () => {
  for (const perm of ['Write', 'Edit', 'Move', 'Remove', 'Exec', 'Pty']) {
    assert.equal(isAllowed('plan', perm), false, `plan must be denied ${perm}`)
  }
  // The draft itself is reached through the todowrite-for-plan programming surface.
  assert.equal(isAllowed('plan', 'JsPlan'), true)
})

test('WHAT[work-020] a negative openingCursor rejects assembly fail-closed', () => {
  const rawMessages = [userText('u-1', 'go')]
  const tenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: -1n,
    isFreshHandover: false,
  }
  assert.throws(
    () => assemble(rawMessages, tenure, {}),
    /nonnegative bigint|cannot be negative/i,
  )
})

test('WHAT[work-020] synthetic rows are stripped and never re-injected during assembly', () => {
  const rawMessages = [
    userText('u-1', 'go'),
    syntheticPredecessor('inc-old', 'w-001', 'old synthesized previous row'),
    assistantText('a-1', 'current work', 'u-1'),
  ]
  const tenure = {
    workId: 'w-001-inputs',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    isFreshHandover: false,
  }
  const result = assemble(
    rawMessages,
    tenure,
    cursors({ 'a-1': 30n }),
  )
  const syntheticRows = result.messages.filter((m) => msgId(m).startsWith('synthetic-prev-'))
  assert.equal(syntheticRows.length, 0, 'no synthetic-prev-* row is ever present')
  assert.ok(!result.messages.some((m) => msgId(m) === 'synthetic-prev-inc-old'))
  assert.deepEqual(ids(result.messages), ['u-1', 'a-1'])
})
