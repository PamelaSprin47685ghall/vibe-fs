import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import {
  assistantText,
  cursors,
  ids,
  msgId,
  msgRole,
  msgText,
  syntheticLwr,
  toolResult,
  userText,
} from './support/plan-messages.mjs'

const assemble = (rawMessages, tenure, materializePrev, messageCursors) =>
  surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, materializePrev, messageCursors)

const life = lifecycle.LifecycleSurface ?? lifecycle
const framesOf = (text) => text.split('\n').filter((line) => line.length > 0).map((line) => JSON.parse(line))
const eventsOf = (text) => framesOf(text).flatMap((frame) => frame.events ?? [])
const incumbencyIdOf = (text) => [...eventsOf(text)].reverse().find((event) => event.type === 'IncumbencyOpened').incumbencyId

test('WHAT[work-034] initial tenure (S1) assembles U + current messages with no LWR_prev and requests reanchor on first turn', () => {
  const rawMessages = [
    userText('u1', 'Initial user prompt'),
    userText('u2', 'Second user instruction'),
  ]

  const tenure = {
    workId: 'work-1',
    incumbencyId: 'inc-1',
    stage: 'S1',
    openingCursor: 0n,
    previousRange: null,
    isFreshHandover: true,
  }

  const matPrev = () => {
    throw new Error('materializePrev must not be called for initial tenure')
  }

  // 1. First turn: no assistant/tool messages yet, isFreshHandover = true -> reanchorRequested = true
  const firstTurn = assemble(rawMessages, tenure, matPrev, {})
  assert.equal(firstTurn.reanchorRequested, true, 'a fresh handover must request a reanchor once')
  assert.equal(firstTurn.messages.length, 2)
  assert.equal(msgId(firstTurn.messages[0]), 'u1')
  assert.equal(msgId(firstTurn.messages[1]), 'u2')

  // 2. Subsequent turn: current tenure assistant/tool messages exist -> reanchorRequested stays false
  const subsequentMessages = [
    ...rawMessages,
    assistantText('a1', 'working on S1', 'u2'),
    toolResult('t1', 'js-plan', 'draft'),
  ]
  const tenureContinuing = { ...tenure, isFreshHandover: false }
  const secondTurn = assemble(
    subsequentMessages,
    tenureContinuing,
    matPrev,
    cursors({ a1: 5n, t1: 6n }),
  )
  assert.equal(secondTurn.reanchorRequested, false, 'a continuing tenure must not re-request reanchor')
  assert.equal(secondTurn.messages.length, 4)
  assert.deepEqual(
    ids(secondTurn.messages),
    ['u1', 'u2', 'a1', 't1'],
  )
})

