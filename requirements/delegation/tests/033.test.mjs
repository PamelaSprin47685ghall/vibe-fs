import assert from 'node:assert/strict'
import test from 'node:test'
import * as eventsSurface from '../../../dist/OpenCode/Host/EventsSurface.js'
import * as runLifecycle from '../../../dist/Execution/Delegation/Fork/Host/RunLifecycle.js'
import { createJoinProbe, joinProbeDeliver, validateDeliveredMaterial } from '../../../dist/Execution/Delegation/Fork/JoinSurface.js'
import * as identity from '../../../dist/Foundation/Identity.js'
import * as deliveryJournal from '../../../dist/Composition/Durable/DelegationJournalAdapter.js'
import { create as createWriter } from '../../../dist/OpenCode/Host/DeliveryWitnessWriter.js'
import {
  DeliverySettlement,
  DeliveryTaskReference,
  JsToolWorkflow_executeWithReviewCheckAndDelivery,
} from '../../../dist/Repository/Programming/Js/ToolWorkflow.js'
import { typedRole } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { clearDeliveredValue, deliveredValue } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { JournalHandle__get_Journal } from '../../../dist/Persistence/Journal/Surface.js'
import { mkdtempSync, rmSync } from 'node:fs'
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

test('WHAT[delegation-033] consuming a delivery before the lazy read still settles a journal-less run', { timeout: 3000 }, async () => {
  const result = await joinProbeDeliver(createJoinProbe(), ['selected evidence'], 'current report')
  assert.deepEqual(result, {
    status: 'completed',
    returned: [{ text: 'selected evidence' }, { text: 'current report' }],
  })
})

test('WHAT[delegation-033] the committed delivery settles the delivering run before the cut', async () => {
  await withExecutablePlugin(async (_hooks, _directory, _createdIds, runtime) => {
    const owner = 'ses-owner-033'
    const child = 'ses-child-033'
    const physical = 'msg-033-root'
    const run = 'run-033'
    const call = 'call-033'
    const journal = JournalHandle__get_Journal(runtime.journal)

    await bindManagedChild(runtime, owner, child, 'engineer', 'h-033')

    const committed = await createWriter(journal, () => {}).CommitDeliveryWitness({
      SessionId: identity.SessionIdModule_create(child),
      LogicalRunId: identity.LogicalRunIdModule_create('logical-033'),
      AuthorityRoot: identity.AuthorityRootUserMessageIdModule_create(physical),
      PhysicalUserMessageId: identity.PhysicalUserMessageIdModule_create(physical),
      ProviderRun: identity.ProviderRunIdentityModule_create(run),
      ToolCallId: identity.ToolCallIdModule_create(call),
      Task: new DeliveryTaskReference(0, ['h-033']),
      FormalText: prose,
      ViewDeclaration: '["DELIVERED MATERIAL"]',
      View: JSON.stringify([material, prose]),
      RetirementId: undefined,
    })
    assert.equal(committed.tag, 0)

    // The witness is the only evidence the return stands on.
    const witness = deliveryJournal.TaskDeliveryJournal_tryFindByFenceFields(
      journal,
      identity.SessionIdModule_create(child),
      identity.ProviderRunIdentityModule_create(run),
      identity.ToolCallIdModule_create(call),
      undefined,
      undefined,
      undefined,
      undefined,
    )
    assert.ok(witness, 'the delivery committed a durable witness')
    assert.match(witness.View, /DELIVERED MATERIAL/)

    const port = await runtime.terminalPort
    const seen = []
    const subscription = eventsSurface.subscribe(port, (sessionId, outcome) => seen.push({ sessionId, outcome }))
    try {
      runLifecycle.publishDeliveredReturn(
        journal,
        port,
        identity.SessionIdModule_create(child),
        identity.ProviderRunIdentityModule_create(run),
        identity.ToolCallIdModule_create(call),
      )

      const completed = seen.find(
        (entry) => entry.sessionId === child && entry.outcome?.kind === 'Completed' && entry.outcome?.providerRun === run,
      )
      assert.ok(completed, 'the delivering run publishes its completed terminal before the cut')
      assert.equal(completed.outcome.text, prose, 'terminal prose never contains encoded or rendered delivery material')

      // A fence with no matching witness settles nothing: the return is only
      // what the committed delivery proves.
      const before = seen.length
      runLifecycle.publishDeliveredReturn(
        journal,
        port,
        identity.SessionIdModule_create(child),
        identity.ProviderRunIdentityModule_create(run),
        identity.ToolCallIdModule_create('call-other'),
      )
      assert.equal(seen.length, before, 'an unmatched fence publishes nothing')
    } finally {
      eventsSurface.dispose(subscription)
    }
  })
})

