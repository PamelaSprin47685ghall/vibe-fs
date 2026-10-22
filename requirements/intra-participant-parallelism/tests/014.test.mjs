import assert from 'node:assert/strict'
import test from 'node:test'
import * as fission from '../../../dist/Execution/Fission/Surface.js'
import { deliveredFenceOf, deliveredValue, deliveredViewOf } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { anchors, applyView, clearAll } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { observeTerminals } from '../../delegation/tests/support/real-fork.mjs'
import { factPayloads, readJournal } from '../../verification-system/tests/e2e/support/journal-observer.js'

/** The aggregate the takeover prompt carries; production writes it. */
const AGGREGATE_MARKER = '[[fission_convergence]]'

/** Wait for a production effect to show up. Bounded, and it never asserts. */
const waitFor = async (predicate, message, timeoutMs = 30000) => {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    if (predicate()) return
    await new Promise((resolve) => setTimeout(resolve, 25))
  }
  assert.fail(message)
}

const promptText = (prompt) => JSON.stringify(prompt?.body?.parts ?? '')

/** The physical user message the Host last sent to one session. */
const physicalOf = (runtime, session) =>
  runtime.messages.filter((message) => message.id?.startsWith(`msg-${session}-`) && message.role === 'user').at(-1)

/** The anchor the session's own ledger assigned to one physical message. */
const anchorOf = (session, messageId) => anchors(session).find((entry) => entry.messageId === messageId)?.anchor

const identityValue = value => Array.isArray(value) ? value[1] : value

const deliveryOf = (directory, session, providerRun, toolCallId) => {
  const witnesses = factPayloads(directory, 'TaskDeliveryCommitted').filter(witness =>
    identityValue(witness.Fence.SessionId) === session && identityValue(witness.Fence.ProviderRun) === providerRun && identityValue(witness.Fence.ToolCallId) === toolCallId,
  )
  assert.equal(witnesses.length, 1, 'the exact delivering call must commit one durable witness')
  return witnesses[0]
}

const assertEmptyOwnerCell = owner => {
  assert.equal(deliveredValue(owner), null, 'the owner cell must stay empty until the owner delivers')
  assert.equal(deliveredViewOf(owner), null, 'an undelivered owner must have no view')
  assert.equal(deliveredFenceOf(owner), null, 'an undelivered owner must have no delivery fence')
}

test('WHAT[intra-participant-parallelism-014] production settlement decision yields nonfinal observations to the ordinary turn owner', () => {
  for (const phase of ['lane', 'takeover']) {
    for (const observation of ['running', 'needs-continuation', 'provider-failed', 'degeneration-interrupted']) {
      assert.equal(fission.settlementDecision(phase, observation), 'yield-to-turn-workflow')
    }
    assert.equal(fission.settlementDecision(phase, 'external-abort'), 'fail-group')
  }
  assert.equal(fission.settlementDecision('lane', 'completed'), 'materialize-lane')
  // LC-011: the pure decision still routes a stable completed takeover to
  // complete-owner, but the actual owner terminal publication is gated by
  // FissionHost.publishOwnerTakeover on an independent durable TaskDelivery
  // witness — a takeover owner that stops without its own integrated
  // delivery is rejected (RejectedUndelivered), never published as complete.
  assert.equal(fission.settlementDecision('takeover', 'completed'), 'complete-owner')
})

/**
 * The owner delivery cell is the owner's own cell: only what the owner itself
 * delivers may appear in it. Another participant's delivery (a Fission lane's
 * local delivery) is integration material that travels into the takeover
 * prompt through the aggregate; it is never staged into the owner's cell, and
 * it never stands in for the owner's own integrated delivery.
 *
 * Boundary: this file drives the real Fission admission and the real delivery
 * path (production `this.deliver`, production anchor ledger, production
 * delivery cell), and observes the owner cell through the registered
 * repository-programming surface.
 *
 * The first case checks the owner's exact durable declaration and sealed
 * material. The second drives lane settlement and takeover end to end: the
 * takeover delivery and its later text-only stop come from separate provider
 * steps, and the owner terminal must publish the sealed material. The owner
 * never delivers in that case, so its delivery cell stays empty throughout.
 */