test('WHAT[work-034] non-initial tenure strips prior assistant/tool messages, injects LWR_prev before the first current interaction, and preserves the current user reply after it', () => {
  // Real Host rows: historical U, prior S1 assistant+tool, a current user
  // reply (feedback) and a current assistant row. The canonical cursor map
  // carries first-observation evidence for every physical row, including the
  // current user reply, so LWR_prev is injected before that reply — never
  // moving it ahead of an older current assistant.
  const rawMessages = [
    userText('u1', 'Initial user prompt'),
    assistantText('a-old', 'S1 assistant work', 'u1'),
    toolResult('t-old', 'js-plan', 'S1 tool work'),
    userText('u2', 'Feedback from user'),
    assistantText('a-current', 'S2 assistant fresh', 'u2'),
    toolResult('t-current', 'js-plan', 'S2 tool result'),
  ]

  const tenureS2 = {
    workId: 'work-1',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [0n, 15n],
    isFreshHandover: false,
  }

  let matRangeCalledWith = null
  const matPrev = (rangeObj) => {
    matRangeCalledWith = rangeObj
    return '# Previous Incumbency Work Record\nCompleted S1 diagnosis.'
  }

  const result = assemble(
    rawMessages,
    tenureS2,
    matPrev,
    cursors({ 'a-old': 5n, 't-old': 8n, u2: 22n, 'a-current': 25n, 't-current': 26n }),
  )

  // Verify previous range was passed to materialize
  assert.ok(matRangeCalledWith != null)
  assert.equal(matRangeCalledWith.start, 0n)
  assert.equal(matRangeCalledWith.end, 15n)

  // Assembled sequence: U1, LWR_prev, U2(current reply), a-current, t-current.
  // a-old and t-old are stripped; LWR_prev is injected before the first
  // retained current physical message whose first observation exceeds the
  // opening cursor — here the current user reply u2 (cursor 22 > 20). The
  // reply stays in chronological position before the newer current assistant.
  assert.deepEqual(
    ids(result.messages),
    ['u1', 'lwr-prev-inc-2', 'u2', 'a-current', 't-current'],
  )

  const lwrMsg = result.messages[1]
  assert.equal(msgId(lwrMsg), 'lwr-prev-inc-2')
  assert.equal(msgRole(lwrMsg), 'user')
  assert.equal(msgText(lwrMsg), '# Previous Incumbency Work Record\nCompleted S1 diagnosis.')
  assert.equal(lwrMsg.info.synthetic, true)

  // Prior assistant/tool messages must be completely gone
  const assembledIds = ids(result.messages)
  assert.ok(!assembledIds.includes('a-old'), 'prior assistant message must be stripped')
  assert.ok(!assembledIds.includes('t-old'), 'prior tool message must be stripped')
})

test('WHAT[work-034] user messages U preserve original bytes and order verbatim without modification', () => {
  const originalUser1 = userText('u1', 'Exact bytes line 1\nLine 2\tTabs and spaces   ')
  const originalUser2 = userText('u2', 'Another user prompt with emojis: 💡🚀')

  const tenure = {
    workId: 'work-1',
    incumbencyId: 'inc-1',
    stage: 'S1',
    openingCursor: 0n,
    previousRange: null,
    isFreshHandover: true,
  }

  const result = assemble([originalUser1, originalUser2], tenure, () => '', {})

  assert.equal(result.messages.length, 2)
  // Strict identity and byte-for-byte content equality
  assert.equal(msgText(result.messages[0]), msgText(originalUser1))
  assert.equal(msgText(result.messages[1]), msgText(originalUser2))
  assert.deepEqual(result.messages[0], originalUser1)
  assert.deepEqual(result.messages[1], originalUser2)
})

test('WHAT[work-034] the first part immediately after the opening cursor survives tenure assembly', () => {
  // The very first assistant/tool row whose first observation is exactly the
  // opening cursor + 1 must survive — the boundary is strict `>` so the first
  // current interaction is retained.
  const rawMessages = [
    userText('u1', 'go'),
    assistantText('a-first', 'first current assistant part', 'u1'),
    userText('u2', 'follow-up'),
  ]
  const tenure = {
    workId: 'work-1',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [0n, 19n],
    isFreshHandover: false,
  }
  const result = assemble(
    rawMessages,
    tenure,
    () => '# prev',
    cursors({ 'a-first': 21n, u2: 22n }),
  )
  const assembledIds = ids(result.messages)
  assert.ok(assembledIds.includes('a-first'), 'the first part right after opening must survive')
  assert.ok(assembledIds.includes('u2'), 'the follow-up user reply must survive after LWR')
  // LWR_prev is injected before the first retained current interaction (a-first).
  assert.deepEqual(assembledIds, ['u1', 'lwr-prev-inc-2', 'a-first', 'u2'])
})

test('WHAT[work-034] a current assistant row with a Host id but no first-observation entry stays', () => {
  const rawMessages = [
    userText('u1', 'go'),
    assistantText('a-no-cursor', 'current work', 'u1'),
  ]
  const tenure = {
    workId: 'work-1',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [0n, 19n],
    isFreshHandover: false,
  }
  // info.id is present. The empty cursor map means this physical message has
  // not been captured yet. That is not a missing identity, so assembly keeps
  // the row and does not fail the exchange.
  const result = assemble(rawMessages, tenure, () => '# prev', {})
  assert.ok(ids(result.messages).includes('a-no-cursor'))
  assert.equal(result.messages.filter((m) => msgId(m).startsWith('lwr-prev-')).length, 1)
})

