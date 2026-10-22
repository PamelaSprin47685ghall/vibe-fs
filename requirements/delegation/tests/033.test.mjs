import assert from 'node:assert/strict'
import test from 'node:test'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { parse } from 'smol-toml'
import * as delivery from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { reportMaterial } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { factPayloads } from '../../verification-system/tests/e2e/support/journal-observer.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import {
  deliverChildTurn,
  forkCall,
  joinCall,
  joinRecordsOf,
  observeTerminals,
  physicalUserMessageOf,
  workLifecycleOf,
  withRealFork,
} from './support/real-fork.mjs'

const material = 'DELIVERED MATERIAL'
const prose = 'DELIVERED PROSE'
const identityValue = value => Array.isArray(value) ? value[1] : value
const deliveryOf = (directory, session, run, call) => {
  const witnesses = factPayloads(directory, 'TaskDeliveryCommitted').filter(witness =>
    identityValue(witness.Fence.SessionId) === session &&
    identityValue(witness.Fence.ProviderRun) === run &&
    identityValue(witness.Fence.ToolCallId) === call,
  )
  assert.equal(witnesses.length, 1, 'the exact delivering call commits one durable witness')
  return witnesses[0]
}

const userMessage = (session, physical, text) => ({
  info: {
    id: physical, role: 'user', sessionID: session, agent: 'engineer',
    model: { providerID: 'provider', modelID: 'engineer-model' }, time: { created: 1 },
  },
  parts: [{ type: 'text', text }],
})

const assistantMessage = (session, physical, run, parts, created = 2) => ({
  info: {
    id: run, role: 'assistant', sessionID: session, parentID: physical, agent: 'engineer',
    providerID: 'provider', modelID: 'engineer-model', time: { created },
  },
  parts,
})

const toolPart = (call, program) => ({
  id: `part-${call}`, type: 'tool', tool: 'todowrite-for-engineer', callID: call,
  state: { status: 'pending', input: { program } },
})

test('WHAT[delegation-033] a committed child delivery survives the cut boundary and completes only after the actual Host stop', async () => {
  const owner = 'ses-owner-033'
  await withRealFork(owner, async ({ hooks, directory, createdIds, runtime }) => {
    const receipt = await forkCall(hooks, owner, 'Ada', 'Report the exact verified material')
    const agentId = /agentId = "([^"]+)"/.exec(receipt)?.[1]
    assert.ok(agentId)
    const child = createdIds[0]
    const physical = physicalUserMessageOf(runtime, child)
    const run = 'run-033'
    const terminals = await observeTerminals(runtime)
    try {
      const { result, assistant } = await deliverChildTurn(hooks, runtime, child, run, prose, material)
      assert.match(String(result), /# ok/)
      const witness = deliveryOf(directory, child, run, `call-${run}`)
      assert.equal(witness.FormalText, prose)
      assert.equal(delivery.deliveredValue(child), witness.View)
      assert.deepEqual(delivery.deliveredReturned(child), [])
      assert.deepEqual(terminals.seen, [])
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, physical.id), 'Active')

      const snapshot = await runtime.client.session.messages({ path: { id: child } })
      const request = {
        messages: snapshot.data.map(message => {
          if (message.info) return message
          const { parts, ...info } = message
          return { info, parts }
        }),
      }
      await hooks['experimental.chat.messages.transform']({ sessionID: child }, request)
      assert.ok(request.messages.length > 0)
      assert.equal(runtime.abortedIds.includes(child), false)
      assert.deepEqual(terminals.seen, [], 'a delivery cut cannot invent a physical terminal')
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, physical.id), 'Active')

      assistant.parts.at(-1).state = { ...assistant.parts.at(-1).state, status: 'completed', output: String(result) }
      assistant.info.time.completed = 3
      assistant.info.finish = 'tool-calls'
      await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
      assert.deepEqual(terminals.seen, [], 'a completed tool-call step leaves the child work active')
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, physical.id), 'Active')

      const stopped = assistantMessage(child, physical.id, `${run}-stop`, [{ type: 'text', text: 'LATE STOP PROSE' }], 4)
      runtime.pushHostMessage(child, stopped)
      await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
      stopped.info.time.completed = 5
      stopped.info.finish = 'stop'
      await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await terminals.awaitRun(child, stopped.info.id)
      const completed = terminals.seen.filter(entry => entry.sessionId === child && entry.outcome.kind === 'Completed')
      assert.equal(completed.length, 1)
      assert.equal(completed[0].outcome.providerRun, stopped.info.id)
      assert.equal(completed[0].outcome.text, witness.View)
      assert.doesNotMatch(completed[0].outcome.text, /LATE STOP PROSE/)

      const joined = await joinCall(hooks, owner)
      assert.match(joined, /DELIVERED MATERIAL/)
      assert.doesNotMatch(joined, /LATE STOP PROSE/)
      assert.equal((joinRecordsOf(joined).match(/\[\[data\.records\]\]/g) ?? []).length, 1)
      const returned = delivery.deliveredReturned(child)
      assert.ok(returned.some(key => key.includes(prose)))
      assert.ok(returned.every(key => !key.includes('LATE STOP PROSE')))
      assert.equal(delivery.deliveredFenceOf(child), null)
      await hooks.event({ event: { type: 'message.updated', properties: { info: stopped.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await new Promise(resolve => setImmediate(resolve))
      assert.equal(terminals.seen.filter(entry => entry.sessionId === child && entry.outcome.kind === 'Completed').length, 1)
      assert.deepEqual(delivery.deliveredReturned(child), returned)
    } finally {
      terminals.dispose()
    }
  })
})

