import assert from 'node:assert/strict'
import test from 'node:test'
import * as projection from '../../../dist/Mission/Relay/ProjectionSurface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const cutMessages = [
  { id: 'u1', run: '', role: 'user', text: 'root user request' },
  { id: 'a1', run: 'old-run', role: 'assistant', text: 'old iteration audit' },
  {
    id: 't1',
    run: 'old-run',
    role: 'assistant',
    parts: [{ callID: 'suicide-call', tool: 'suicide' }],
    text: 'suicide call',
  },
  { id: 'r1', run: 'old-run', role: 'tool', text: 'suicide result' },
  { id: 'a-late', run: 'old-run', role: 'assistant', text: 'late old part' },
  { id: 'wake-1', run: '', role: 'user', text: 'internal loop wake' },
  { id: 'a2', run: 'new-run', role: 'assistant', text: 'next iteration audit' },
]

const cutResult = () => projection.projectMessages(cutMessages)

const ids = (result) => result.provider.map((message) => message.id ?? message.info?.id)

test('WHAT[relay-retirement-008] RETIRE_008_physical_interruption_boundary_retains_full_physical_history', () => {
  const providerIds = ids(cutResult())
  assert.equal(providerIds.includes('a-late'), true, 'late retired part must be retained')
  assert.equal(providerIds.includes('wake-1'), true, 'internal loop wake must be retained')
  assert.equal(providerIds.includes('t1'), true, 'suicide tool call must be retained')
  assert.equal(providerIds.includes('r1'), true, 'suicide tool result must be retained')
  assert.equal(providerIds.includes('u1'), true, 'root authority must be preserved')
  assert.equal(providerIds.includes('a2'), true, 'new iteration audit must be preserved')
})

test('WHAT[relay-retirement-008] suicide interrupts the old Host attempt before dispatching the next manager', async () => {
  await withExecutablePlugin(async (hooks, _directory, _children, runtime) => {
    const sessionID = 'ses-retired-manager-interruption'
    const rootID = `root-${sessionID}`
    const root = {
      id: rootID,
      role: 'user',
      parts: [{ type: 'text', text: 'Complete the requested work.' }],
    }
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    runtime.pushHostMessage(sessionID, root)
    await hooks['chat.message'](
      { sessionID, messageID: rootID, agent: 'manager' },
      { message: root, parts: root.parts },
    )
    const user = { info: { id: rootID, role: 'user', sessionID }, parts: root.parts }
    await hooks['experimental.chat.messages.transform']({ sessionID }, { messages: [user] })

    const scores = Object.fromEntries([
      'language_algorithms', 'simplicity', 'structure', 'granularity',
      'tests_evidence', 'logic_reliability_boundaries', 'caller_ergonomics', 'completeness',
    ].map((name) => [name, name === 'structure' ? 'REVISE' : 'PERFECT']))
    const review = {
      id: 'run-review', role: 'assistant', parentID: rootID,
      parts: [
        { type: 'text', text: 'The structure needs revision.' },
        { type: 'tool', tool: 'review', callID: 'call-review', state: { status: 'pending', input: scores } },
      ],
    }
    runtime.pushHostMessage(sessionID, review)
    const context = (callID, messageID) => ({ sessionID, agent: 'manager', callID, messageID })
    const reviewResult = await hooks.tool.review.execute(scores, context('call-review', review.id))
    assert.match(reviewResult, /recorded = true/)

    const retiredRun = {
      id: 'run-suicide', role: 'assistant', parentID: rootID,
      parts: [{ type: 'tool', tool: 'suicide', callID: 'call-suicide', state: { status: 'pending', input: {} } }],
    }
    runtime.pushHostMessage(sessionID, retiredRun)
    const result = await hooks.tool.suicide.execute({}, context('call-suicide', retiredRun.id))
    assert.match(result, /finished = true/)

    const messages = [
      user,
      { info: { id: review.id, role: 'assistant', sessionID }, parts: review.parts },
      { info: { id: retiredRun.id, role: 'assistant', sessionID }, parts: retiredRun.parts },
    ]
    const priorPrompts = runtime.prompts.length
    const oldRequest = { messages: [...messages] }
    await hooks['experimental.chat.messages.transform']({ sessionID }, oldRequest)
    assert.deepEqual(oldRequest.messages, [], 'the retired manager must not receive the suicide result from a new provider call')
    assert.deepEqual(runtime.abortedIds, [sessionID], 'Host interruption must settle before the next manager prompt')
    assert.equal(runtime.prompts.length, priorPrompts + 1, 'Continue retirement must dispatch exactly one next-iteration prompt')

    const nextUser = runtime.messages.filter((message) => message.role === 'user' && message.id !== rootID).at(-1)
    assert.ok(nextUser, 'the next manager must have an accepted physical prompt')
    const gate = { info: { id: nextUser.id, role: 'user', sessionID }, parts: nextUser.parts }
    const nextRequest = {
      messages: [...messages, gate],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID }, nextRequest)
    assert.ok(nextRequest.messages.some((message) => message.info?.id === rootID), 'root authority stays in the retained history')
    assert.ok(nextRequest.messages.some((message) => message.info?.id === review.id), 'the next iteration request retains the full physical history')
    assert.ok(nextRequest.messages.some((message) => message.info?.id === retiredRun.id), 'the suicide call stays in the retained history')
    assert.ok(nextRequest.messages.some((message) => message.info?.id === nextUser.id), 'the gate message is appended after the retained history')
    assert.deepEqual(runtime.abortedIds, [sessionID], 'the new manager must not inherit the old interrupt')

    const human = {
      id: 'human-after-retirement', role: 'user',
      parts: [{ type: 'text', text: 'Please also check the final change.' }],
    }
    await hooks['chat.message'](
      { sessionID, messageID: human.id, agent: 'manager' },
      { message: human, parts: human.parts },
    )
    const humanRequest = {
      messages: [...messages, gate, { info: { id: human.id, role: 'user', sessionID }, parts: human.parts }],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID }, humanRequest)
    assert.ok(
      humanRequest.messages.some((message) => message.info?.id === human.id),
      `human request lost: ${JSON.stringify({ ids: humanRequest.messages.map((message) => message.info?.id), aborts: runtime.abortedIds })}`,
    )
    assert.deepEqual(runtime.abortedIds, [sessionID], 'an accepted new human message must not be mistaken for the retired attempt')
  })
})