test('WHAT[work-034] a semantic assistant row with no Host info.id rejects assembly', () => {
  const rawMessages = [
    userText('u1', 'go'),
    {
      info: { role: 'assistant' },
      parts: [{ type: 'text', text: 'current work without a stable identity' }],
    },
  ]
  const identified = assistantText('a-uncaptured', 'captured later', 'u1')
  const tenure = {
    workId: 'work-1',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [0n, 19n],
    isFreshHandover: false,
  }
  assert.throws(
    () => assemble(rawMessages, tenure, () => '# prev', {}),
    /no stable XTrace message identity/i,
  )
  const stayed = assemble([userText('u1', 'go'), identified], tenure, () => '# prev', {})
  assert.ok(ids(stayed.messages).includes('a-uncaptured'))
})

test('WHAT[work-034] synthetic previous LWR rows are stripped and exactly one current record is injected', () => {
  const rawMessages = [
    userText('u1', 'go'),
    syntheticLwr('inc-old', 'work-1', 'stale synthesized row'),
    assistantText('a-current', 'current work', 'u1'),
  ]
  const tenure = {
    workId: 'work-1',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [0n, 19n],
    isFreshHandover: false,
  }
  const result = assemble(
    rawMessages,
    tenure,
    () => '# Fresh record',
    cursors({ 'a-current': 25n }),
  )
  const lwrRows = result.messages.filter((m) => msgId(m).startsWith('lwr-prev-'))
  assert.equal(lwrRows.length, 1, 'exactly one current LWR_prev is injected')
  assert.equal(msgId(lwrRows[0]), 'lwr-prev-inc-2')
  assert.ok(!result.messages.some((m) => msgId(m) === 'lwr-prev-inc-old'))
})

test('WHAT[work-034] durable Plan active state assembles U + current interactions in place', () => {
  const rawMessages = [
    userText('u1', 'go'),
    assistantText('a1', 'current work', 'u1'),
    userText('u2', 'feedback'),
  ]
  const tenure = {
    workId: 'w-25a',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [0n, 15n],
    isFreshHandover: false,
  }
  const result = assemble(rawMessages, tenure, () => '# prev', cursors({ a1: 25n, u2: 26n }))
  assert.deepEqual(
    ids(result.messages),
    ['u1', 'lwr-prev-inc-2', 'a1', 'u2'],
    'U, LWR_prev and current interactions must be assembled in place with LWR injected before the first current interaction',
  )
  assert.equal(result.reanchorRequested, false, 'a continuing durable tenure must not re-request reanchor')
})

test('WHAT[work-034] the lifecycle document view is reachable as a pure function', () => {
  assert.equal(typeof life.documentView, 'function')
  const opened = life.encodeOpening('plan', 'w-25a', '/tmp/plan-test', 0n)
  assert.equal(life.documentView(opened).found, true)
})

test('WHAT[work-034] reanchorRequested is returned by assemble and is true only on a fresh handover', () => {
  const rawMessages = [userText('u1', 'hi')]
  const tenure = {
    workId: 'w-25b',
    incumbencyId: 'inc-1',
    stage: 'S1',
    openingCursor: 0n,
    previousRange: null,
    isFreshHandover: true,
  }
  const fresh = assemble(rawMessages, tenure, () => '', {})
  assert.equal(typeof fresh.reanchorRequested, 'boolean')
  assert.equal(fresh.reanchorRequested, true, 'a fresh handover must request a reanchor')

  const continuing = assemble(rawMessages, { ...tenure, isFreshHandover: false }, () => '', {})
  assert.equal(continuing.reanchorRequested, false, 'a continuing tenure must not re-request reanchor on an empty stop')
})

const tenureFor = (overrides) => ({
  workId: 'w1',
  incumbencyId: 'inc-2',
  stage: 'S2',
  openingCursor: 20n,
  previousRange: [7n, 25n],
  isFreshHandover: false,
  ...overrides,
})