for (const defect of ['no prose', 'prose from an older physical input', 'an unmatched tool call']) {
  test(`WHAT[delegation-033] a durable delivery with ${defect} is refused while ordinary reads remain available`, async () => {
    await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
      const session = `ses-033-refused-${defect}`
      const physical = `msg-${session}`
      const run = `run-${session}`
      const readCall = `read-${session}`
      const call = `deliver-${session}`
      await acceptAuthorityRoot(runtime, session, 'engineer', physical)
      if (defect === 'prose from an older physical input') {
        const oldPhysical = `old-${physical}`
        runtime.pushHostMessage(session, userMessage(session, oldPhysical, 'OLDER WORK'))
        runtime.pushHostMessage(session, assistantMessage(session, oldPhysical, `old-${run}`, [{ type: 'text', text: 'OLDER PHYSICAL PROSE' }]))
      }
      const user = userMessage(session, physical, 'DELIVER THIS WORK')
      runtime.pushHostMessage(session, user)
      await hooks['chat.message']({ sessionID: session, messageID: physical, agent: 'engineer' }, { message: user.info, parts: user.parts })
      const readProgram = 'class Js extends JsProgram { async run() { return { text: await this.cat("evidence.txt") }; } }'
      const deliverProgram = 'class Js extends JsProgram { async run() { await this.deliver({ 0: "UNATTRIBUTED MATERIAL" }); } }'
      writeFileSync(join(directory, 'evidence.txt'), 'ACTUAL READ EVIDENCE')
      const parts = [toolPart(readCall, readProgram), toolPart(defect === 'an unmatched tool call' ? `other-${call}` : call, deliverProgram)]
      if (defect === 'an unmatched tool call') parts.unshift({ type: 'text', text: prose })
      runtime.pushHostMessage(session, assistantMessage(session, physical, run, parts, 3))
      await hooks['experimental.chat.messages.transform']({ sessionID: session }, { messages: [user] })

      const read = await hooks.tool['todowrite-for-engineer'].execute(
        { program: readProgram }, { sessionID: session, agent: 'engineer', callID: readCall, messageID: run },
      )
      assert.match(String(read), /# ok/)
      assert.match(String(read), /ACTUAL READ EVIDENCE/)
      const refused = await hooks.tool['todowrite-for-engineer'].execute(
        { program: deliverProgram }, { sessionID: session, agent: 'engineer', callID: call, messageID: run },
      )
      assert.match(String(refused), /DELIVERY_NOT_ESTABLISHED/)
      assert.equal(delivery.deliveredValue(session), null)
      assert.equal(delivery.deliveredViewOf(session), null)
      assert.equal(delivery.deliveredFenceOf(session), null)
      assert.equal(factPayloads(directory, 'TaskDeliveryCommitted').filter(witness => identityValue(witness.Fence.SessionId) === session).length, 0)
    })
  })
}

