import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Context/Companion/Blogger/FrameSurface.js");

const entryFrame = (n) => blog.frame({
  kind: 'Entry',
  digest: `sha-entry-${n}`,
  ref: `blob-entry-${n}`,
  coveredFrom: n - 1,
  coveredThrough: n,
})
const squashFrame = (n) => blog.frame({
  kind: 'Squash',
  digest: `sha-entry-${n}`,
  ref: `blob-squash-${n}`,
  coveredFrom: 0,
  coveredThrough: 2,
})
const commitEntry = (state, { epoch = 0, from, to, cutoffFrom, cutoffTo, digest = `digest-${cutoffTo}`, n = 1 }) =>
  blog.applyEntry(
    {
      epoch,
      previous: from,
      next: to,
      previousCutoff: cutoffFrom,
      nextCutoff: cutoffTo,
      digest,
      frame: entryFrame(n),
    },
    state,
  )
function threeEntries() {
  let state = blog.empty

  for (let i = 1; i <= 3; i += 1) {
    const result = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i })
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  }

  return state
}

test('WHAT[CONTEXT-COMPRESSION-014] COMPANION_006_squash_rewrites_first_half_of_frames_permanently', () => {
  let state = blog.empty
  for (let i = 1; i <= 4; i += 1) {
    const result = commitEntry(state, { from: i - 1, to: i, cutoffFrom: i - 1, cutoffTo: i, n: i })
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    state = result.value
  }

  const squashed = blog.applySquash({ previousEpoch: 0, nextEpoch: 1, count: 2, frame: squashFrame(1) }, state).value
  assert.deepEqual(blog.frameKinds(squashed), ['Squash', 'Entry', 'Entry'])

  // The rewritten first half persists: a later entry does not restore the old frames.
  const next = commitEntry(squashed, { epoch: 1, from: 4, to: 5, cutoffFrom: 4, cutoffTo: 5, n: 5 }).value
  assert.deepEqual(blog.frameKinds(next), ['Squash', 'Entry', 'Entry', 'Entry'])
  assert.equal(blog.coverage(next).cutoff, 5)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toml = await import("../../../dist/Context/Companion/Blogger/TomlSurface.js");
const owner = await import("../../../dist/Context/Companion/ProjectionSurface.js");

const ident = owner
const prompt = owner
const proj = owner
const spy = (input) => `«${input}»`
const frames = (count) =>
  Array.from({ length: count }, (_, n) => ({ digest: `sha-f${n}`, body: `frame body ${n}` }))
const dataItems = [{ role: 'user', kind: 'text', text: 'work', truncated: false }]
const dataToml = '[[new_work_to_record]]\nuser = "work"\n'
const combinedDelta = prompt.newWork(dataItems)
const isHistoricFrame = (text) => text.startsWith('[[do_not_exec]]') && text.includes('historic_frame')
const isCombinedNormalDelta = (text) =>
  text.startsWith('# Write the dense work-log continuation now') && text.includes('[[new_work_to_record]]')
const isPreviousTip = (text) => text.includes('previous_enforcer_tip')

test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_projects_only_oldest_historic_frames_then_instruction', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(2),
    frames: frames(4),
    delta: undefined,
  })

  assert.equal(plan.texts.filter(isHistoricFrame).length, 2)
  assert.equal(plan.texts.some((t) => t.includes('[[new_work_to_record]]')), false)
  assert.deepEqual(plan.texts, [
    toml.renderHistoricFrame('frame body 0'),
    toml.renderHistoricFrame('frame body 1'),
    prompt.squashInstruction,
  ])
  assert.deepEqual(plan.physicalFlags, [false, false, false])
  assert.deepEqual(plan.roles, ['assistant', 'assistant', 'user'])
  assert.equal(plan.system, undefined)
})
test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_pairs_tips_with_covered_frames_then_instruction', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(2),
    frames: frames(4),
    delta: undefined,
    previousTips: [
      { field: 'primitive-obsession', cycleId: 'c1' },
      { field: 'ignored-tdd', cycleId: 'c2' },
    ],
  })

  assert.deepEqual(
    plan.texts.map((t) => {
      if (isPreviousTip(t)) return 'tip'
      if (isHistoricFrame(t)) return 'frame'
      if (t === prompt.squashInstruction) return 'instruction'
      return 'other'
    }),
    ['tip', 'frame', 'tip', 'frame', 'instruction'],
  )
  // Only oldest k=2 frames; later bodies must not appear.
  assert.equal(
    plan.texts.some((t) => t.includes('frame body 2') || t.includes('frame body 3')),
    false,
  )
})
test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_a_squash_ignores_a_delta_even_if_one_is_supplied', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(1),
    frames: frames(3),
    delta: { messageId: 'msg_should_not_appear', items: dataItems },
  })

  assert.deepEqual(plan.texts, [toml.renderHistoricFrame('frame body 0'), prompt.squashInstruction])
  assert.equal(plan.physicalFlags.includes(true), false)
  assert.equal(
    plan.messages.some((m) => m.text.includes('UNCONSUMED')),
    false,
    'the delta must not reach a squash request',
  )
})
test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_a_squash_never_shows_the_later_frames', () => {
  const plan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 0,
    kind: proj.squash(2),
    frames: frames(5),
    delta: undefined,
  })

  for (const n of [2, 3, 4]) {
    assert.equal(
      plan.texts.some((t) => t.includes(`frame body ${n}`)),
      false,
      `frame ${n} is outside the squash range and must not be projected`,
    )
  }
})
test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_and_normal_requests_use_different_last_message_ids', () => {
  const shared = { blogger: 'ses_y', epoch: 0, frames: frames(1) }

  const normal = proj.build(spy, { ...shared, kind: proj.normal, delta: { messageId: 'm', items: dataItems } })
  const squash = proj.build(spy, { ...shared, kind: proj.squash(1), delta: undefined })

  assert.equal(normal.messages.at(-1).id, 'm')
  assert.equal(squash.messages.at(-1).id, '«ses_y|0|squash|instruction»')
  assert.notEqual(normal.messages.at(-1).id, squash.messages.at(-1).id)
})
test('WHAT[CONTEXT-COMPRESSION-014] CTX_012_squash_plan_has_zero_physical_messages_and_not_first_turn', () => {
  const squashPlan = proj.build(spy, {
    blogger: 'ses_y',
    epoch: 1,
    kind: proj.squash(2),
    frames: frames(3),
    delta: { messageId: 'msg_should_not_appear', items: dataItems },
    previousTips: [{ field: 'ignored-tdd', cycleId: 'c1' }],
  })

  assert.equal(squashPlan.isFirstTurnShape, false)
  assert.equal(squashPlan.physicalFlags.some((f) => f === true), false)
  assert.equal(squashPlan.messages.some((m) => m.physical), false)
  assert.equal(squashPlan.messages.at(-1).role, 'user')
  assert.equal(squashPlan.messages.at(-1).text, prompt.squashInstruction)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const algebra = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const companionProj = await import("../../../dist/Context/Companion/ProjectionSurface.js");

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
}
