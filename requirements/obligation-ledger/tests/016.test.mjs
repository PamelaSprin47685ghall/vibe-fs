import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");
const envelope = await import("../../../dist/Persistence/Journal/ObligationEnvelopeSurface.js");

const SESSION = 'ses_a'
const LIFE = 'life-1'
const lifecycle = (caseName) => ({
  caseName,
  payload: caseName === 'LifeOpened'
    ? {
        sessionId: SESSION,
        lifeId: LIFE,
        openingUserMessageId: 'msg-open-1',
        openingTextRef: 'blob-1',
        openingTextDigest: 'd-1',
        openingCursorSequence: 1,
      }
    : {
        sessionId: SESSION,
        lifeId: LIFE,
        activationPromptKey: 'key-1',
        protectedPrefixEndSequence: 42,
      },
})

test('WHAT[OBLIGATION-LEDGER-016] T1 revelation hook wraps the accepted result with entrustment', () => {
  const wrapped = todo.wrapT1AcceptedResult(SESSION, 'checkpoint body')
  assert.ok(wrapped.startsWith('# The account has been accepted.'))
  assert.ok(wrapped.includes('The Manager who will carry it is you.'))
  assert.ok(wrapped.includes('checkpoint body'))
})
}

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

test('WHAT[OBLIGATION-LEDGER-016] first accepted planComplete=false stays at the Planning Table without commitment', async (context) => {
  const opened = await openJournal()
  context.after(opened.close)
  const session = 'ses-magic-todo-planning-false'
  const life = 'life-magic-todo-planning-false'
  await openLife(opened.handle, session, life)
  await acceptPlanningFalseCheckpoint(opened.handle, session, life, 'call-magic-todo-planning-false')
  assert.equal(membrane.MagicTodoMembraneSurface_snapshot(opened.handle, life).firstPlanCommitment, null)
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

test('WHAT[OBLIGATION-LEDGER-016] projection latches the first true commitment and never reopens it', () => {
  const makeCheckpoint = (suffix, declared, baseName, proposalName, frontier) => {
    const cpCall = `todo-${suffix}`
    const cpWrite = todo.todoWriteId(sha256, life, cpCall)
    const cpPreparedRef = `prepared-${suffix}`
    const inputDigest = `provider-${suffix}`
    return {
      suffix,
      write: cpWrite,
      frontier,
      prepared: preparedFact({
        todoWriteId: cpWrite,
        toolCallId: cpCall,
        toolPartOrdinal: 1,
        baseTodoRef: baseName,
        baseTodoDigest: `${baseName}-digest`,
        proposedTodoRef: proposalName,
        proposedTodoDigest: `${proposalName}-digest`,
        planCompleteDeclared: declared,
        providerInputDigest: inputDigest,
        reviewFrontier: frontier,
      }),
      accepted: acceptedFact({
        todoWriteId: cpWrite,
        toolCallId: cpCall,
        preparedFactRef: cpPreparedRef,
        inputDigest,
        outputDigest: `output-${suffix}`,
      }),
      preparedRef: cpPreparedRef,
    }
  }

  const planning = makeCheckpoint('planning', false, 'base-0', 'plan-1', 10)
  const commitment = makeCheckpoint('commit', true, 'plan-1', 'mission-1', 20)
  const laterFalse = makeCheckpoint('later-false', false, 'mission-1', 'mission-2', 30)
  const handle = projection.MagicTodoProjectionSurface_create()

  ok(foldMagic(handle, planning.prepared, planning.preparedRef))
  ok(foldMagic(handle, planning.accepted))
  let lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.firstPlanCommitment, null)
  assert.equal(lifeState.latestCommittedCheckpoint, null)
  assert.equal(lifeState.firstAcceptedCheckpoint, planning.write)

  ok(foldMagic(handle, commitment.prepared, commitment.preparedRef))
  ok(foldMagic(handle, commitment.accepted))
  lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.firstPlanCommitment, commitment.write)
  assert.equal(lifeState.latestCommittedCheckpoint, commitment.write)
  assert.equal(lifeState.previousCommittedCheckpoint, null)

  ok(foldMagic(handle, laterFalse.prepared, laterFalse.preparedRef))
  ok(foldMagic(handle, laterFalse.accepted))
  lifeState = projection.MagicTodoProjectionSurface_view(handle, life)
  assert.equal(lifeState.firstPlanCommitment, commitment.write)
  assert.equal(lifeState.previousCommittedCheckpoint, commitment.write)
  assert.equal(lifeState.latestCommittedCheckpoint, laterFalse.write)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { default: test } = await import("node:test");

const root = new URL('../../../', import.meta.url).pathname
const read = (path) => readFileSync(join(root, path), 'utf8')
const firstCheckpointSurfaces = [
  ['planning-table/en', 'resources/provider/lifecycle/manager/planning-table/en.md'],
  ['planning-table/zh-CN', 'resources/provider/lifecycle/manager/planning-table/zh-CN.md'],
  ['todowrite-description/en', 'resources/provider/lifecycle/magic-todo/todowrite-description/en.md'],
  ['todowrite-description/zh-CN', 'resources/provider/lifecycle/magic-todo/todowrite-description/zh-CN.md'],
]

test('WHAT[OBLIGATION-LEDGER-016] planning checkpoints are allowed until the first irreversible true commitment', () => {
  for (const [label, path] of firstCheckpointSurfaces) {
    const text = read(path)
    assert.match(text, /planComplete/i, `${label}: must expose the explicit commitment declaration`)
    assert.match(text, /false/i, `${label}: must permit planning checkpoints before commitment`)
    assert.match(text, /true/i, `${label}: must explain the commitment declaration`)
    assert.match(text, /irreversible|不可逆|forever|永久|cannot.*return|不能.*回退/i, `${label}: first accepted true must be one-way`)
    assert.match(text, /planning|计划|规划/i, `${label}: false checkpoints must be allowed to carry planning work`)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");
const todo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");
const opening = await import("../../../dist/Mission/WorkRecord/OpeningSemanticSurface.js");
const traceOwner = await import("../../../dist/Context/Trace/SemanticTraceSurface.js");

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const item = (sequence, role, part) => traceOwner.item({ sequence, role, part })

test('WHAT[OBLIGATION-LEDGER-016] T1 constitutive boundary is independent from the compression floor', () => {
  const callId = 't1-call'
  const parts = [
    { sequence: 1, kind: 'text', text: 'opening' },
    { sequence: 5, kind: 'tool_call', toolCallId: callId },
    { sequence: 6, kind: 'tool_result', toolCallId: callId },
    { sequence: 9, kind: 'text', text: 'later' },
  ]
  assert.equal(todo.blindPlanOpeningBoundary(1, 5, callId, parts), 7)
  assert.equal(todo.effectiveOpeningFloor(true, false, 1, null, null, 9, parts), 2)
  const floor = todo.effectiveOpeningFloor(true, true, 1, 5, callId, 9, parts)
  assert.equal(floor, 2)
  assert.equal(todo.bloggerEffectiveStart(7, floor), 7)
  assert.equal(todo.bloggerEffectiveStart(10, floor), 10)
})
test('WHAT[OBLIGATION-LEDGER-016] T1 constitutive body renders in Opening, not Recent', () => {
  const openingCharge = opening.opening('Ship the bridge.', [], '')
  const constitutive = [
    item(5, 'assistant', traceOwner.toolCallPart('t1', 'todowrite', '{"planComplete":true,"workingOn":"","obligations":[]}')),
    item(6, 'tool', traceOwner.toolResultPart('t1', 'The Manager who will carry it is you.')),
  ]
  const withT1 = opening.withConstitutive(openingCharge, traceOwner.render(traceOwner.forOpening(constitutive)))
  const trace = [
    item(1, 'user', traceOwner.textPart('Ship the bridge.')),
    ...constitutive,
    item(8, 'assistant', traceOwner.textPart('post-T1 labor')),
  ]

  const gap = traceOwner.render(traceOwner.forWorkRecord(traceOwner.sliceFrom({ sequence: 7 }, trace)))
  const rendered = opening.materialize(withT1, [], gap, true)
  assert.match(rendered, /^Opening\n/)
  assert.match(rendered, /Ship the bridge/)
  assert.match(rendered, /\[tool call\] todowrite/)
  assert.match(rendered, /The Manager who will carry it is you/)
  assert.match(rendered, /Recent work\n/)
  assert.match(rendered, /post-T1 labor/)
  const recentSection = rendered.slice(rendered.indexOf('Recent work'))
  assert.doesNotMatch(recentSection, /todowrite/)
  assert.doesNotMatch(recentSection, /The Manager who will carry it is you/)
})
test('WHAT[OBLIGATION-LEDGER-016] XTrace.forOpening keeps T1 tools; forWorkRecord drops them', () => {
  const items = [
    item(5, 'assistant', traceOwner.toolCallPart('t1', 'todowrite', '{}')),
    item(6, 'tool', traceOwner.toolResultPart('t1', 'entrusted')),
  ]
  assert.equal(traceOwner.forOpening(items).length, 2)
  assert.equal(traceOwner.forWorkRecord(items).length, 0)
})
}
