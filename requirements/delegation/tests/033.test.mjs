import assert from 'node:assert/strict'
import test from 'node:test'
import * as eventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'
import { createJoinProbe, joinProbeDeliver, validateDeliveredMaterial } from '../../../dist/Execution/Delegation/Fork/JoinSurface.js'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import { deliveredValue } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { readdirSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {
  acceptAuthorityRoot,
  bindManagedChild,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

// WHAT[delegation-033]: the delivery cut ends the delivering attempt, not the
// delivery. The committed witness must settle that same run before the
// interrupt, so the owner's join receives the delivered view instead of
// waiting for a terminal that never addresses the delivering run.
const material = 'DELIVERED MATERIAL'
const prose = 'DELIVERED PROSE'

const findDeliveryWitnessFact = (directory) => {
  const commonDir = join(directory, '.Herdchestra')
  const eventsDir = join(commonDir, 'wanxiangshu', 'events')
  const files = readdirSync(eventsDir)
  const store = eventStore.create(commonDir, files[0].split('-')[0])
  try {
    for (const stream of eventStore.streams(store)) {
      let id = eventStore.head(store, stream)
      while (id) {
        const ev = eventStore.read(store, id)
        if (ev?.payload?.Fact?.[1]?.[1]?.[0] === 'TaskDeliveryCommitted') {
          return ev.payload.Fact[1][1][1]
        }
        id = ev?.parents?.[0]
      }
    }
    return null
  } finally {
    eventStore.dispose(store)
  }
}

test('WHAT[delegation-033] consuming a delivery before the lazy read still settles a journal-less run', { timeout: 3000 }, async () => {
  const result = await joinProbeDeliver(createJoinProbe(), ['selected evidence'], 'current report')
  assert.deepEqual(result, {
    status: 'completed',
    body: 'current report',
    returned: [{ text: 'selected evidence' }],
  })
})

test('WHAT[delegation-033] the committed delivery settles the delivering run before the cut', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const owner = 'ses-owner-033'
    const child = 'ses-child-033'
    const physical = 'msg-033-root'
    const run = 'run-033'
    const call = 'call-033'

    await bindManagedChild(runtime, owner, child, 'engineer', 'h-033')

    const root = {
      info: { id: physical, role: 'user', sessionID: child, agent: 'engineer', time: { created: 1 } },
      parts: [{ type: 'text', text: 'DELIVER THIS WORK' }],
    }
    runtime.pushHostMessage(child, root)
    await hooks['chat.message'](
      { sessionID: child, messageID: physical, agent: 'engineer' },
      { message: root, parts: root.parts },
    )
    runtime.pushHostMessage(child, {
      info: { id: 'msg-033-prose', role: 'assistant', sessionID: child, parentID: physical, agent: 'engineer', time: { created: 2 } },
      parts: [{ type: 'text', text: prose }],
    })
    runtime.pushHostMessage(child, {
      info: { id: run, role: 'assistant', sessionID: child, parentID: physical, agent: 'engineer', time: { created: 3 } },
      parts: [],
    })

    const port = await runtime.terminalPort
    const seen = []
    const subscription = eventsSurface.subscribe(port, (sessionId, outcome) => seen.push({ sessionId, outcome }))
    try {
      const result = await hooks.tool['todowrite-for-engineer'].execute(
        {
          name: 'deliver-test-report',
          program: `class Js extends JsProgram { async run() { await this.deliver([${JSON.stringify(material)}]); return { done: true }; } }`,
          prefixUntil: 0,
          carry: []
        },
        { sessionID: child, agent: 'engineer', callID: call, messageID: run },
      )
      assert.match(String(result), /ok/i)

      // Transform hook executes StopAfterDelivery: settles run before interrupt
      const outObj = { messages: [{ info: { id: physical, role: 'user' }, parts: [] }] }
      await hooks['experimental.chat.messages.transform']({ sessionID: child }, outObj)

      const completed = seen.find(
        (entry) => entry.sessionId === child && entry.outcome?.kind === 'Completed' && entry.outcome?.providerRun === run,
      )
      assert.ok(completed, 'the delivering run publishes its completed terminal before the cut')
      assert.equal(completed.outcome.text, prose, 'terminal prose never contains encoded or rendered delivery material')

      // The witness is recorded durably in the event store.
      const witness = findDeliveryWitnessFact(directory)
      assert.ok(witness, 'the delivery committed a durable witness')
      assert.match(witness.View, /DELIVERED MATERIAL/)
    } finally {
      eventsSurface.dispose(subscription)
    }
  })
})

