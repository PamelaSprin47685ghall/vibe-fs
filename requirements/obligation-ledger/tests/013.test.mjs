import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const todoJournal = await import("../../../dist/Persistence/Journal/ObligationJournalSurface.js");
const host = await import("../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js");
const membrane = await import("../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js");
const locality = await import("../../../dist/Mission/Obligation/Todo/MagicTodoLocalitySurface.js");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')
const openJournal = async (runtime = 'rt_magic_todo_membrane') => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-obligation-membrane-'))
  const boot = await journal.JournalSurface_boot(directory, runtime, 4242, '2026-08-11T00:00:00Z')
  assert.equal(boot.ok, true, boot.ok ? '' : boot.error)
  return {
    handle: boot.journal,
    close: () => {
      journal.JournalSurface_dispose(boot.journal)
      rmSync(directory, { recursive: true, force: true })
    },
  }
}
const withJournal = async (body, runtime = 'rt_magic_todo_membrane') => {
  const opened = await openJournal(runtime)
  try {
    return await body(opened.handle)
  } finally {
    opened.close()
  }
}
const openLife = async (handle, session, life) => {
  const result = await membrane.MagicTodoMembraneSurface_openLife(handle, session, life)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
}
const prepare = (handle, session, call, obligations, planComplete = true, state = 0) => {
  const args = { planComplete, workingOn: obligations[0]?.name ?? '', obligations }
  const canonical = host.canonicalInput(args)
  const digest = host.canonicalInputDigest(sha256Hex, args)
  return membrane.MagicTodoMembraneSurface_prepare(handle, session, call, canonical, digest, planComplete, obligations, state)
    .then((result) => ({ result, digest, args, canonical }))
}
const accept = async (handle, prepared, inputDigest, outputDigest) =>
  membrane.MagicTodoMembraneSurface_accept(handle, prepared, 'LiveAfterSuccess', inputDigest, outputDigest)
const assertOk = (result, message = '') => {
  assert.equal(result.ok, true, message || (result.ok ? '' : JSON.stringify(result.error)))
  return result.value
}
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const append = async (handle, session, caseName, payload) => {
  const result = await todoJournal.appendMagicTodo(handle, session, null, fact(caseName, payload))
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  return result
}
const acceptPlanningFalseCheckpoint = async (handle, session, life, callText) => {
  const planning = await prepare(handle, session, callText, [
    { name: 'inspect-startup', work: 'Inspect startup paths so the implementation plan can be completed.' },
  ], false)
  const prepared = assertOk(planning.result)
  const accepted = await accept(handle, prepared.bridge, planning.digest, sha256Hex('planning-false-physical-output'))
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return { planning, accepted }
}
const acceptT1Checkpoint = async (handle, session, callText) => {
  const t1 = await prepare(handle, session, callText, [{ name: 'diagnose', work: 'Establish why the first todowrite succeeds.' }])
  const prepared = assertOk(t1.result)
  const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('t1-physical-output'))
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return { t1, accepted }
}

