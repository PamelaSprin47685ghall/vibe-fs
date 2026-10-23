import assert from 'node:assert/strict'
import test from 'node:test'
import * as fission from '../../../dist/Execution/Fission/Surface.js'
import { deliveredValue, deliveredViewOf } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { anchors, applyView, clearAll } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { observeTerminals } from '../../delegation/tests/support/real-fork.mjs'

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

test('WHAT[intra-participant-parallelism-014] production settlement decision yields nonfinal observations to the ordinary turn owner', () => {
  for (const phase of ['lane', 'takeover']) {
    for (const observation of ['running', 'needs-continuation', 'provider-failed']) {
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
 * Three cases live here. The first one drives the real admission and the real
 * delivery path and asserts that the owner cell holds only the owner's own
 * delivery, with no lane anchor and no lane material. The second one drives
 * the whole takeover chain for real: each lane's own turn settles as a stable
 * TurnCompleted, the group materializes every lane
 * (`FissionLaneMaterialized`), the aggregate takeover prompt is sent into the
 * ring's final lane, and that lane's next turn settles the takeover through
 * `FissionHost.observeCurrentTakeoverOutcome` → `CompleteOwner`. The second
 * case is the failing-before/passing-after proof that the takeover branch
 * never stages a lane delivery into the owner cell: with the cross-session
 * staging restored, the owner cell carries the lane's view and the case
 * fails; with the branch as it stands, the owner cell stays empty. The third
 * case (the last `test(...)` in this file) drives the same real takeover
 * chain end to end and asserts the owner's own settlement: the owner cell
 * must stay empty because the owner never delivered, and no lane anchor and
 * no lane material may appear in it. The third case is the regression for
 * the lane authority inheritance: before the inheritance fix, the lane turn
 * never reconciles, the group never converges, and the case fails at the
 * aggregate-prompt wait instead of reaching the owner-cell assertion.
 */
test('WHAT[intra-participant-parallelism-014] the owner delivery cell stays empty until the owner delivers itself, and a lane delivery never lands in it', async () => {
  try {
    await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
      const managerSession = 'ses-fission-owner-cell-manager'
      await acceptAuthorityRoot(runtime, managerSession, 'manager')
      await openIncumbency(runtime, managerSession)

      const forked = await hooks.tool['todowrite-for-manager'].execute(
        {
          program:
            'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada", charge: "fission owner cell" }, []); } }',
          prefixUntil: 0,
          carry: []
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

      const laneSessions = createdIds.filter((id) => id !== ownerSession)
      assert.equal(laneSessions.length, 2, 'the fission created the two lane sessions')

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

        const assistant = {
          info: {
            id: `run-${laneSession}-1`,
            sessionID: laneSession,
            parentID: physical.id,
            role: 'assistant',
            agent: 'engineer',
            providerID: 'provider',
            modelID: 'engineer-model',
            time: { created: 2 },
          },
          parts: [{ type: 'text', text: `lane ${index} report` }],
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

        await hooks.tool['todowrite-for-engineer'].execute(
          {
            program: `class Js extends JsProgram { async run() { await this.deliver([${laneAnchor}, "LANE-MATERIAL-${index}"]); } }`,
          prefixUntil: 0,
          carry: []
        },
          { sessionID: laneSession, agent: 'engineer', callID: `call-${laneSession}-1`, messageID: `run-${laneSession}-1` },
        )
      }

      // The lanes really delivered: each lane's own cell holds its material.
      for (const [index, laneSession] of laneSessions.entries()) {
        const laneCell = deliveredValue(laneSession)
        assert.ok(Array.isArray(laneCell), `lane ${index} must hold its own carry array`)
        assert.deepEqual(
          [...laneCell],
          [laneAnchors[index], `LANE-MATERIAL-${index}`],
          `lane ${index} must hold only its own selected anchor and literal`,
        )
      }

      // The owner cell: still empty. No lane delivery was staged into it, and
      // no lane anchor declaration was forwarded into it.
      assert.equal(deliveredValue(ownerSession), null, 'the owner delivery cell must be empty before the owner delivers')
      assert.equal(deliveredViewOf(ownerSession), null, 'the owner delivery cell must hold no view before the owner delivers')

      // The owner delivers itself: only now does its cell carry anything, and
      // what it carries is the owner's own anchor and literal material.
      const ownerPhysical = runtime.messages
        .filter((message) => message.id?.startsWith(`msg-${ownerSession}-`) && message.role === 'user')
        .at(-1)
      assert.ok(ownerPhysical, 'the owner session must have received its physical user message')
      applyView(ownerSession, [{ id: ownerPhysical.id, text: 'owner context' }])
      const ownerAnchor = anchors(ownerSession).find((entry) => entry.messageId === ownerPhysical.id)?.anchor
      assert.ok(ownerAnchor, 'the owner must have its own anchor')

      const ownerAssistant = {
        info: {
          id: `run-${ownerSession}-1`,
          sessionID: ownerSession,
          parentID: ownerPhysical.id,
          role: 'assistant',
          agent: 'engineer',
          providerID: 'provider',
          modelID: 'engineer-model',
          time: { created: 2 },
        },
        parts: [{ type: 'text', text: 'owner integrated report' }],
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
      await hooks.tool['todowrite-for-engineer'].execute(
        {
          program: `class Js extends JsProgram { async run() { await this.deliver([${ownerAnchor}, "OWNER-MATERIAL"]); } }`,
          prefixUntil: 0,
          carry: []
        },
        { sessionID: ownerSession, agent: 'engineer', callID: `call-${ownerSession}-1`, messageID: `run-${ownerSession}-1` },
      )

      const ownerCell = deliveredValue(ownerSession)
      assert.ok(Array.isArray(ownerCell), 'the owner cell must hold the owner carry array after delivery')
      assert.deepEqual(
        [...ownerCell],
        [ownerAnchor, 'OWNER-MATERIAL'],
        'the owner cell must hold only its own selected anchor and literal',
      )
      for (const laneAnchor of laneAnchors) {
        assert.equal(
          ownerCell.includes(laneAnchor),
          false,
          `the owner cell must not carry the lane anchor declaration ${laneAnchor}`,
        )
      }
      assert.equal(
        ownerCell.some((value) => String(value).startsWith('LANE-MATERIAL-')),
        false,
        'the owner cell must not carry a lane delivery as the owner delivery',
      )
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
 * lane's next turn settles the takeover through
 * `FissionHost.observeCurrentTakeoverOutcome` → `CompleteOwner`.
 *
 * The owner delivery cell is the owner's own cell. The lane's delivery is
 * integration material: it travels into the takeover prompt through the
 * aggregate, and it never becomes the owner's cell content.
 */
test('WHAT[intra-participant-parallelism-014] the real takeover settles the owner without ever staging a lane delivery into the owner cell', async () => {
  try {
    await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
      const managerSession = 'ses-fission-takeover-manager'
      await acceptAuthorityRoot(runtime, managerSession, 'manager')
      await openIncumbency(runtime, managerSession)

      const forked = await hooks.tool['todowrite-for-manager'].execute(
        {
          program:
            'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada", charge: "fission takeover" }, []); } }',
          prefixUntil: 0,
          carry: []
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

      const laneSessions = createdIds.filter((id) => id !== ownerSession)
      assert.equal(laneSessions.length, 2, 'the fission created the two lane sessions')

      const terminals = await observeTerminals(runtime)
      const laneAnchors = []

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
        laneAnchors.push(anchor)

        const run = `run-${lane}-1`
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
          parts: [{ type: 'text', text: `lane ${index} report` }],
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
        await hooks.tool['todowrite-for-engineer'].execute(
          {
            program: `class Js extends JsProgram { async run() { await this.deliver([${anchor}, "LANE-MATERIAL-${index}"]); } }`,
          prefixUntil: 0,
          carry: []
        },
          { sessionID: lane, agent: 'engineer', callID: `call-${run}`, messageID: run },
        )
        assistant.info.time.completed = 3
        assistant.info.finish = 'stop'
        await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
        await hooks.event({ event: { type: 'session.idle', properties: { sessionID: lane } } })
      }

      // The lanes really delivered: each lane's own cell holds its material.
      for (const [index, lane] of laneSessions.entries()) {
        const laneCell = deliveredValue(lane)
        assert.ok(Array.isArray(laneCell), `lane ${index} must hold its own carry array`)
        assert.deepEqual(
          [...laneCell],
          [laneAnchors[index], `LANE-MATERIAL-${index}`],
          `lane ${index} must hold only its own selected anchor and literal`,
        )
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
      const takeoverLane = takeoverPrompt.path.id
      assert.ok(laneSessions.includes(takeoverLane), 'the takeover must target a lane of this group')

      // The takeover lane answers: one more real turn, with the integrated
      // delivery production requires for the owner terminal's durable witness.
      const takeoverPhysical = physicalOf(runtime, takeoverLane)
      assert.ok(takeoverPhysical, 'the takeover lane must have received the takeover prompt')
      applyView(takeoverLane, [{ id: takeoverPhysical.id, text: 'takeover context' }])
      const takeoverAnchor = anchorOf(takeoverLane, takeoverPhysical.id)
      assert.ok(takeoverAnchor, 'the takeover lane must have an anchor for the takeover turn')

      const takeoverRun = `run-${takeoverLane}-takeover`
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
        parts: [{ type: 'text', text: 'integrated report' }],
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
      await hooks.tool['todowrite-for-engineer'].execute(
        {
          program: `class Js extends JsProgram { async run() { await this.deliver([${takeoverAnchor}, "INTEGRATED-MATERIAL"]); } }`,
          prefixUntil: 0,
          carry: []
        },
        { sessionID: takeoverLane, agent: 'engineer', callID: `call-${takeoverRun}`, messageID: takeoverRun },
      )
      takeoverAssistant.info.time.completed = 3
      takeoverAssistant.info.finish = 'stop'
      await hooks.event({ event: { type: 'message.updated', properties: { info: takeoverAssistant.info } } })
      await hooks.event({ event: { type: 'session.idle', properties: { sessionID: takeoverLane } } })

      // The takeover settled: production published the owner's terminal.
      await waitFor(
        () => terminals.seen.some((entry) => entry.sessionId === ownerSession && entry.outcome?.kind === 'Completed'),
        'the takeover must publish the owner terminal',
      )

      // The owner cell is the owner's own cell. The owner never delivered
      // here, so the cell must stay empty: no lane anchor, no lane material.
      const ownerCell = deliveredValue(ownerSession)
      assert.equal(ownerCell, null, 'the owner delivery cell must stay empty: the owner never delivered')
      assert.equal(deliveredViewOf(ownerSession), null, 'the owner delivery cell must hold no view')
      for (const laneAnchor of laneAnchors) {
        assert.equal(
          (ownerCell ?? []).includes(laneAnchor),
          false,
          `the owner cell must not carry the lane anchor declaration ${laneAnchor}`,
        )
      }
      assert.equal(
        (ownerCell ?? []).some((value) => String(value).startsWith('LANE-MATERIAL-')),
        false,
        'the owner cell must not carry a lane delivery as the owner delivery',
      )

      terminals.dispose()
    })
  } finally {
    clearAll()
  }
})

