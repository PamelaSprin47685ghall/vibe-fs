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

test('WHAT[OBLIGATION-LEDGER-009] duplicate obligation name is the provider-red class', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-syntax-red'
    const life = 'life-syntax-red'
    await openLife(handle, session, life)
    const result = await prepare(handle, session, 'call-syntax-red', [
      { name: 'same', work: 'first' },
      { name: 'same', work: 'second' },
    ], false)
    assert.equal(result.result.ok, false)
    assert.equal(result.result.error.code, 'DuplicateObligationName')
  })
})
test('WHAT[OBLIGATION-LEDGER-009] prepare and accept succeed directly without review runtime', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-runtime-fatal'
    const life = 'life-runtime-fatal'
    await openLife(handle, session, life)
    const t1 = await prepare(handle, session, 'call-runtime-fatal', [
      { name: 'work', work: 'Do real mission work.' },
    ], true)
    const prepared = assertOk(t1.result)
    const accepted = await accept(handle, prepared.bridge, t1.digest, sha256Hex('runtime-fatal-output'))
    assert.equal(accepted.ok, true, 'prepare+accept must succeed directly')
  })
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

test('WHAT[OBLIGATION-LEDGER-009] failure triage keeps red for syntax and kills OpenCode on infrastructure faults', () => {
  const membrane = read('src/Wanxiangshu/Mission/Obligation/Todo/MagicTodoMembrane.fs')
  const hostCodec = read('src/Wanxiangshu/Mission/Obligation/Todo/OpenCode/HostCodec.fs')

  assert.match(membrane, /MagicTodoHostCodec\.decodeInputOrReject args/, 'schema decode is allowed to reject the tool call')
  // W5 cutover: schema reject → ProviderInputRejection (typed refusal, no
  // ownership); durable append fault → JournalAppendException; post-effect
  // digest mismatch → invalidOp (SettlementIncomplete).
  assert.match(membrane, /MagicTodoHostCodec\.ProviderInputRejection/, 'hook boundary refuses with typed rejection')
  assert.match(membrane, /JournalAppendException/, 'durable-refusal surface stays typed')
  assert.doesNotMatch(membrane, /Diagnostic\.fatal/, 'not routed through fatalInfrastructure any more')
  assert.match(hostCodec, /raise\s*\(\s*ProviderInputRejection/, 'HostCodec raises typed ProviderInputRejection for output.args missing')
})
}
