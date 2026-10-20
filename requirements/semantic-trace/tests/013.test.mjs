import assert from 'node:assert/strict'
import test from 'node:test'
import { createHash } from 'node:crypto'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import * as agentJournalModule from '../../../dist/Persistence/Journal/AgentJournal.js'
import * as delivery from '../../../dist/Composition/Durable/DelegationJournalAdapter.js'
import * as hostFactFold from '../../../dist/Composition/Durable/HostFactFold.js'
import * as facts from '../../../dist/Host/Facts.js'
import * as identity from '../../../dist/Foundation/Identity.js'
import * as terminalReporter from '../../../dist/Context/Trace/TerminalReporter.js'
import * as roles from '../../../dist/Foundation/Roles.js'
import * as message from '../../../dist/OpenCode/Host/Message.js'
import * as turnProgram from '../../../dist/Composition/Turn/Program.js'
import * as trace from '../../../dist/Context/Trace/SemanticTraceSurface.js'

const SESSION = 'ses_delivery_013'
const RUN = 'run_013_a'

const sha256Hex = (text) => createHash('sha256').update(text).digest('hex')
const isOk = (result) => result.tag === 0
const reasonOf = (result) => result.fields[0].Reason

// The durable journal is the only place a delivery witness lives; every read
// below goes through the real journal surface, never through a local model.
const withJournal = async (body) => {
  const dir = mkdtempSync(join(tmpdir(), 'semantic-trace-013-'))
  try {
    return await body(dir)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

const openJournal = async (dir) => {
  const created = await journalSurface.JournalSurface_boot(dir, 'rt_semantic_trace_013', 4242, '2026-01-01T00:00:00Z')
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  return created.journal
}

const journalOf = (handle) => journalSurface.JournalHandle__get_Journal(handle)
const deliveriesOf = (handle) => agentJournalModule.AgentJournalModule_snapshot(journalOf(handle)).AgentProjections.Deliveries

// A delivery fence: the (session, providerRun, toolCallId) triple plus the
// task / logical run / authority term / this call that make it its own witness.
const makeFence = (overrides = {}) =>
  new facts.TaskDeliveryFence(
    overrides.sessionId ?? new identity.SessionId(SESSION),
    overrides.logicalRunId ?? new identity.LogicalRunId('lrun_013_a'),
    overrides.authorityRoot ?? new identity.AuthorityRootUserMessageId('root_013_a'),
    overrides.physicalUserMessageId ?? new identity.PhysicalUserMessageId('msg_013_a'),
    overrides.providerRun ?? new identity.ProviderRunIdentity(RUN),
    overrides.toolCallId ?? new identity.ToolCallId('call_013_a'),
    overrides.task ?? new facts.TaskDeliveryTask(0, ['handle_013_a']),
  )

const makeWitness = (fence, material = {}) =>
  new facts.TaskDeliveryWitness(
    fence,
    material.formalText ?? 'formal delivery prose',
    material.viewDeclaration ?? 'view declaration text',
    material.view ?? 'materialized view body',
    sha256Hex(material.view ?? 'materialized view body'),
    material.retirementId ?? undefined,
  )

// The reconciled provider turn whose terminal outcome depends on the witness.
const makeTurn = (fence) => ({
  SessionId: fence.SessionId,
  PhysicalUserMessageId: fence.PhysicalUserMessageId,
  AuthorityRootUserMessageId: fence.AuthorityRoot,
  ProviderRun: fence.ProviderRun,
  Role: roles.Role.Engineer,
  Directory: undefined,
  Parts: [new message.MessagePart(0, ['the assistant prose of this turn'])],
  Finish: 'stop',
  ErrorName: undefined,
  Model: undefined,
  Outcome: turnProgram.TurnOutcome.TurnCompleted,
  Observation: undefined,
})

const recordingEventPort = (recorded) => ({
  NotifyTerminal: (_sessionId, outcome) => {
    recorded.push(outcome.name)
    return true
  },
})

test('WHAT[semantic-trace-013] a committed delivery witness reads back by its triple with prose, view declaration and materialized view intact, and survives a durable replay', async () => {
  await withJournal(async (dir) => {
    const fence = makeFence()
    const witness = makeWitness(fence)

    const handle = await openJournal(dir)
    try {
      const committed = await delivery.TaskDeliveryJournal_commit(journalOf(handle), witness)
      assert.equal(isOk(committed), true, JSON.stringify(committed.fields[0]))

      const read = delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), fence)
      assert.ok(read, 'the committed witness must be readable by its fence')
      assert.equal(read.FormalText, witness.FormalText)
      assert.equal(read.ViewDeclaration, witness.ViewDeclaration)
      assert.equal(read.View, witness.View)
      assert.equal(read.MaterialDigest, witness.MaterialDigest)

      const byTriple = delivery.TaskDeliveryJournal_tryFindByFenceFields(
        journalOf(handle),
        fence.SessionId,
        fence.ProviderRun,
        fence.ToolCallId,
        undefined,
        undefined,
      )
      assert.ok(byTriple, 'the committed witness must be readable by its (session, providerRun, toolCallId) triple')
      assert.equal(byTriple.View, witness.View)
      assert.equal(byTriple.ViewDeclaration, witness.ViewDeclaration)
    } finally {
      journalSurface.JournalSurface_dispose(handle)
    }

    // Replay: a fresh journal over the same durable directory re-derives the
    // same witness from the durable facts, not from any narration.
    const replayed = await openJournal(dir)
    try {
      const witness2 = delivery.TaskDeliveryJournal_tryFindByFence(journalOf(replayed), fence)
      assert.ok(witness2, 'the witness must be replayable from the durable journal')
      assert.equal(witness2.FormalText, witness.FormalText)
      assert.equal(witness2.ViewDeclaration, witness.ViewDeclaration)
      assert.equal(witness2.View, witness.View)
      assert.equal(witness2.MaterialDigest, witness.MaterialDigest)
    } finally {
      journalSurface.JournalSurface_dispose(replayed)
    }
  })
})

