import assert from 'node:assert/strict'
import test from 'node:test'
import * as fission from '../../../dist/Execution/Fission/Surface.js'
import { deliveredValue, deliveredViewOf } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { anchors, applyView, clearAll } from '../../../dist/Repository/Programming/Js/Anchor/AnchorSurface.js'
import { acceptAuthorityRoot, openIncumbency, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

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
 * repository-programming surface. The takeover branch itself
 * (`FissionHost.observeCurrentTakeoverOutcome` → CompleteOwner) is not
 * reachable from this fixture: a lane's own turn never settles as a stable
 * TurnCompleted here, so no `FissionLaneMaterialized` fact and no takeover
 * prompt are ever produced. That remaining chain is exactly the open
 * `GAP-158` todo below ("actual lane and takeover continuation completes
 * before settlement").
 *
 * What this case therefore does and does not establish: it drives the real
 * admission and the real delivery path and asserts that the owner cell holds
 * only the owner's own delivery, with no lane anchor and no lane material.
 * It does **not** reach the takeover branch, so it is not a
 * failing-before/passing-after proof for the removal of the cross-session
 * staging in that branch: the removed call sits on a path this fixture never
 * enters. The dynamic verification of the takeover chain is still missing and
 * stays open with `GAP-158`.
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
            'class Js extends JsProgram { async run() { return await this.fork({ name: "Ada", charge: "fission owner cell" }, { 1: null }); } }',
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
            program: `class Js extends JsProgram { async run() { await this.deliver({ ${laneAnchor}: "LANE-MATERIAL-${index}" }); } }`,
          },
          { sessionID: laneSession, agent: 'engineer', callID: `call-${laneSession}-1`, messageID: `run-${laneSession}-1` },
        )
      }

      // The lanes really delivered: each lane's own cell holds its material.
      for (const [index, laneSession] of laneSessions.entries()) {
        const laneCell = deliveredValue(laneSession)
        assert.ok(laneCell, `lane ${index} must hold its own delivery`)
        assert.equal(
          Object.values(laneCell.thenMap ?? {}).includes(`LANE-MATERIAL-${index}`),
          true,
          `lane ${index} must hold its own material`,
        )
      }

      // The owner cell: still empty. No lane delivery was staged into it, and
      // no lane anchor declaration was forwarded into it.
      assert.equal(deliveredValue(ownerSession), null, 'the owner delivery cell must be empty before the owner delivers')
      assert.equal(deliveredViewOf(ownerSession), null, 'the owner delivery cell must hold no view before the owner delivers')

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
          program: `class Js extends JsProgram { async run() { await this.deliver({ ${ownerAnchor}: "OWNER-MATERIAL" }); } }`,
        },
        { sessionID: ownerSession, agent: 'engineer', callID: `call-${ownerSession}-1`, messageID: `run-${ownerSession}-1` },
      )

      const ownerCell = deliveredValue(ownerSession)
      assert.ok(ownerCell, 'the owner cell must hold the owner delivery after the owner delivered')
      const ownerCellAnchors = Object.keys(ownerCell.thenMap ?? {})
      assert.equal(
        ownerCellAnchors.includes(String(ownerAnchor)),
        true,
        'the owner cell must hold the owner material under the owner anchor',
      )
      for (const laneAnchor of laneAnchors) {
        assert.equal(
          ownerCellAnchors.includes(String(laneAnchor)),
          false,
          `the owner cell must not carry the lane anchor declaration ${laneAnchor}`,
        )
      }
      assert.equal(
        Object.values(ownerCell.thenMap ?? {}).some((value) => String(value).startsWith('LANE-MATERIAL-')),
        false,
        'the owner cell must not carry a lane delivery as the owner delivery',
      )
    })
  } finally {
    clearAll()
  }
})

test.todo('WHAT[intra-participant-parallelism-014] actual lane and takeover continuation completes before settlement, including nudge, AABB and degeneration abort through their real owners (GAP-158)')
