import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const pluginHooks = await import("../../../dist/OpenCode/Host/PluginHooksSurface.js");
const dispatch = await import("../../../dist/Interaction/Dispatch/DispatchSurface.js");
const recovery = await import("../../../dist/Interaction/Dispatch/RecoverySurface.js");
const todoHost = await import("../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js");
const todoMembrane = await import("../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");

const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')
const ownerRootSelection = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager',
    role: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}
const acceptOwnerLogicalRun = async (handle, ownerSession) => {
  const accepted = await dispatch.acceptHumanRootSelection(
    handle,
    ownerSession,
    `msg-root-${ownerSession}`,
    ownerRootSelection,
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  assert.ok(
    dispatch.projectionObservation(handle, ownerSession).activeLogicalRun,
    'the owner must hold an active durable Logical Run before its Companion leaf is dispatched',
  )
}
const withJournal = async (prefix, writer, runtime, body) => {
  const directory = mkdtempSync(join(tmpdir(), prefix))
  const opened = await journal.JournalSurface_bootWithWriterId(directory, writer, runtime, 4242, '2026-08-30T00:00:00Z')
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))
  try {
    return await body(opened.journal)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

test('WHAT[EFFECT-ACCOUNTING-011] Adapter Todo Host Before executor After commits exact checkpoint and recovery does not repeat mutation', async () => {
  await withJournal('wxs-todo-host-adapter-', 'writer-todo-host', 'rt-todo-host', async (handle) => {
    const session = 'ses-todo-effect-011'
    const life = 'life-todo-effect-011'
    const call = 'call-todo-effect-011'
    const args = {
      planComplete: true,
      workingOn: 'verify-adapter',
      obligations: [{
        name: 'verify-adapter',
        horizon: 'near',
        work: 'Prove the physical Todo Host workflow reaches durable acceptance.',
      }],
    }
    const canonicalInput = todoHost.canonicalInput(args)
    const inputDigest = todoHost.canonicalInputDigest(sha256Hex, args)
    const messages = [
      {
        info: { id: 'msg-opening', role: 'user' },
        parts: [{ type: 'text', text: 'Open the durable Todo workflow.' }],
      },
      {
      info: { id: 'msg-provider-todo-011', role: 'assistant' },
      parts: [{
        type: 'tool',
        id: 'part-todo-011',
        callID: call,
        tool: 'todowrite',
        state: { status: 'pending', input: args },
      }],
    }]

    const openedLife = await todoMembrane.MagicTodoMembraneSurface_openLife(handle, session, life)
    assert.equal(openedLife.ok, true, openedLife.ok ? '' : openedLife.error)

    let executorCalls = 0
    const physicalOutput = 'physical-todo-success-011'
    const execution = await todoMembrane.MagicTodoMembraneSurface_executeHostSuccess(
      handle,
      messages,
      session,
      life,
      call,
      args,
      (compatibilityArgs) => {
        executorCalls += 1
        assert.equal(compatibilityArgs.todos.length, 1, 'real Before must adapt provider obligations for the builtin Host executor')
        return { title: '1 todos', output: physicalOutput, metadata: { todos: compatibilityArgs.todos } }
      },
    )

    assert.equal(executorCalls, 1)
    assert.equal(execution.life.checkpoints.length, 1)
    const checkpoint = execution.life.checkpoints[0]
    assert.deepEqual({
      toolCallId: checkpoint.toolCallId,
      accepted: checkpoint.accepted,
      inputDigest: checkpoint.inputDigest,
      outputDigest: checkpoint.outputDigest,
    }, {
      toolCallId: call,
      accepted: true,
      inputDigest,
      outputDigest: sha256Hex(JSON.stringify(physicalOutput)),
    })

    const replay = await todoMembrane.MagicTodoMembraneSurface_prepare(
      handle,
      session,
      call,
      canonicalInput,
      inputDigest,
      true,
      args.obligations,
      1,
    )
    assert.equal(replay.ok, true, replay.ok ? '' : JSON.stringify(replay.error))
    assert.equal(replay.value.prepared.todoWriteId, checkpoint.todoWriteId)
    assert.equal(executorCalls, 1, 'journal recovery must not blindly invoke the physical builtin mutation again')
    assert.equal(todoMembrane.MagicTodoMembraneSurface_snapshot(handle, life).checkpoints.length, 1)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const projection = await import("../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const sha256 = (value) => `digest:${value}`
const life = 'effect-life-011'
const incumbency = 'effect-incumbency-011'
const managerSession = 'effect-session-011'
const call = 'effect-call-011'
const preparedFactRef = 'effect-prepared-fact-ref-011'
const ids = todo.todoWriteId(sha256, incumbency, call)
const write = ids.todoWriteId ?? ids
const sequence = (number) => ({ Sequence: number })
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result
}
const foldError = (result) => {
  assert.equal(result.ok, false, 'expected the projection to reject this fact')
  return result.error.code
}
let nextEvent = 0
const foldMagic = (handle, magicFact, eventId = undefined) => {
  nextEvent += 1
  return projection.MagicTodoProjectionSurface_fold(handle, eventId ?? `effect-todo-${nextEvent}`, magicFact)
}
const prepared = fact('TodoWritePrepared', {
  ManagerSessionId: managerSession,
  IncumbencyId: incumbency,
  TodoWriteId: write,
  ToolCallId: call,
  ToolPartOrdinal: 2,
  BaseTodoRef: 'effect-base-list-011',
  BaseTodoDigest: 'effect-base-digest-011',
  ProposedTodoRef: 'effect-proposal-list-011',
  ProposedTodoDigest: 'effect-proposal-digest-011',
  PlanCompleteDeclared: false,
  ProviderInputDigest: 'effect-provider-input-digest-011',
  ReviewFrontier: sequence(10),
  SemanticVersion: 'magic-v1',
})
const accepted = (preparedRef = preparedFactRef) => fact('TodoWriteAccepted', {
  IncumbencyId: incumbency,
  TodoWriteId: write,
  ToolCallId: call,
  PreparedFactRef: preparedRef,
  InputDigest: 'effect-provider-input-digest-011',
  OutputDigest: 'effect-output-digest-011',
  PhysicalSuccessEvidence: 'LiveAfterSuccess',
  SemanticVersion: 'magic-v1',
})

test('WHAT[EFFECT-ACCOUNTING-011] accepted_without_any_prepared_is_rejected', () => {
  // No TodoWritePrepared means Accept is rejected, never silently accepted.
  const handle = projection.MagicTodoProjectionSurface_create()
  assert.equal(foldError(foldMagic(handle, accepted())), 'PreparedMissingForAccept')
})
test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_another_prepared_envelope_is_identity_corruption', () => {
  // A Prepared exists, but Accepted names another envelope: identity corruption.
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, preparedFactRef))
  assert.equal(foldError(foldMagic(handle, accepted('different-prepared-fact-ref-011'))), 'IdentityCorruption')
})
test('WHAT[EFFECT-ACCOUNTING-011] accepted_naming_exact_prepared_switches_current_immediately', () => {
  // Naming the exact Prepared envelope succeeds and immediately switches Current
  // to the proposal list; a later review conclusion cannot roll it back.
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, preparedFactRef))
  ok(foldMagic(handle, accepted(preparedFactRef)))

  assert.deepEqual(projection.MagicTodoProjectionSurface_view(handle, incumbency).currentObligations, {
    reference: 'effect-proposal-list-011',
    digest: 'effect-proposal-digest-011',
  })
})
}