test('WHAT[semantic-trace-013] a different fence is a different witness: each stays readable and neither overwrites the other', async () => {
  await withJournal(async (dir) => {
    const first = makeFence()
    const second = makeFence({
      logicalRunId: new identity.LogicalRunId('lrun_013_b'),
      authorityRoot: new identity.AuthorityRootUserMessageId('root_013_b'),
      physicalUserMessageId: new identity.PhysicalUserMessageId('msg_013_b'),
      toolCallId: new identity.ToolCallId('call_013_b'),
      task: new facts.TaskDeliveryTask(0, ['handle_013_b']),
    })
    const firstWitness = makeWitness(first, { view: 'materialized view body of the first call' })
    const secondWitness = makeWitness(second, {
      formalText: 'formal delivery prose of the second call',
      view: 'materialized view body of the second call',
    })

    const handle = await openJournal(dir)
    try {
      assert.equal(isOk(await delivery.TaskDeliveryJournal_commit(journalOf(handle), firstWitness)), true)
      assert.equal(isOk(await delivery.TaskDeliveryJournal_commit(journalOf(handle), secondWitness)), true)

      assert.equal(deliveriesOf(handle).size, 2, 'two fences must yield two witnesses')

      const readFirst = delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), first)
      const readSecond = delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), second)
      assert.ok(readFirst)
      assert.ok(readSecond)
      assert.equal(readFirst.View, firstWitness.View)
      assert.equal(readSecond.View, secondWitness.View)
      assert.notEqual(readFirst.View, readSecond.View)

      // A fence that differs in any field is not the stored witness: reading it
      // must not hand back the other call's delivery.
      const otherTask = makeFence({ task: new facts.TaskDeliveryTask(0, ['handle_013_other']) })
      assert.equal(
        delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), otherTask),
        undefined,
        'a different fence must not resolve to the stored witness',
      )

      // ... and the stored witnesses are untouched by that lookup.
      assert.equal(delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), first).View, firstWitness.View)
      assert.equal(delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), second).View, secondWitness.View)
    } finally {
      journalSurface.JournalSurface_dispose(handle)
    }
  })
})

test('WHAT[semantic-trace-013] replaying the same witness under the same fence is idempotent: no second witness, no error', async () => {
  await withJournal(async (dir) => {
    const fence = makeFence()
    const witness = makeWitness(fence)

    const handle = await openJournal(dir)
    try {
      assert.equal(isOk(await delivery.TaskDeliveryJournal_commit(journalOf(handle), witness)), true)
      assert.equal(isOk(await delivery.TaskDeliveryJournal_commit(journalOf(handle), witness)), true)

      assert.equal(deliveriesOf(handle).size, 1, 'a replayed witness must not add a second one')

      const read = delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), fence)
      assert.ok(read)
      assert.equal(read.FormalText, witness.FormalText)
      assert.equal(read.ViewDeclaration, witness.ViewDeclaration)
      assert.equal(read.View, witness.View)
      assert.equal(read.MaterialDigest, witness.MaterialDigest)

      // The fold itself is idempotent for the same fence and the same material.
      const projection = agentJournalModule.AgentJournalModule_snapshot(journalOf(handle)).AgentProjections
      const folded = hostFactFold.fold(projection, new facts.HostFactCases(0, [witness]))
      assert.equal(isOk(folded), true, JSON.stringify(folded.fields[0]))
      assert.equal(folded.fields[0].Deliveries.size, 1)
    } finally {
      journalSurface.JournalSurface_dispose(handle)
    }
  })
})