test('WHAT[delegation-033] an unattributed durable delivery is refused, not left joinable-only in memory', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const session = 'sess-033-unattributed'
    const root = 'msg-033-unattributed-root'
    const run = 'run-033-unattributed'
    const call = 'call-033-unattributed'

    await acceptAuthorityRoot(runtime, session, 'engineer', root)

    // An unattributed call (no owning assistant prose or physical input) fails visibly
    const result = await hooks.tool['todowrite-for-engineer'].execute(
      {
        name: 'unattributed-delivery',
        program:
          'class Js extends JsProgram { async run() { await this.deliver(["UNATTRIBUTED MATERIAL"]); return { done: true }; } }',
          prefixUntil: 0,
          carry: []
        },
      { sessionID: session, agent: 'engineer', callID: call, messageID: 'unattributed-missing-run' },
    )

    assert.match(String(result), /DELIVERY_NOT_ESTABLISHED/, 'the failure is DELIVERY_NOT_ESTABLISHED')
    assert.equal(deliveredValue(session), null, 'no process-live cell pretends the delivery happened')
  })
})

// The delivery contract tells the model to state the result in prose and then

// The delivery contract tells the model to state the result in prose and then
// deliver. Providers often split those into two assistant messages, so the
// prose of the same turn (after this call's physical input) must attribute the
// delivery; otherwise a real deliver has no witness and the owner's join
// waits forever.
test('WHAT[delegation-033] the turn prose before the deliver call attributes its witness', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const session = 'ses-033-turn-prose'
    const root = 'msg-033-turn-root'
    const run = 'run-033-turn'
    const call = 'call-033-turn'

    await acceptAuthorityRoot(runtime, session, 'engineer', root)
    runtime.pushHostMessage(session, {
      info: { id: root, role: 'user', sessionID: session, agent: 'engineer', time: { created: 1 } },
      parts: [{ type: 'text', text: 'DELIVER THIS WORK' }],
    })
    await hooks['chat.message'](
      { sessionID: session, messageID: root, agent: 'engineer' },
      { message: runtime.messages.at(-1), parts: runtime.messages.at(-1).parts },
    )
    runtime.pushHostMessage(session, {
      info: {
        id: 'msg-033-turn-prose',
        role: 'assistant',
        sessionID: session,
        parentID: root,
        agent: 'engineer',
        providerID: 'provider',
        modelID: 'engineer-model',
        time: { created: 2 },
      },
      parts: [{ type: 'text', text: 'THE TURN PROSE' }],
    })
    runtime.pushHostMessage(session, {
      info: {
        id: run,
        role: 'assistant',
        sessionID: session,
        parentID: root,
        agent: 'engineer',
        providerID: 'provider',
        modelID: 'engineer-model',
        time: { created: 3 },
      },
      parts: [],
    })

    const result = await hooks.tool['todowrite-for-engineer'].execute(
      {
        name: 'deliver-turn-report',
        program:
          'class Js extends JsProgram { async run() { await this.deliver(); return { done: true }; } }',
          prefixUntil: 0,
          carry: []
        },
      { sessionID: session, agent: 'engineer', callID: call, messageID: run },
    )
    assert.match(String(result), /ok/i)

    const witness = findDeliveryWitnessFact(_directory)
    assert.ok(witness, 'the turn prose attributes the delivery witness')
    assert.equal(witness.FormalText, 'THE TURN PROSE')
    assert.deepEqual(JSON.parse(witness.View), { body: ['THE TURN PROSE'], carry: [] })
  })
})