// A durable host that cannot attribute the delivering call has no witness to
// carry the view to the owner's join. The delivery must then fail visibly
// instead of leaving a process-live cell that no join can ever receive.
test('WHAT[delegation-033] an unattributed durable delivery is refused, not left joinable-only in memory', async () => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-033-unattributed-'))
  const sessionId = 'sess-033-unattributed'
  try {
    clearDeliveredValue(sessionId)
    const surface = typedRole('engineer', 'en')
    const outcome = await JsToolWorkflow_executeWithReviewCheckAndDelivery(
      undefined,
      new DeliverySettlement(1, ['the delivering assistant message carries no prose']),
      sessionId,
      [
        identity.ProviderRunIdentityModule_create('run-033-unattributed'),
        identity.ToolCallIdModule_create('call-033-unattributed'),
      ],
      surface.Capabilities,
      root,
      surface.BaseClassSource,
      {
        name: 'unattributed-delivery',
        program:
          'class Js extends JsProgram { async run() { await this.deliver(["UNATTRIBUTED MATERIAL"]); return { done: true }; } }',
      },
      3000,
      Date.now() + 60_000,
      1 << 20,
      undefined,
      undefined,
      undefined,
    )

    assert.equal(outcome.tag, 1, 'the call fails')
    assert.equal(outcome.fields[0].tag, 51, 'the failure is DELIVERY_NOT_ESTABLISHED')
    assert.equal(deliveredValue(sessionId), null, 'no process-live cell pretends the delivery happened')
  } finally {
    clearDeliveredValue(sessionId)
    rmSync(root, { recursive: true, force: true })
  }
})

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
      },
      { sessionID: session, agent: 'engineer', callID: call, messageID: run },
    )
    assert.match(String(result), /ok/i)

    const journal = JournalHandle__get_Journal(runtime.journal)
    const witness = deliveryJournal.TaskDeliveryJournal_tryFindByFenceFields(
      journal,
      identity.SessionIdModule_create(session),
      identity.ProviderRunIdentityModule_create(run),
      identity.ToolCallIdModule_create(call),
      undefined,
      undefined,
      undefined,
      undefined,
    )
    assert.ok(witness, 'the turn prose attributes the delivery witness')
    assert.equal(witness.FormalText, 'THE TURN PROSE')
    assert.deepEqual(JSON.parse(witness.View), ['THE TURN PROSE'])
  })
})

// AXIOM 6 (wanxiangli): the system never judges content. A form-compliant
// delivery is established even when its projection selects nothing; the
// witness carries an empty view and the caller still receives an empty view.
test('WHAT[delegation-033] an empty delivered view is still an established delivery', async () => {
  const { DeliveryAttribution } = await import('../../../dist/Repository/Programming/Js/ToolWorkflow.js')
  const root = mkdtempSync(join(tmpdir(), 'wxs-033-empty-'))
  const sessionId = 'sess-033-empty'
  try {
    clearDeliveredValue(sessionId)
    const surface = typedRole('engineer', 'en')
    const witnessed = []
    const attribution = new DeliveryAttribution(
      identity.LogicalRunIdModule_create('logical-033-empty'),
      identity.AuthorityRootUserMessageIdModule_create('msg-033-empty-root'),
      identity.PhysicalUserMessageIdModule_create('msg-033-empty-root'),
      new DeliveryTaskReference(1, ['call-033-empty']),
      undefined,
      '',
    )
    const writer = {
      CommitDeliveryWitness: async (witness) => {
        witnessed.push(witness)
        return { tag: 0, fields: [] }
      },
    }

    const outcome = await JsToolWorkflow_executeWithReviewCheckAndDelivery(
      undefined,
      new DeliverySettlement(0, [attribution, writer]),
      sessionId,
      [identity.ProviderRunIdentityModule_create('run-033-empty'), identity.ToolCallIdModule_create('call-033-empty')],
      surface.Capabilities,
      root,
      surface.BaseClassSource,
      { name: 'empty-delivery', program: 'class Js extends JsProgram { async run() { await this.deliver(); return { done: true }; } }' },
      3000,
      Date.now() + 60_000,
      1 << 20,
      undefined,
      undefined,
      undefined,
    )

    assert.equal(outcome.tag, 0, 'the delivery form is accepted')
    assert.equal(witnessed.length, 1, 'the witness is committed once')
    assert.equal(witnessed[0].View, '[]', 'the empty projection is canonical returned material')
    assert.equal(witnessed[0].FormalText, '', 'no prose is invented')
    assert.equal(witnessed[0].ViewDeclaration, '[]', 'the carry array is captured')
    assert.deepEqual(
      validateDeliveredMaterial(witnessed[0].View),
      { accepted: true, reason: '' },
      'the durable owner accepts the material produced by delivery',
    )
    const rejected = validateDeliveredMaterial('pre-rendered old material')
    assert.equal(rejected.accepted, false, 'the durable owner rejects old material before presentation')
    assert.match(rejected.reason, /canonical JSON string array/, 'rejection concerns the material format, not its digest')
  } finally {
    clearDeliveredValue(sessionId)
    rmSync(root, { recursive: true, force: true })
  }
})