test('WHAT[semantic-trace-013] conflicting material under the same fence is rejected and the original witness survives', async () => {
  await withJournal(async (dir) => {
    const fence = makeFence()
    const witness = makeWitness(fence)

    const handle = await openJournal(dir)
    try {
      assert.equal(isOk(await delivery.TaskDeliveryJournal_commit(journalOf(handle), witness)), true)

      // The journal's integrator folds every host fact through this fold, so
      // the rejection is asserted on that fold over the journal's own
      // projection: the same material conflict must be refused.
      const projection = agentJournalModule.AgentJournalModule_snapshot(journalOf(handle)).AgentProjections

      const proseConflict = makeWitness(fence, { formalText: 'a different formal delivery prose' })
      const rejectedProse = hostFactFold.fold(projection, new facts.HostFactCases(0, [proseConflict]))
      assert.equal(rejectedProse.tag, 1, 'a conflicting prose under the same fence must be rejected')
      assert.ok(reasonOf(rejectedProse).length > 0)

      const viewConflict = makeWitness(fence, { view: 'a different materialized view body' })
      const rejectedView = hostFactFold.fold(projection, new facts.HostFactCases(0, [viewConflict]))
      assert.equal(rejectedView.tag, 1, 'a conflicting materialized view under the same fence must be rejected')
      assert.ok(reasonOf(rejectedView).length > 0)

      // The refused material did not erase the witness that was already there.
      const read = delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), fence)
      assert.ok(read)
      assert.equal(read.FormalText, witness.FormalText)
      assert.equal(read.View, witness.View)
      assert.equal(read.MaterialDigest, witness.MaterialDigest)
      assert.equal(deliveriesOf(handle).size, 1)
    } finally {
      journalSurface.JournalSurface_dispose(handle)
    }
  })
})

test('WHAT[semantic-trace-013] prose, view declaration text and a provider stop are material, not a witness', async () => {
  await withJournal(async (dir) => {
    const fence = makeFence()
    const witness = makeWitness(fence)

    const handle = await openJournal(dir)
    try {
      // Material without a real delivery commit: no witness exists to read.
      assert.equal(delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), fence), undefined)
      assert.equal(
        delivery.TaskDeliveryJournal_tryFindByFenceFields(
          journalOf(handle),
          fence.SessionId,
          fence.ProviderRun,
          fence.ToolCallId,
          undefined,
          undefined,
        ),
        undefined,
      )
      assert.equal(delivery.TaskDeliveryJournal_tryFind(journalOf(handle), fence.SessionId, fence.ProviderRun), undefined)

      // A refused delivery commit leaves no witness behind either.
      const refused = await delivery.TaskDeliveryJournal_commit(
        journalOf(handle),
        makeWitness(fence, { formalText: '' }),
      )
      assert.equal(isOk(refused), false, 'a delivery without formal prose must be refused')
      assert.equal(delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), fence), undefined)

      // The provider stop is durable material, still not a witness.
      const captured = await trace.captureTerminalText(handle, SESSION, 'completed', RUN)
      assert.equal(captured.terminalCaptured, true)
      assert.equal(delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), fence), undefined)

      // Only the real delivery commit produces the witness.
      assert.equal(isOk(await delivery.TaskDeliveryJournal_commit(journalOf(handle), witness)), true)
      const read = delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), fence)
      assert.ok(read)
      assert.equal(read.FormalText, witness.FormalText)
      assert.equal(read.View, witness.View)
    } finally {
      journalSurface.JournalSurface_dispose(handle)
    }
  })
})

test('WHAT[semantic-trace-013] a missing witness is not back-filled by later narration and the terminal stays undelivered', async () => {
  await withJournal(async (dir) => {
    const fence = makeFence()

    const handle = await openJournal(dir)
    try {
      // The provider stops and its prose is durable material.
      const captured = await trace.captureTerminalText(handle, SESSION, 'completed', RUN)
      assert.equal(captured.terminalCaptured, true)

      // Narration does not create the witness, before or after the fact.
      assert.equal(delivery.TaskDeliveryJournal_tryFindByFence(journalOf(handle), fence), undefined)
      assert.equal(
        delivery.TaskDeliveryJournal_tryFindByFenceFields(
          journalOf(handle),
          fence.SessionId,
          fence.ProviderRun,
          fence.ToolCallId,
          undefined,
          undefined,
        ),
        undefined,
      )

      // And a terminal without a witness is not written as a completion.
      const recorded = []
      const completion = await terminalReporter.TerminalReporter_completeWithEvidence(
        recordingEventPort(recorded),
        undefined,
        makeTurn(fence),
        false,
      )
      assert.equal(completion.name, 'RejectedUndelivered')
      assert.deepEqual(recorded, [], 'a terminal without a delivery witness must not publish a completion')
    } finally {
      journalSurface.JournalSurface_dispose(handle)
    }
  })
})