test('WHAT[delegation-033] absent or whitespace assistant body stops child deliver and suffix side effect without witness or completion', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const session = 'sess-033-empty-body'
    const root = 'msg-033-empty-root'
    const run = 'run-033-empty'
    const call = 'call-033-empty'

    await acceptAuthorityRoot(runtime, session, 'engineer', root)
    runtime.pushHostMessage(session, {
      info: { id: root, role: 'user', sessionID: session, agent: 'engineer', time: { created: 1 } },
      parts: [{ type: 'text', text: 'DELIVER THIS WORK' }],
    })
    await hooks['chat.message'](
      { sessionID: session, messageID: root, agent: 'engineer' },
      { message: runtime.messages.at(-1), parts: runtime.messages.at(-1).parts },
    )
    // Assistant message carries no prose (whitespace only)
    runtime.pushHostMessage(session, {
      info: { id: run, role: 'assistant', sessionID: session, parentID: root, agent: 'engineer', time: { created: 2 } },
      parts: [{ type: 'text', text: '   \n\t  ' }],
    })

    const port = await runtime.terminalPort
    const seen = []
    const subscription = eventsSurface.subscribe(port, (sessionId, outcome) => seen.push({ sessionId, outcome }))
    try {
      const result = await hooks.tool['todowrite-for-engineer'].execute(
        {
          name: 'empty-body-delivery',
          program:
            'class Js extends JsProgram { async run() { await this.deliver(); await this.write("suffix-never-runs.txt", "leak"); return { done: true }; } }',
          prefixUntil: 0,
          carry: []
        },
        { sessionID: session, agent: 'engineer', callID: call, messageID: run },
      )

      // Must abort visibly with error reminding LLM to output prose first
      assert.match(String(result), /DELIVERY_NOT_ESTABLISHED/, 'the failure code is DELIVERY_NOT_ESTABLISHED')
      assert.match(String(result), /prose/i, 'the error reminds LLM to output prose first')

      // No live cell recorded
      assert.equal(deliveredValue(session), null, 'no process-live cell pretends delivery happened')

      // No witness committed
      assert.equal(findDeliveryWitnessFact(directory), null, 'no durable witness fact is committed')

      // No completed terminal published
      const completed = seen.find(
        (entry) => entry.sessionId === session && entry.outcome?.kind === 'Completed' && entry.outcome?.providerRun === run,
      )
      assert.equal(completed, undefined, 'no completion is published without prose body')
    } finally {
      eventsSurface.dispose(subscription)
    }
  })
})

test('WHAT[delegation-033] carry string alone does not substitute for absent assistant prose body', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const session = 'sess-033-carry-no-body'
    const root = 'msg-033-carry-root'
    const run = 'run-033-carry'
    const call = 'call-033-carry'

    await acceptAuthorityRoot(runtime, session, 'engineer', root)
    runtime.pushHostMessage(session, {
      info: { id: root, role: 'user', sessionID: session, agent: 'engineer', time: { created: 1 } },
      parts: [{ type: 'text', text: 'DELIVER THIS WORK' }],
    })
    await hooks['chat.message'](
      { sessionID: session, messageID: root, agent: 'engineer' },
      { message: runtime.messages.at(-1), parts: runtime.messages.at(-1).parts },
    )
    runtime.pushHostMessage(session, {
      info: { id: run, role: 'assistant', sessionID: session, parentID: root, agent: 'engineer', time: { created: 2 } },
      parts: [],
    })

    const result = await hooks.tool['todowrite-for-engineer'].execute(
      {
        name: 'carry-only-delivery',
        program:
          'class Js extends JsProgram { async run() { await this.deliver(["CARRY STRING ALONE"]); return { done: true }; } }',
          prefixUntil: 0,
          carry: []
        },
      { sessionID: session, agent: 'engineer', callID: call, messageID: run },
    )

    assert.match(String(result), /DELIVERY_NOT_ESTABLISHED/, 'fails with DELIVERY_NOT_ESTABLISHED')
    assert.match(String(result), /prose/i, 'reminds LLM to output prose first')
    assert.equal(deliveredValue(session), null, 'no process-live cell recorded')
    assert.equal(findDeliveryWitnessFact(directory), null, 'no witness committed when carry is provided without prose body')
  })
})

