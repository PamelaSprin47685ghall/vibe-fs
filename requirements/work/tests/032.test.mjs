import assert from 'node:assert/strict'
import { existsSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import test from 'node:test'
import { isAllowed } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import * as completedTurn from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as eventStore from '../../../dist/Persistence/EventStore/Surface.js'
import { acceptAuthorityRoot, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

// 交付见证的观察口：已注册的 EventStore Surface 把 durable 事实读成普通 JSON
// （payload.Fact = ["Agent", [family, [case, payload]]]），不构造任何 F# union。
// 交付见证就是 Host 家族里那条 TaskDeliveryCommitted 事实。
const deliveryWitnesses = (commonDir) => {
  const eventsDir = join(commonDir, 'wanxiangshu', 'events')
  if (!existsSync(eventsDir)) return []
  const all = new Map()
  for (const file of readdirSync(eventsDir)) {
    const store = eventStore.create(commonDir, file.split('-')[0])
    for (const stream of eventStore.streams(store)) {
      let id = eventStore.head(store, stream)
      while (id) {
        const event = eventStore.read(store, id)
        if (!event) break
        all.set(id, event)
        id = event.parents?.[0]
      }
    }
    eventStore.dispose(store)
  }
  const witnesses = []
  for (const event of all.values()) {
    const fact = event?.payload?.Fact
    if (fact?.[1]?.[0] === 'Host' && fact[1][1][0] === 'TaskDeliveryCommitted') witnesses.push(fact)
  }
  return witnesses
}

// WHAT[work-032] keeps unrelated offices unchanged. Manager shares human Ask;
// it must not inherit Plan's DevOps consultation or draft capabilities. Two
// observable faces are asserted here: non-Plan roles never receive the
// Plan-only permission projection, and non-Plan empty stops keep requiring
// interaction repair (the full classification suite lives in 027.test.mjs
// under work-027). Manager's review tool is reached through the
// ReviewAssessment permission and the shared shell of work-038; the
// Plan-backend Review label itself never appears in a non-Plan projection.
// The Baton isolation trigger is gated by ownerRole = Plan inside the
// transform pipeline (PluginTransforms planSessionOf); its live proof
// belongs to the physical host canary tracked by GAP-230, not to this pure
// surface.

test('WHAT[work-032] non-Plan roles never receive the Plan-only tool projection', () => {
  const planOnlyPermissions = ['JsPlan', 'DevOpsConsultation', 'Deliver', 'Review']
  const nonPlanRoles = ['manager', 'engineer', 'devops', 'orchestrator']
  // Each role label must resolve to a real role, so the deny below is a
  // genuine projection fact and not an unknown-role fallback.
  const roleWitness = {
    manager: 'Fork',
    engineer: 'Write',
    devops: 'Exec',
    orchestrator: 'Fork',
  }
  for (const role of nonPlanRoles) {
    assert.equal(isAllowed(role, roleWitness[role]), true, `${role} must resolve as a real role`)
    for (const perm of planOnlyPermissions) {
      assert.equal(isAllowed(role, perm), false, `${role} must be denied ${perm}`)
    }
  }
})

test('WHAT[work-032] non-Plan empty stops keep requiring interaction repair', () => {
  // There is no Plan empty-stop exemption to propagate to another role.
  assert.equal(completedTurn.needsInteractionRepair('engineer', true, 'stop', []), true)
  assert.equal(completedTurn.needsInteractionRepair('manager', true, 'stop', []), true)
})

test('WHAT[work-032] only Plan may ask the human; Manager and other roles are denied ask', () => {
  assert.equal(isAllowed('plan', 'Ask'), true)
  for (const role of ['manager', 'engineer', 'devops', 'orchestrator', 'coder', 'inspector']) {
    assert.equal(isAllowed(role, 'Ask'), false, role)
    assert.equal(isAllowed(role, 'DevOpsConsultation'), false, role)
  }
})

test('WHAT[work-032] Engineer, DevOps, and Orchestrator progress stops do not record completion fact without delivery witness', async () => {
  const { completeWithoutDelivery } = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  for (const name of ['engineer', 'devops', 'orchestrator']) {
    const outcome = await completeWithoutDelivery(name, `ses-${name}-progress`, `root-${name}-1`, `run-${name}-1`, 'Finished subtask progress summary.')
    assert.equal(outcome.caseName, 'RejectedUndelivered')
    assert.equal(outcome.terminalNotifications, 0)
  }

  for (const name of ['engineer', 'devops', 'orchestrator']) {
    for (const delivered of [false, true]) {
      await withExecutablePlugin(async (hooks, directory, created, runtime) => {
        const session = `w032-${name}-${delivered ? 'with' : 'without'}-witness`
        const physical = `phys-${session}`
        await acceptAuthorityRoot(runtime, session, name, physical)

        const user = {
          info: {
            id: physical,
            role: 'user',
            sessionID: session,
            agent: name,
            model: { providerID: 'provider', modelID: `${name}-model` },
            time: { created: 1 },
          },
          parts: [{ type: 'text', text: 'Do the work.' }],
        }
        runtime.pushHostMessage(session, user)
        await hooks['chat.message'](
          { sessionID: session, messageID: physical, agent: name },
          { message: user, parts: user.parts },
        )

        const run = `run-${session}`
        const program = delivered
          ? 'class Js extends JsProgram { async run() { await this.deliver({ 0: "the delivered view" }); } }'
          : 'class Js extends JsProgram { async run() { return 1; } }'
        const assistant = {
          info: {
            id: run,
            role: 'assistant',
            sessionID: session,
            parentID: physical,
            agent: name,
            providerID: 'provider',
            modelID: `${name}-model`,
            time: { created: 2 },
          },
          parts: [
            { type: 'text', text: `${name} progress report: no deliver called.` },
            { type: 'tool', tool: `todowrite-for-${name}`, callID: `call-${session}`, state: { status: 'pending', input: { program } } },
          ],
        }
        runtime.pushHostMessage(session, assistant)
        await hooks['experimental.chat.messages.transform']({ sessionID: session }, { messages: [user] })

        const before = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
        assert.ok(before !== null, `${name} logical run must start active`)

        const result = await hooks.tool[`todowrite-for-${name}`].execute(
          { program },
          { sessionID: session, agent: name, callID: `call-${session}`, messageID: run },
        )
        assert.match(String(result), /# ok/)
        assistant.parts.at(-1).state = { status: 'completed', input: { program }, output: String(result) }
        assistant.info.time.completed = 3
        assistant.info.finish = 'stop'
        await hooks.event({ event: { type: 'message.updated', properties: { info: assistant.info } } })
        await hooks.event({ event: { type: 'session.idle', properties: { sessionID: session } } })

        const witnesses = deliveryWitnesses(join(directory, '.Herdchestra'))
        if (delivered) {
          assert.equal(witnesses.length, 1, `${name} a real delivery writes exactly one witness`)
        } else {
          assert.deepEqual(
            witnesses,
            [],
            `${name} an undelivered progress stop writes no delivery witness, so no completion fact is recorded`,
          )
        }

        const after = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
        assert.deepEqual(after, before, `${name} logical run must not close without a delivery witness`)
      })
    }
  }
})

test('WHAT[work-032] Advisor keeps its own terminal protocol without an outer TaskDelivery', async () => {
  const { completeWithoutDelivery } = await import('../../../dist/Context/Trace/SemanticTraceSurface.js')
  const outcome = await completeWithoutDelivery('advisor', 'ses-advisor', 'own-root', 'own-run', 'The dedicated role produced its formal result.')
  assert.equal(outcome.caseName, 'Published')
  assert.equal(outcome.terminalNotifications, 1)
})