test('WHAT[intra-participant-parallelism-014] the owner delivery cell stays empty until the owner delivers itself, and a lane delivery never lands in it', async () => {
  try {
    await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
      const managerSession = 'ses-fission-owner-cell-manager'
      await acceptAuthorityRoot(runtime, managerSession, 'manager')
      await openIncumbency(runtime, managerSession)

      const forked = await hooks.tool['todowrite-for-manager'].execute(
        {
          program:
            'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada", charge: "fission owner cell" }, {}); } }',
        },
        { sessionID: managerSession, agent: 'manager' },
      )
      assert.match(forked, /ok = true/)
      const ownerSession = createdIds[0]

      const fissioned = await hooks.tool.fission.execute(
        { prompts: ['lane one', 'lane two'] },
        { sessionID: ownerSession, agent: 'engineer', callID: 'call-fission-owner-cell', messageID: 'run-fission-owner-cell' },
      )
      assert.match(fissioned, /status\s*=\s*"fissioned"/i)
      await runtime.awaitIngress()

      const laneSessions = createdIds.filter((id) => id !== ownerSession)
      assert.equal(laneSessions.length, 2, 'the fission created the two lane sessions')
      assertEmptyOwnerCell(ownerSession)

      // Each lane delivers through the production delivery path. The lane
      // anchor is the lane's own anchored message: the anchor number belongs
      // to the lane's own ledger, and the declaration it belongs to is the
      // lane's, not the owner's.
      const laneAnchors = []
      for (const [index, laneSession] of laneSessions.entries()) {
        const physical = runtime.messages
          .filter((message) => message.id?.startsWith(`msg-${laneSession}-`) && message.role === 'user')
          .at(-1)
        assert.ok(physical, `lane ${index} must have received its physical user message`)

        applyView(laneSession, [{ id: physical.id, text: `lane context ${index}` }])
        const laneAnchor = anchors(laneSession).find((entry) => entry.messageId === physical.id)?.anchor
        assert.ok(laneAnchor, `lane ${index} must have its own anchor`)
        laneAnchors.push(laneAnchor)

        const run = `run-${laneSession}-1`
        const callID = `call-${laneSession}-1`
        const program = `class Js extends JsProgram { async run() { await this.deliver({ ${laneAnchor}: "LANE-MATERIAL-${index}" }); } }`
        const assistant = {
          info: {
            id: run,
            sessionID: laneSession,
            parentID: physical.id,
            role: 'assistant',
            agent: 'engineer',
            providerID: 'provider',
            modelID: 'engineer-model',
            time: { created: 2 },
          },
          parts: [
            { type: 'text', text: `lane ${index} report` },
            { id: `part-${callID}`, type: 'tool', tool: 'todowrite-for-engineer', callID,
              state: { status: 'pending', input: { program } } },
          ],
        }
        runtime.pushHostMessage(laneSession, assistant)
        await hooks['experimental.chat.messages.transform'](
          { sessionID: laneSession },
          {
            messages: [
              {
                info: {
                  id: physical.id,
                  sessionID: laneSession,
                  role: 'user',
                  agent: 'engineer',
                  model: { providerID: 'provider', modelID: 'engineer-model' },
                  time: { created: 1 },
                },
                parts: physical.parts ?? [],
              },
            ],
          },
        )

        const delivered = await hooks.tool['todowrite-for-engineer'].execute(
          { program },
          { sessionID: laneSession, agent: 'engineer', callID, messageID: run },
        )
        assert.match(delivered, /^# ok\b/m, `lane ${index} must commit its exact Host delivery`)
        assistant.parts[1].state = { status: 'completed', input: { program }, output: delivered }
        const witness = deliveryOf(directory, laneSession, run, callID)
        assert.equal(identityValue(witness.Fence.PhysicalUserMessageId), physical.id)
        assert.deepEqual(JSON.parse(witness.ViewDeclaration), { prefixUntil: 0, thenMap: { [laneAnchor]: `LANE-MATERIAL-${index}` } })
        assert.match(witness.View, new RegExp(`LANE-MATERIAL-${index}`))
        assert.equal(deliveredValue(laneSession), witness.View, 'the public cell must hold the sealed lane material')
        assert.deepEqual(deliveredFenceOf(laneSession), { providerRun: run, toolCallId: callID })
        assertEmptyOwnerCell(ownerSession)
      }

      // The owner cell: still empty. No lane delivery was staged into it, and
      // no lane anchor declaration was forwarded into it.
      assertEmptyOwnerCell(ownerSession)

      // The owner delivers itself: only now does its cell carry anything, and
      // what it carries is the owner's own material under the owner's own
      // anchor.
      const ownerPhysical = runtime.messages
        .filter((message) => message.id?.startsWith(`msg-${ownerSession}-`) && message.role === 'user')
        .at(-1)
      assert.ok(ownerPhysical, 'the owner session must have received its physical user message')
      applyView(ownerSession, [{ id: ownerPhysical.id, text: 'owner context' }])
      const ownerAnchor = anchors(ownerSession).find((entry) => entry.messageId === ownerPhysical.id)?.anchor
      assert.ok(ownerAnchor, 'the owner must have its own anchor')

      const ownerRun = `run-${ownerSession}-1`
      const ownerCall = `call-${ownerSession}-1`
      const ownerProgram = `class Js extends JsProgram { async run() { await this.deliver({ ${ownerAnchor}: "OWNER-MATERIAL" }); } }`
      const ownerAssistant = {
        info: {
          id: ownerRun,
          sessionID: ownerSession,
          parentID: ownerPhysical.id,
          role: 'assistant',
          agent: 'engineer',
          providerID: 'provider',
          modelID: 'engineer-model',
          time: { created: 2 },
        },
        parts: [
          { type: 'text', text: 'owner integrated report' },
          { id: `part-${ownerCall}`, type: 'tool', tool: 'todowrite-for-engineer', callID: ownerCall,
            state: { status: 'pending', input: { program: ownerProgram } } },
        ],
      }
      runtime.pushHostMessage(ownerSession, ownerAssistant)
      await hooks['experimental.chat.messages.transform'](
        { sessionID: ownerSession },
        {
          messages: [
            {
              info: {
                id: ownerPhysical.id,
                sessionID: ownerSession,
                role: 'user',
                agent: 'engineer',
                model: { providerID: 'provider', modelID: 'engineer-model' },
                time: { created: 1 },
              },
              parts: ownerPhysical.parts ?? [],
            },
          ],
        },
      )
      const ownerDelivered = await hooks.tool['todowrite-for-engineer'].execute(
        { program: ownerProgram },
        { sessionID: ownerSession, agent: 'engineer', callID: ownerCall, messageID: ownerRun },
      )
      assert.match(ownerDelivered, /^# ok\b/m, 'the owner must commit its own exact Host delivery')
      ownerAssistant.parts[1].state = { status: 'completed', input: { program: ownerProgram }, output: ownerDelivered }

      const ownerWitness = deliveryOf(directory, ownerSession, ownerRun, ownerCall)
      assert.equal(identityValue(ownerWitness.Fence.PhysicalUserMessageId), ownerPhysical.id)
      const ownerDeclaration = JSON.parse(ownerWitness.ViewDeclaration)
      assert.deepEqual(ownerDeclaration, { prefixUntil: 0, thenMap: { [ownerAnchor]: 'OWNER-MATERIAL' } })
      assert.match(ownerWitness.View, /OWNER-MATERIAL/)
      assert.equal(deliveredValue(ownerSession), ownerWitness.View, 'the public owner cell must hold its own sealed material')
      assert.deepEqual(deliveredFenceOf(ownerSession), { providerRun: ownerRun, toolCallId: ownerCall })
      for (const laneAnchor of laneAnchors) {
        assert.equal(
          Object.hasOwn(ownerDeclaration.thenMap, String(laneAnchor)),
          false,
          `the owner cell must not carry the lane anchor declaration ${laneAnchor}`,
        )
      }
      assert.doesNotMatch(ownerWitness.ViewDeclaration, /LANE-MATERIAL-/)
      assert.doesNotMatch(ownerWitness.View, /LANE-MATERIAL-/)
    })
  } finally {
    clearAll()
  }
})