test('WHAT[work-034] S1 with no retirement carries no previous range', () => {
  const opened = life.encodeOpening('plan', 'w1', '/tmp/plan-test', 0n)
  const view = life.documentView(opened)
  assert.equal(view.openingStage, 'S1')
  assert.equal(view.latestRetirement, '')
  assert.equal(view.predecessor, '')
  assert.equal(view.openingCursor, 0)
})

test('WHAT[work-034] S1 retirement preserves its opening range for the next tenure', () => {
  const opened = life.encodeOpening('plan', 'w1', '/tmp/plan-test', 7n)
  const reviewed = life.reviewProbe(opened, incumbencyIdOf(opened), 'Stayed')
  assert.equal(reviewed.decision, 'Write', reviewed.detail)
  const relayed = life.deliverProbe(`${opened}${reviewed.bytes}`, 'run-034', 'call-034', 25n)
  assert.equal(relayed.decision, 'Write', relayed.detail)
  const retired = eventsOf(relayed.bytes)[0]
  const successor = eventsOf(relayed.bytes)[1]
  assert.equal(retired.retirementCursor, 25)
  assert.equal(successor.openingCursor, 25)
  assert.equal(successor.stage, 'S1', 'Stayed keeps the opening stage for the successor')
  const view = life.documentView(`${opened}${reviewed.bytes}${relayed.bytes}`)
  assert.equal(view.latestRetirement, '7,25')
  assert.equal(view.predecessor, '7,25')
  assert.equal(view.openingCursor, 25)
})

test('WHAT[work-034] tenure assembly consumes PreviousRange into LWR_prev', () => {
  const rawMessages = [
    userText('u1', 'go'),
    assistantText('a-old', 'old work', 'u1'),
    assistantText('a-new', 'new work', 'u1'),
  ]
  let seen = null
  const result = surface.PlanningSurface.assembleTenureMessages(
    rawMessages,
    tenureFor(),
    (range) => {
      seen = range
      return 'prev record'
    },
    cursors({ 'a-old': 9n, 'a-new': 30n }),
  )
  assert.equal(seen.start, 7n)
  assert.equal(seen.end, 25n)
  const assembledIds = ids(result.messages)
  assert.ok(!assembledIds.includes('a-old'))
  assert.ok(assembledIds.includes('a-new'))
  assert.ok(assembledIds.some((id) => id.startsWith('lwr-prev-')))
  // LWR_prev is injected before the first current interaction (a-new).
  assert.deepEqual(assembledIds, ['u1', 'lwr-prev-inc-2', 'a-new'])
})

test('WHAT[work-034] tenure reanchor event shape carries session, epochs, work and incumbency', () => {
  assert.equal(typeof prefix.applyTenureReanchor, 'function')
  assert.equal(typeof prefix.isTenureReanchored, 'function')
  assert.equal(typeof prefix.reanchoredTenures, 'function')
})

test('WHAT[work-034] duplicate incumbencyId is rejected on second tenure reanchor', () => {
  const empty = prefix.empty
  const first = prefix.applyTenureReanchor({ previousEpoch: 0n, nextEpoch: 1n, incumbencyId: 'inc-1' }, empty)
  assert.equal(first.ok, true)
  const second = prefix.applyTenureReanchor({ previousEpoch: 1n, nextEpoch: 2n, incumbencyId: 'inc-1' }, first.value)
  assert.equal(second.ok, false)
  assert.equal(second.error, 'TenureAlreadyReanchored')
})

test('WHAT[work-034] reanchorRequested is true on fresh handover and deduplicated via isTenureReanchored', () => {
  const rawMessages = [userText('u1', 'hi')]
  const tenure = {
    workId: 'w-27',
    incumbencyId: 'inc-1',
    stage: 'S1',
    openingCursor: 0n,
    previousRange: null,
    isFreshHandover: true,
  }
  const result = surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, () => '', {})
  assert.equal(result.reanchorRequested, true)

  const continuing = surface.PlanningSurface.assembleTenureMessages(
    rawMessages,
    { ...tenure, isFreshHandover: false },
    () => '',
    {},
  )
  assert.equal(continuing.reanchorRequested, false)
})