test('WHAT[delegation-033] the same physical turn prose before the actual deliver call attributes its sealed witness', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const session = 'ses-033-turn-prose'
    const physical = 'msg-033-turn-root'
    const run = 'run-033-turn'
    const call = 'call-033-turn'
    await acceptAuthorityRoot(runtime, session, 'engineer', physical)
    const user = userMessage(session, physical, 'DELIVER THIS WORK')
    runtime.pushHostMessage(session, user)
    await hooks['chat.message']({ sessionID: session, messageID: physical, agent: 'engineer' }, { message: user.info, parts: user.parts })
    const priorCall = 'call-033-turn-prose'
    const priorProgram = 'class Js extends JsProgram { async run() { return { ready: true }; } }'
    const prior = assistantMessage(session, physical, 'msg-033-turn-prose', [
      { type: 'text', text: 'THE TURN PROSE' }, toolPart(priorCall, priorProgram),
    ])
    runtime.pushHostMessage(session, prior)
    await hooks['experimental.chat.messages.transform']({ sessionID: session }, { messages: [user] })
    const priorResult = await hooks.tool['todowrite-for-engineer'].execute(
      { program: priorProgram },
      { sessionID: session, agent: 'engineer', callID: priorCall, messageID: prior.info.id },
    )
    assert.match(String(priorResult), /# ok/)
    assert.equal(delivery.deliveredValue(session), null)
    prior.parts.at(-1).state = { status: 'completed', input: { program: priorProgram }, output: String(priorResult) }
    prior.info.time.completed = 3
    prior.info.finish = 'tool-calls'
    await hooks.event({ event: { type: 'message.updated', properties: { info: prior.info } } })
    const program = 'class Js extends JsProgram { async run() { await this.deliver({ 0: "TURN PROSE MATERIAL" }); } }'
    runtime.pushHostMessage(session, assistantMessage(session, physical, run, [toolPart(call, program)], 4))
    await hooks['experimental.chat.messages.transform']({ sessionID: session }, { messages: [user] })
    const result = await hooks.tool['todowrite-for-engineer'].execute(
      { program }, { sessionID: session, agent: 'engineer', callID: call, messageID: run },
    )
    assert.match(String(result), /# ok/)
    const witness = deliveryOf(directory, session, run, call)
    assert.equal(witness.FormalText, 'THE TURN PROSE')
    assert.equal(identityValue(witness.Fence.PhysicalUserMessageId), physical)
    assert.equal(delivery.deliveredValue(session), witness.View)
    assert.match(witness.View, /TURN PROSE MATERIAL/)
  })
})