/**
 * The takeover chain, driven for real, never stages a lane's delivery into the
 * owner delivery cell.
 *
 * The chain this case exercises is the production one: a Fission group admits
 * two lanes; each lane's own turn reaches a stable terminal (the production
 * recipe: physical user message → assistant message → provider transform →
 * real `this.deliver` → `message.updated` → `session.idle`); the group
 * materializes every lane (`FissionLaneMaterialized`) and converges by
 * sending the aggregate takeover prompt into the ring's final lane; that
 * lane's later provider step settles the takeover through
 * `FissionHost.observeCurrentTakeoverOutcome` → `CompleteOwner`.
 *
 * The owner delivery cell is the owner's own cell. The lane's delivery is
 * integration material: it travels into the takeover prompt through the
 * aggregate, and it never becomes the owner's cell content.
 */
test('WHAT[intra-participant-parallelism-014] the real takeover settles the owner without ever staging a lane delivery into the owner cell', async () => {
  try {
    await withExecutablePlugin(async (hooks, directory, createdIds, runtime) => {
      const managerSession = 'ses-fission-takeover-manager'
      await acceptAuthorityRoot(runtime, managerSession, 'manager')
      await openIncumbency(runtime, managerSession)

      const forked = await hooks.tool['todowrite-for-manager'].execute(
        {
          program:
            'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada", charge: "fission takeover" }, {}); } }',
        },
        { sessionID: managerSession, agent: 'manager' },
      )
      assert.match(forked, /ok = true/)
      const ownerSession = createdIds[0]

      const fissioned = await hooks.tool.fission.execute(
        { prompts: ['lane one', 'lane two'] },
        { sessionID: ownerSession, agent: 'engineer', callID: 'call-fission-takeover', messageID: 'run-fission-takeover' },
      )
      assert.match(fissioned, /status\s*=\s*"fissioned"/i)
      await runtime.awaitIngress()

      const laneSessions = createdIds.filter((id) => id !== ownerSession)
      assert.equal(laneSessions.length, 2, 'the fission created the two lane sessions')

      const terminals = await observeTerminals(runtime)
      const ownerRoot = factPayloads(directory, 'AuthorityRootAccepted').find(root => identityValue(root.SessionId) === ownerSession)
      assert.ok(ownerRoot, 'the logical owner must have its actual accepted root')
      assertEmptyOwnerCell(ownerSession)

      // Every lane runs one real turn to a stable terminal: the production
      // recipe, including the provider transform and the terminal events the
      // reconciler observes. The lane's own delivery is written by the
      // production delivery path.
      for (const [index, lane] of laneSessions.entries()) {
        const physical = physicalOf(runtime, lane)
        assert.ok(physical, `lane ${index} must have received its physical user message`)
        applyView(lane, [{ id: physical.id, text: `lane context ${index}` }])
        const anchor = anchorOf(lane, physical.id)
        assert.ok(anchor, `lane ${index} must have its own anchor`)

        const run = `run-${lane}-1`
        const callID = `call-${run}`
        const program = `class Js extends JsProgram { async run() { await this.deliver({ ${anchor}: "LANE-MATERIAL-${index}" }); } }`
        const assistant = {
          info: {
            id: run,
            role: 'assistant',
            sessionID: lane,
            parentID: physical.id,
            agent: 'engineer',
            providerID: 'provider',
            modelID: 'engineer-model',
            time: { created: 2 },
          },
          parts: [
            { type: 'text', text: `lane ${index} report` },
            { id: `part-${callID}`, type: 'tool', tool: 'todowrite-for-engineer', callID,
              state: { status: 'pending', input: { program } } },
          ],
        }
        runtime.pushHostMessage(lane, assistant)
        await hooks['experimental.chat.messages.transform'](
          { sessionID: lane },
          {
            messages: [
              {
                info: {
                  id: physical.id,
                  sessionID: lane,
                  role: 'user',
                  agent: 'engineer',
                  model: { providerID: 'provider', modelID: 'engineer-model' },
                  time: { created: 1 },
                },
                parts: physical.parts ?? [],
              },
            ],
          },
        )
        const delivered = await hooks.tool['todowrite-for-engineer'].execute(
          { program },
          { sessionID: lane, agent: 'engineer', callID, messageID: run },
        )
        assert.match(delivered, /^# ok\b/m, `lane ${index} must commit its exact Host delivery`)
        assistant.parts[1].state = { status: 'completed', input: { program }, output: delivered }
        const witness = deliveryOf(directory, lane, run, callID)
        assert.equal(identityValue(witness.Fence.PhysicalUserMessageId), physical.id)
        assert.equal(identityValue(witness.Fence.AuthorityRoot), identityValue(ownerRoot.AuthorityRootUserMessageId))
        assert.equal(identityValue(witness.Fence.LogicalRunId), identityValue(ownerRoot.LogicalRunId))
        assert.deepEqual(JSON.parse(witness.ViewDeclaration), { prefixUntil: 0, thenMap: { [anchor]: `LANE-MATERIAL-${index}` } })
        assert.match(witness.View, new RegExp(`LANE-MATERIAL-${index}`))
        assert.equal(deliveredValue(lane), witness.View, 'the public cell must hold the sealed lane material')
        assert.deepEqual(deliveredFenceOf(lane), { providerRun: run, toolCallId: callID })
        assertEmptyOwnerCell(ownerSession)
        assert.equal(terminals.seen.some(entry => entry.sessionId === ownerSession), false, 'a lane delivery alone cannot publish the owner terminal')
        assistant.info.time.completed = 3
        assistant.info.finish = 'stop'
        await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
        await hooks.event({ event: { type: 'session.idle', properties: { sessionID: lane } } })
        await Promise.all([
          hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } }),
          hooks.event({ event: { type: 'session.idle', properties: { sessionID: lane } } }),
          hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } }),
        ])
        assertEmptyOwnerCell(ownerSession)
      }

      // The converged group routes the whole ring bundle back into the ring's
      // final lane. The target is production's choice; the fixture reads it
      // from the aggregate prompt instead of assuming a lane order.
      await waitFor(
        () => runtime.prompts.some((prompt) => promptText(prompt).includes(AGGREGATE_MARKER)),
        'the convergence must send the aggregate takeover prompt into the ring',
      )
      const takeoverPrompt = runtime.prompts.find((prompt) => promptText(prompt).includes(AGGREGATE_MARKER))
      assert.ok(takeoverPrompt, 'the takeover prompt must be observable')
      assert.match(promptText(takeoverPrompt), /lane_count = 2/, 'the aggregate must carry the group size')
      for (const index of [0, 1]) {
        assert.match(promptText(takeoverPrompt), new RegExp(`LANE-MATERIAL-${index}`), 'the takeover prompt must carry each real lane delivery')
      }
      assert.deepEqual(
        factPayloads(directory, 'FissionLaneMaterialized').map(lane => identityValue(lane.LaneSessionId)).sort(),
        [...laneSessions].sort(),
        'both exact lane terminals must be materialized before takeover',
      )
      const group = factPayloads(directory, 'FissionAdmitted').find(group => identityValue(group.OwnerSessionId) === ownerSession)
      assert.ok(group)
      const materialized = factPayloads(directory, 'FissionLaneMaterialized')
      assert.equal(materialized.length, 2, 'reobserving the real lane stops cannot append another materialization')
      for (const lane of laneSessions) {
        const [record] = materialized.filter(record => identityValue(record.LaneSessionId) === lane)
        const run = `run-${lane}-1`
        const witness = deliveryOf(directory, lane, run, `call-${run}`)
        assert.equal(record.GroupId, group.GroupId)
        assert.equal(identityValue(record.OwnerSessionId), ownerSession)
        assert.equal(record.LaneIndex, group.LaneSessions.findIndex(session => identityValue(session) === lane))
        assert.equal(identityValue(record.ProviderRun), run)
        assert.equal(identityValue(record.ViewDigest), witness.MaterialDigest)
      }
      assert.equal(readJournal(directory, 'FissionConverged').named, 0, 'the takeover prompt alone cannot settle the group')
      assertEmptyOwnerCell(ownerSession)
      assert.equal(terminals.seen.some(entry => entry.sessionId === ownerSession), false, 'local lane reports cannot publish the owner terminal')
      const takeoverLane = takeoverPrompt.path.id
      assert.ok(laneSessions.includes(takeoverLane), 'the takeover must target a lane of this group')
      await runtime.awaitIngress()

      // The takeover lane answers: one more real turn, with the integrated
      // delivery production requires for the owner terminal's durable witness.
      const takeoverPhysical = physicalOf(runtime, takeoverLane)
      assert.ok(takeoverPhysical, 'the takeover lane must have received the takeover prompt')
      applyView(takeoverLane, [{ id: takeoverPhysical.id, text: 'takeover context' }])
      const takeoverAnchor = anchorOf(takeoverLane, takeoverPhysical.id)
      assert.ok(takeoverAnchor, 'the takeover lane must have an anchor for the takeover turn')

      const takeoverRun = `run-${takeoverLane}-takeover`
      const takeoverCall = `call-${takeoverRun}`
      const takeoverProgram = `class Js extends JsProgram { async run() { await this.deliver({ ${takeoverAnchor}: "INTEGRATED-MATERIAL" }); } }`
      const takeoverAssistant = {
        info: {
          id: takeoverRun,
          role: 'assistant',
          sessionID: takeoverLane,
          parentID: takeoverPhysical.id,
          agent: 'engineer',
          providerID: 'provider',
          modelID: 'engineer-model',
          time: { created: 2 },
        },
        parts: [
          { type: 'text', text: 'integrated report' },
          { id: `part-${takeoverCall}`, type: 'tool', tool: 'todowrite-for-engineer', callID: takeoverCall,
            state: { status: 'pending', input: { program: takeoverProgram } } },
        ],
      }
      runtime.pushHostMessage(takeoverLane, takeoverAssistant)
      await hooks['experimental.chat.messages.transform'](
        { sessionID: takeoverLane },
        {
          messages: [
            {
              info: {
                id: takeoverPhysical.id,
                sessionID: takeoverLane,
                role: 'user',
                agent: 'engineer',
                model: { providerID: 'provider', modelID: 'engineer-model' },
                time: { created: 1 },
              },
              parts: takeoverPhysical.parts ?? [],
            },
          ],
        },
      )
      const takeoverDelivered = await hooks.tool['todowrite-for-engineer'].execute(
        { program: takeoverProgram },
        { sessionID: takeoverLane, agent: 'engineer', callID: takeoverCall, messageID: takeoverRun },
      )
      assert.match(takeoverDelivered, /^# ok\b/m, 'the takeover must commit its own integrated Host delivery')
      takeoverAssistant.parts[1].state = { status: 'completed', input: { program: takeoverProgram }, output: takeoverDelivered }
      const takeoverWitness = deliveryOf(directory, takeoverLane, takeoverRun, takeoverCall)
      assert.equal(identityValue(takeoverWitness.Fence.PhysicalUserMessageId), takeoverPhysical.id)
      assert.deepEqual(JSON.parse(takeoverWitness.ViewDeclaration), { prefixUntil: 0, thenMap: { [takeoverAnchor]: 'INTEGRATED-MATERIAL' } })
      assert.match(takeoverWitness.View, /INTEGRATED-MATERIAL/)
      assert.doesNotMatch(takeoverWitness.View, /LANE-MATERIAL-/)
      assert.equal(deliveredValue(takeoverLane), takeoverWitness.View)
      assert.deepEqual(deliveredFenceOf(takeoverLane), { providerRun: takeoverRun, toolCallId: takeoverCall })
      assertEmptyOwnerCell(ownerSession)
      assert.equal(terminals.seen.some(entry => entry.sessionId === ownerSession), false, 'integrated delivery still requires a later physical terminal')
      takeoverAssistant.info.time.completed = 3
      takeoverAssistant.info.finish = 'tool-calls'
      await hooks.event({ event: { type: 'message.updated', properties: { info: takeoverAssistant.info } } })
      assertEmptyOwnerCell(ownerSession)
      assert.equal(readJournal(directory, 'FissionConverged').named, 0, 'the delivering provider tool boundary cannot settle takeover')
      assert.equal(terminals.seen.some(entry => entry.sessionId === ownerSession), false, 'a tool boundary cannot publish the owner terminal')

      const takeoverStopRun = `run-${takeoverLane}-takeover-stop`
      const takeoverStop = {
        info: {
          ...takeoverAssistant.info,
          id: takeoverStopRun,
          finish: 'stop',
          time: { created: 4, completed: 5 },
        },
        parts: [{ id: `part-${takeoverStopRun}`, type: 'text', text: 'LATE_TAKEOVER_PROSE' }],
      }
      runtime.pushHostMessage(takeoverLane, takeoverStop)
      await hooks.event({ event: { type: 'message.updated', properties: { info: takeoverStop.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: takeoverLane } } })

      // The takeover settled: production published the owner's terminal.
      await waitFor(
        () => terminals.seen.some((entry) => entry.sessionId === ownerSession && entry.outcome?.kind === 'Completed') &&
          readJournal(directory, 'FissionConverged').named === 1,
        'the takeover must publish the owner terminal and durably settle the group',
      )
      assert.deepEqual(
        terminals.seen.filter(entry => entry.sessionId === ownerSession).map(entry => entry.outcome),
        [{ kind: 'Completed', providerRun: takeoverStopRun, text: takeoverWitness.View }],
        'the later stop publishes the sealed integrated material exactly once',
      )
      assert.doesNotMatch(terminals.seen.find(entry => entry.sessionId === ownerSession).outcome.text, /LATE_TAKEOVER_PROSE/)
      const converged = factPayloads(directory, 'FissionConverged')
      assert.equal(converged.length, 1, 'the group must durably converge once')
      assert.equal(identityValue(converged[0].OwnerSessionId), ownerSession)
      assert.equal(identityValue(converged[0].TerminalLaneSessionId), takeoverLane)
      assert.equal(identityValue(converged[0].TerminalProviderRun), takeoverStopRun)
      assert.equal(factPayloads(directory, 'TaskDeliveryCommitted').filter(witness => identityValue(witness.Fence.SessionId) === ownerSession).length, 0)
      assert.equal(factPayloads(directory, 'TaskDeliveryCommitted').filter(witness => identityValue(witness.Fence.ProviderRun) === takeoverStopRun).length, 0, 'the text-only stop cannot create a delivery witness')

      // The owner cell is the owner's own cell. The owner never delivered
      // here, so the cell must stay empty: no lane anchor, no lane material.
      assertEmptyOwnerCell(ownerSession)

      terminals.dispose()
    })
  } finally {
    clearAll()
  }
})