test('WHAT[OBLIGATION-LEDGER-013] T2 prepare after T1 succeeds immediately without process review wait', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-lag1'
    const life = 'life-magic-todo-t1-t2-lag1'
    await openLife(handle, session, life)
    await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    const t2 = await prepare(handle, session, 'call-magic-todo-t2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    assert.equal(t2.result.ok, true)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const projection = await import("../../../dist/Mission/Obligation/Todo/MagicTodoProjectionSurface.js");
const codec = await import("../../../dist/Mission/Obligation/Todo/MagicTodoProjectionCodecSurface.js");
const envelope = await import("../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const sha256 = (value) => `digest:${value}`
const life = 'manager-life'
const managerSession = 'manager-session'
const call = 'todo-call'
const write = todo.todoWriteId(sha256, life, call)
const cursor = (sequence) => ({ Sequence: sequence })
const fact = (caseName, payload) => JSON.stringify({ case: caseName, ...payload })
const ok = (result) => {
  assert.equal(result.ok, true, result.ok ? '' : JSON.stringify(result.error))
  return result
}
const error = (result) => {
  assert.equal(result.ok, false, 'expected projection rejection')
  return result.error
}
let nextEvent = 0
const foldMagic = (handle, magicFact, eventId = undefined) => {
  nextEvent += 1
  return projection.MagicTodoProjectionSurface_fold(handle, eventId ?? `magic-todo-${nextEvent}`, magicFact)
}
const preparedFact = ({
  managerSessionId = managerSession,
  managerLifeId = life,
  incumbencyId = life,
  todoWriteId = write,
  toolCallId = call,
  toolPartOrdinal = 2,
  baseTodoRef = 'base-list',
  baseTodoDigest = 'base-digest',
  proposedTodoRef = 'proposal-list',
  proposedTodoDigest = 'proposal-digest',
  planCompleteDeclared = false,
  providerInputDigest = 'provider-input-digest',
  reviewFrontier = 10,
  semanticVersion = 'magic-v1',
} = {}) => fact('TodoWritePrepared', {
  ManagerSessionId: managerSessionId,
  IncumbencyId: incumbencyId,
  TodoWriteId: todoWriteId,
  ToolCallId: toolCallId,
  ToolPartOrdinal: toolPartOrdinal,
  BaseTodoRef: baseTodoRef,
  BaseTodoDigest: baseTodoDigest,
  ProposedTodoRef: proposedTodoRef,
  ProposedTodoDigest: proposedTodoDigest,
  PlanCompleteDeclared: planCompleteDeclared,
  ProviderInputDigest: providerInputDigest,
  ReviewFrontier: cursor(reviewFrontier),
  SemanticVersion: semanticVersion,
})
const acceptedFact = ({
  managerLifeId = life,
  incumbencyId = life,
  todoWriteId = write,
  toolCallId = call,
  preparedFactRef = 'prepared-fact-ref',
  inputDigest = 'provider-input-digest',
  outputDigest = 'output-digest',
  physicalSuccessEvidence = 'LiveAfterSuccess',
  semanticVersion = 'magic-v1',
} = {}) => fact('TodoWriteAccepted', {
  IncumbencyId: incumbencyId,
  TodoWriteId: todoWriteId,
  ToolCallId: toolCallId,
  PreparedFactRef: preparedFactRef,
  InputDigest: inputDigest,
  OutputDigest: outputDigest,
  PhysicalSuccessEvidence: physicalSuccessEvidence,
  SemanticVersion: semanticVersion,
})
const prepared = preparedFact()
const accepted = acceptedFact()
const acceptedState = () => {
  const handle = projection.MagicTodoProjectionSurface_create()
  ok(foldMagic(handle, prepared, 'prepared-fact-ref'))
  ok(foldMagic(handle, accepted))
  return handle
}

test('WHAT[OBLIGATION-LEDGER-013] successive checkpoints can be prepared and accepted without review blockage', () => {
  const nextWrite = todo.todoWriteId(sha256, life, 'todo-call-2')
  const handle = acceptedState()
  const nextPrepared = preparedFact({
    todoWriteId: nextWrite,
    toolCallId: 'todo-call-2',
    toolPartOrdinal: 1,
    baseTodoRef: 'proposal-list',
    baseTodoDigest: 'proposal-digest',
    proposedTodoRef: 'next-proposal-list',
    proposedTodoDigest: 'next-proposal-digest',
    planCompleteDeclared: true,
    providerInputDigest: 'next-provider-input-digest',
    reviewFrontier: 12,
  })
  ok(foldMagic(handle, nextPrepared, 'next-prepared'))
  const nextAccepted = acceptedFact({
    todoWriteId: nextWrite,
    toolCallId: 'todo-call-2',
    preparedFactRef: 'next-prepared',
    inputDigest: 'next-provider-input-digest',
    outputDigest: 'next-output-digest',
  })
  ok(foldMagic(handle, nextAccepted))
  const lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.currentObligations.reference, 'next-proposal-list')
  assert.equal(lifeState.checkpoints.length, 2)
})
}