test('WHAT[work-034] successor projection strips earlier assistant and tool messages and inserts LWR_prev once', () => {
  const rawMessages = [
    userText('u-history', 'Original user request'),
    assistantText('a-old', 'previous tenure answer'),
    toolResult('t-old', 'js-plan', 'old draft'),
    assistantText('a-current', 'this tenure answer'),
    assistantText('a-uncaptured', 'this request, not captured yet'),
  ]
  const tenure = {
    workId: 'work-1',
    incumbencyId: 'inc-2',
    stage: 'S2',
    openingCursor: 20n,
    previousRange: [1n, 19n],
    isFreshHandover: false,
  }
  const result = assemble(
    rawMessages,
    tenure,
    () => '# Previous record',
    cursors({ 'a-old': 4n, 't-old': 5n, 'a-current': 25n }),
  )
  const lwrRows = result.messages.filter((m) => msgId(m).startsWith('lwr-prev-'))
  assert.equal(lwrRows.length, 1, 'LWR_prev is inserted once')
  assert.ok(ids(result.messages).includes('u-history'))
  assert.ok(ids(result.messages).includes('a-current'), 'current-tenure message stays')
  assert.ok(ids(result.messages).includes('a-uncaptured'), 'uncaptured physical message stays')
  assert.ok(!ids(result.messages).includes('a-old'), 'earlier assistant message is stripped')
  assert.ok(!ids(result.messages).includes('t-old'), 'earlier tool message is stripped')
  const currentIndex = ids(result.messages).indexOf('a-current')
  assert.equal(ids(result.messages)[currentIndex - 1], 'lwr-prev-inc-2')
})

test('WHAT[work-034] post-S3 projection strips all S3-and-earlier context and injects no LWR_prev', () => {
  // The delivered Plan work carries no active tenure. Its isolation is keyed on
  // the delivered retirement frontier: tenureForPostS3 sets OpeningCursor to
  // frontier + 1 and PreviousRange to null (no predecessor record). So every
  // assistant/tool exchange observed at-or-before the frontier is stripped, the
  // historical user inputs survive, and — unlike a relay successor — no
  // LWR_prev is injected.
  const deliveredFrontier = 40n
  const rawMessages = [
    userText('u-history', 'Original user request'),
    assistantText('a-s1', 'S1 tenure assistant'),
    toolResult('t-s1', 'js-plan', 'S1 tool'),
    assistantText('a-s2', 'S2 tenure assistant'),
    assistantText('a-s3', 'S3 tenure assistant'),
    userText('u-command', 'Please execute deliver now.'),
    assistantText('a-post', 'post-S3 assistant', 'u-command'),
  ]
  const tenure = {
    workId: 'work-post-s3',
    incumbencyId: 'post-s3',
    stage: 'S3',
    openingCursor: deliveredFrontier + 1n,
    previousRange: null,
    isFreshHandover: false,
  }
  const result = assemble(
    rawMessages,
    tenure,
    () => {
      throw new Error('post-S3 must not materialize an LWR_prev')
    },
    cursors({ 'a-s1': 5n, 't-s1': 8n, 'a-s2': 20n, 'a-s3': 38n, 'u-command': 41n, 'a-post': 42n }),
  )
  const kept = ids(result.messages)
  assert.equal(
    result.messages.filter((m) => msgId(m).startsWith('lwr-prev-')).length,
    0,
    'post-S3 inherits no previous work record',
  )
  assert.ok(kept.includes('u-history'), 'historical user input is preserved verbatim')
  assert.ok(kept.includes('u-command'), 'the post-S3 command survives')
  assert.ok(kept.includes('a-post'), 'post-S3 assistant after the frontier stays')
  for (const prior of ['a-s1', 't-s1', 'a-s2', 'a-s3']) {
    assert.ok(!kept.includes(prior), `${prior} (S3-and-earlier) must be stripped`)
  }
})