test('WHAT[delegation-033] an empty report mapping delivers its canonical sealed material only after the actual Host stop', { timeout: 10000 }, async () => {
  const owner = 'ses-owner-033-empty-mapping'
  await withRealFork(owner, async ({ hooks, directory, createdIds, runtime }) => {
    const receipt = await forkCall(hooks, owner, 'Ada', 'Report with no inherited context and actual pre-call prose')
    const agentId = /agentId = "([^"]+)"/.exec(receipt)?.[1]
    assert.ok(agentId)
    const child = createdIds[0]
    const physical = physicalUserMessageOf(runtime, child)
    const run = 'run-033-empty-mapping'
    const call = `call-${run}`
    const attributedProse = 'THE EMPTY MAPPING HAS THIS ACTUAL PRE-CALL PROSE'
    const program = 'class Js extends JsProgram { async run() { await this.deliver({}); } }'
    const assistant = assistantMessage(child, physical.id, run, [
      { type: 'text', text: attributedProse }, toolPart(call, program),
    ])
    const terminals = await observeTerminals(runtime)
    try {
      await runtime.awaitIngress()
      runtime.pushHostMessage(child, assistant)
      const user = userMessage(child, physical.id, '')
      user.parts = physical.parts ?? []
      await hooks['experimental.chat.messages.transform']({ sessionID: child }, { messages: [user] })
      await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
      const canonical = reportMaterial(child, {})
      assert.equal(canonical.ok, true, canonical.reason)
      assert.deepEqual(canonical.material, [], 'an empty mapping does not select existing context')

      const result = await hooks.tool['todowrite-for-engineer'].execute(
        { program }, { sessionID: child, agent: 'engineer', callID: call, messageID: run },
      )
      assert.match(String(result), /# ok/)
      const witness = deliveryOf(directory, child, run, call)
      assert.equal(witness.FormalText, attributedProse)
      assert.deepEqual(JSON.parse(witness.ViewDeclaration), { prefixUntil: 0, thenMap: {} })
      assert.equal(delivery.deliveredViewOf(child), null, 'publishing the sealed capture discards the raw view')
      const sealed = parse(witness.View)
      assert.deepEqual(Object.keys(sealed), ['returned'])
      assert.deepEqual(sealed.returned.map(piece => piece.text),
        [attributedProse],
        'the sealed capture adds only the actual attributed prose to the empty context selection')
      for (const piece of sealed.returned) {
        assert.deepEqual(Object.keys(piece).sort(), ['id', 'text'])
        assert.equal(typeof piece.id, 'string')
        assert.ok(piece.id.length > 0)
      }
      assert.equal(new Set(sealed.returned.map(piece => piece.id)).size, sealed.returned.length)
      assert.equal(delivery.deliveredValue(child), witness.View)
      assert.deepEqual(delivery.deliveredReturned(child), [])
      assert.deepEqual(terminals.seen, [], 'a committed delivery is not a Host terminal')
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, physical.id), 'Active')

      assistant.parts.at(-1).state = { status: 'completed', input: { program }, output: String(result) }
      assistant.info.time.completed = 3
      assistant.info.finish = 'stop'
      await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await terminals.awaitRun(child, run, { timeoutMs: 3000 })
      const completed = terminals.seen.filter(entry => entry.sessionId === child && entry.outcome.kind === 'Completed')
      assert.equal(completed.length, 1)
      assert.equal(completed[0].outcome.providerRun, run)
      assert.equal(completed[0].outcome.text, witness.View)

      const joined = await joinCall(hooks, owner)
      assert.match(joined, /THE EMPTY MAPPING HAS THIS ACTUAL PRE-CALL PROSE/)
      assert.equal((joinRecordsOf(joined).match(/\[\[data\.records\]\]/g) ?? []).length, 1)
      const returned = delivery.deliveredReturned(child)
      assert.ok(returned.some(key => key.includes(attributedProse.slice(0, 24))))
      assert.equal(delivery.deliveredFenceOf(child), null)
      await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: child } } })
      await new Promise(resolve => setImmediate(resolve))
      assert.equal(terminals.seen.filter(entry => entry.sessionId === child && entry.outcome.kind === 'Completed').length, 1)
      assert.deepEqual(delivery.deliveredReturned(child), returned)
      const joinedAgain = await joinCall(hooks, owner, 'call-join-033-empty-repeat')
      assert.equal((joinedAgain.match(/\[\[data\.records\]\]/g) ?? []).length, 0,
        'the completed work is reported by join exactly once')
    } finally {
      terminals.dispose()
    }
  })
})
