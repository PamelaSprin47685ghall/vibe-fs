// Context owns Companion materialization; provider projection only plans and
// renders generic rows.

import assert from 'node:assert/strict'
import test from 'node:test'
import * as algebra from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as companionProj from '../../../dist/Context/Companion/ProjectionSurface.js'

const emptyProjection = {
  providerId: null,
  modelId: null,
  variant: null,
  tools: [],
  system: [],
  messages: [],
}

const snapshot = () => algebra.projectionSnapshot(emptyProjection)

const planNames = (intents) => {
  const result = algebra.plan(intents)
  assert.equal(result.ok, true, `expected Ok plan, got ${JSON.stringify(result)}`)
  return result.intents
}

const assertOwnerRowsMatchBuilder = (intent, builderPlan) => {
  assert.deepEqual(
    intent.rows.map((row) => [row.message.role, row.message.parts[0]?.text]),
    builderPlan.messages.map((message) => [message.role, message.text]),
  )
  assert.deepEqual(
    intent.rows.map((row) => row.hostMessageId),
    builderPlan.messages.map((message) => message.id),
  )
  assert.deepEqual(
    intent.rows.map((row) => row.hostIsPhysical),
    builderPlan.physicalFlags,
  )
}

test('WHAT[CONTEXT-COMPRESSION-012] PROJ_008_Companion_owner_normal_rows_render_through_generic_projection', () => {
  const spy = (input) => `«${input}»`
  const frames = [
    { digest: 'sha-f0', body: 'frame body 0' },
    { digest: 'sha-f1', body: 'frame body 1' },
  ]
  const previousTips = [{ field: 'progress', cycleId: 'cycle-1' }]
  const delta = { messageId: 'msg_delta', toml: '[[new_work_to_record]]\nuser = "work"' }
  const input = { blogger: 'ses_y', epoch: 0, kind: 'normal', frames, delta, previousTips }

  const intent = companionProj.projectionIntent(spy, input)
  const builderPlan = companionProj.build(spy, input)

  assert.equal(intent.kind, 'ReplaceMessageBase')
  assert.deepEqual(planNames([intent]), ['ReplaceMessageBase'])
  assertOwnerRowsMatchBuilder(intent, builderPlan)

  const rendered = algebra.renderMessages(snapshot(), [], [intent])
  const renderedWithHost = algebra.renderMessagesWithHostIds(snapshot(), [], [intent])
  assert.deepEqual(
    rendered.map((message) => [message.role, message.parts[0]?.text]),
    builderPlan.messages.map((message) => [message.role, message.text]),
  )
  assert.deepEqual(
    renderedWithHost.hostMessageIds,
    builderPlan.messages.map((message) => message.id),
  )
  assert.deepEqual(renderedWithHost.hostIsPhysical, builderPlan.physicalFlags)
})

test('WHAT[CONTEXT-COMPRESSION-014] PROJ_008_Companion_owner_squash_rows_render_through_generic_projection', () => {
  const spy = (input) => `«${input}»`
  const frames = [
    { digest: 'sha-f0', body: 'frame body 0' },
    { digest: 'sha-f1', body: 'frame body 1' },
    { digest: 'sha-f2', body: 'frame body 2' },
  ]
  const input = { blogger: 'ses_y', epoch: 1, kind: companionProj.squash(2), frames }

  const intent = companionProj.projectionIntent(spy, input)
  const builderPlan = companionProj.build(spy, input)

  assert.equal(intent.kind, 'ReplaceMessageBase')
  assertOwnerRowsMatchBuilder(intent, builderPlan)

  const rendered = algebra.renderMessages(snapshot(), [], [intent])
  assert.deepEqual(
    rendered.map((message) => [message.role, message.parts[0]?.text]),
    builderPlan.messages.map((message) => [message.role, message.text]),
  )
  assert.equal(rendered.at(-1)?.parts[0]?.text, companionProj.squashInstruction)
})

test('WHAT[CONTEXT-COMPRESSION-012] PROJ_008_frame_only_owner_inserts_before_message_index_one_and_empty_is_no_op', () => {
  const spy = (input) => `«${input}»`
  const input = {
    blogger: 'ses_y',
    epoch: 2,
    kind: 'normal',
    frames: [{ digest: 'sha-f0', body: 'frame body 0' }],
  }

  const intent = companionProj.projectionIntent(spy, input)
  const builderPlan = companionProj.build(spy, input)

  assert.equal(intent.kind, 'InsertMessageRows')
  assert.deepEqual(intent.anchor, { kind: 'BeforeMessageIndex', index: 1 })
  assertOwnerRowsMatchBuilder(intent, builderPlan)
  assert.equal(companionProj.projectionIntent(spy, { ...input, frames: [] }), null)
})
