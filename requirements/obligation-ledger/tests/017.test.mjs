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

test('WHAT[OBLIGATION-LEDGER-017] WorkActivated is an inert legacy fact: it fixes ProtectedPrefixEnd once but never re-decides work eligibility', () => {
  const once = envelope.foldLifecycleSequence(SESSION, [lifecycle('LifeOpened'), lifecycle('WorkActivated')])
  assert.equal(once.ok, true, JSON.stringify(once.error))
  assert.equal(once.protectedPrefixEnd, 42)

  const replay = envelope.foldLifecycleSequence(SESSION, [
    lifecycle('LifeOpened'),
    lifecycle('WorkActivated'),
    lifecycle('WorkActivated'),
  ])
  assert.equal(replay.ok, true, JSON.stringify(replay.error))
  assert.equal(replay.protectedPrefixEnd, 42)
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

test('WHAT[OBLIGATION-LEDGER-017] zero-work planComplete=true with empty obligations is a valid T1 commitment', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-zero-work'
    const life = 'life-magic-todo-zero-work'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-magic-todo-zero-work', [], true)
    const prepared = assertOk(t1.result)
    const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('zero-work-t1-physical-output'))
    assert.equal(accepted.ok, true)
    assert.ok(membrane.MagicTodoMembraneSurface_snapshot(handle, life).firstPlanCommitment)
    assert.match(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
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

test('WHAT[OBLIGATION-LEDGER-017] Pre-T1 BlindPlan does not enlarge the structural Opening floor', () => {
  const floor = todo.effectiveOpeningFloor(true, false, 1, null, null, 7, [
    { sequence: 1, kind: 'text', text: 'opening' },
    { sequence: 7, kind: 'text', text: 'head' },
  ])
  assert.equal(floor, 2)
  assert.equal(todo.bloggerEffectiveStart(3, floor), 3)
})
test('WHAT[OBLIGATION-LEDGER-017] Pre-T1: no CurrentLife → no floor', () => {
  assert.equal(todo.effectiveOpeningFloor(false, false, 1, null, null, 4, []), null)
})
test('WHAT[OBLIGATION-LEDGER-017] static: BloggerCoordinator + CompanionTransform zero ProtectedPrefixEnd refs', () => {
  for (const rel of [
    'src/Wanxiangshu/Context/Companion/Blogger/Runtime/Coordinator.fs',
    'src/Wanxiangshu/Context/Companion/Transform.fs',
  ]) {
    const src = readFileSync(join(ROOT, rel), 'utf8')
    assert.equal(src.includes('ProtectedPrefixEnd'), false, `${rel} must not reference ProtectedPrefixEnd`)
  }
})
}
