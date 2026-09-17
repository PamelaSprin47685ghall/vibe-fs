import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { createHash } = await import("node:crypto");
const { mkdtempSync, rmSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const journal = await import("../../../dist/Persistence/Journal/Surface.js");
const host = await import("../../../dist/Mission/Obligation/Todo/OpenCode/MagicTodoHostSurface.js");
const membrane = await import("../../../dist/Mission/Obligation/Todo/MagicTodoMembraneSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const sha256Hex = (value) => createHash('sha256').update(value).digest('hex')
const openJournal = async (runtime = 'rt_magic_todo_after') => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-magic-todo-after-'))
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
const withJournal = async (body, runtime = 'rt_magic_todo_after') => {
  const opened = await openJournal(runtime)
  try {
    return await body(opened.handle)
  } finally {
    opened.close()
  }
}

test('WHAT[OBLIGATION-LEDGER-026] after hook accepts checkpoint durably and enriches T1 revelation', async () => {
  await withJournal(async (handle) => {
    const sessionId = 'ses-after-test'
    const incumbencyId = 'life-after-test'
    const callId = 'call-after-1'
    const obligations = [{ name: 'task-1', horizon: 'near', work: 'Do work' }]

    // Open life
    const openRes = await membrane.MagicTodoMembraneSurface_openLife(handle, sessionId, incumbencyId)
    assert.equal(openRes.ok, true)

    // Prepare checkpoint
    const args = { planComplete: true, workingOn: 'task-1', obligations }
    const canonical = host.canonicalInput(args)
    const digest = host.canonicalInputDigest(sha256Hex, args)
    const prep = await membrane.MagicTodoMembraneSurface_prepare(
      handle, sessionId, callId, canonical, digest, true, obligations, 0,
    )
    assert.equal(prep.ok, true, prep.ok ? '' : JSON.stringify(prep.error))

    // Accept checkpoint (after hook logic)
    const accepted = await membrane.MagicTodoMembraneSurface_accept(
      handle, prep.value.bridge, 'LiveAfterSuccess', digest, sha256Hex('physical-output-after-test'),
    )
    assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))

    // After hook verified:
    // 1. Snapshot has latched firstPlanCommitment
    const snap = membrane.MagicTodoMembraneSurface_snapshot(handle, incumbencyId)
    assert.equal(snap.firstPlanCommitment, prep.value.prepared.todoWriteId)
    // 2. T1 revelation enriched result
    assert.match(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { applyToolDefinitionHook, createMagicTodoContractHooks, decodeV1TodoWriteArgs, hostTrigger, runHostV1ToolExecutePath, sampleObligationTodoWriteAdvertisement, sampleObligationTodoWriteArgs, projectObligationsToV1TodoRows, v1TodoWriteToolSeed, V1_TODOWRITE_PARAMETERS } = await import("../../verification-system/tests/support/plugin-fixture.mjs");

const SESSION = 'ses_magic_todo_canary'
const CALL = 'call_magic_todo_1'

test('WHAT[OBLIGATION-LEDGER-026] after does not run when executor throws', async () => {
  const hooks = createMagicTodoContractHooks()
  let afterCalls = 0
  const after = async () => {
    afterCalls += 1
  }

  const observation = await runHostV1ToolExecutePath({
    toolID: 'todowrite',
    sessionID: SESSION,
    callID: CALL,
    args: {
      todos: [{ content: 'will-throw', status: 'pending', priority: 'high' }],
    },
    before: hooks['tool.execute.before'],
    after,
    execute: async () => {
      throw new Error('executor boom')
    },
  })

  assert.equal(observation.executeThrew, true, 'F: executor throw is observed')
  assert.equal(observation.executeError?.message, 'executor boom')
  assert.equal(observation.afterRan, false, 'F: FREEZE — after does not run when executor throws')
  assert.equal(afterCalls, 0, 'F: after hook body never invoked on throw')
})
test('WHAT[OBLIGATION-LEDGER-026] after runs when executor succeeds', async () => {
  // Control: same path with success must invoke after (proves the freeze is path-sensitive).
  const hooks = createMagicTodoContractHooks()
  let afterCalls = 0
  const after = async (_input, output) => {
    afterCalls += 1
    output.output = 'enriched'
  }

  const observation = await runHostV1ToolExecutePath({
    toolID: 'todowrite',
    sessionID: SESSION,
    callID: CALL,
    args: {
      todos: [{ content: 'ok', status: 'pending', priority: 'high' }],
    },
    before: hooks['tool.execute.before'],
    after,
    execute: async (params) => ({
      title: '1 todos',
      output: JSON.stringify(params.todos),
      metadata: { todos: params.todos },
    }),
  })

  assert.equal(observation.executeThrew, false)
  assert.equal(observation.afterRan, true, 'F control: after runs on executor success')
  assert.equal(afterCalls, 1)
  assert.equal(observation.afterOutput.output, 'enriched')
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

test('WHAT[OBLIGATION-LEDGER-026] accepted planComplete=false carries no T1 entrustment revelation (revelation is reserved for the first accepted true)', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-planning-false'
    const life = 'life-magic-todo-planning-false'
    await openLife(handle, session, life)
    const { accepted } = await acceptPlanningFalseCheckpoint(handle, session, life, 'call-magic-todo-planning-false')
    assert.doesNotMatch(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
})
test('WHAT[OBLIGATION-LEDGER-026] first accepted planComplete=true reveals entrustment in the enriched result', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-lag1'
    const life = 'life-magic-todo-t1-t2-lag1'
    await openLife(handle, session, life)
    const { accepted } = await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    assert.match(accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i)
  })
})
test('WHAT[OBLIGATION-LEDGER-026] enriched result for normal checkpoints contains epilogue instructions', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-magic-todo-t1-t2-resolve'
    const life = 'life-magic-todo-t1-t2-resolve'
    await openLife(handle, session, life)
    await acceptT1Checkpoint(handle, session, 'call-magic-todo-t1')
    const t2 = await prepare(handle, session, 'call-magic-todo-t2', [
      { name: 'diagnose', work: 'Establish why the first todowrite succeeds.' },
      { name: 'fix', work: 'Keep later todowrite calls from failing red.' },
    ])
    const t2Prepared = assertOk(t2.result)
    const t2Accepted = await accept(handle, t2Prepared.bridge, t2.digest, sha256Hex('t2-physical-output'))
    assert.equal(t2Accepted.ok, true)
    assert.match(t2Accepted.value.enrichedResult, /Keep working/)
    assert.doesNotMatch(t2Accepted.value.enrichedResult, /Manager who will carry it is you|The road is yours/i, 'T1 entrustment revelation happens exactly once')
  })
})
test('WHAT[OBLIGATION-LEDGER-026] prepare without open life is a structured rejection, never a provider red path', async () => {
  await withJournal(async (handle) => {
    // prepare without openLife → NoOpenManagerLife (infrastructure-level, not provider red)
    const obligations = [{ name: 'diagnose', work: 'Fix the todowrite snapshot race.' }]
    const args = { planComplete: false, workingOn: 'diagnose', obligations }
    const canonical = host.canonicalInput(args)
    const digest = host.canonicalInputDigest(sha256Hex, args)
    const result = await membrane.MagicTodoMembraneSurface_prepare(handle, 'ses-after-failclose', 'call-after-failclose', canonical, digest, false, obligations, 0)
    assert.equal(result.ok, false)
    assert.ok(result.error.code === 'NoActiveIncumbency' || result.error.code === 'NoOpenManagerLife')
  })
})
test('WHAT[OBLIGATION-LEDGER-026] host before/after hook rejects without active incumbency as typed tool rejection, not infrastructure fatal', async () => {
  await withJournal(async (handle) => {
    const session = 'ses-fresh-manager-todowrite'
    const call = 'call-fresh-manager-todowrite'
    const obligations = [{ name: 'diagnose', horizon: 'near', work: 'Fix the todowrite snapshot race.' }]
    const args = { planComplete: false, workingOn: 'diagnose', obligations }
    const messages = [
      {
        info: { id: 'msg-user-root-fresh', role: 'user' },
        parts: [{ type: 'text', text: 'You are off today.' }],
      },
      {
        info: { id: 'msg-asst-run-fresh', role: 'assistant' },
        parts: [{
          type: 'tool',
          id: 'part-fresh-todo',
          callID: call,
          tool: 'todowrite',
          state: { status: 'pending', input: args },
        }],
      },
    ]

    await assert.rejects(
      () => membrane.MagicTodoMembraneSurface_executeHostSuccess(
        handle,
        messages,
        session,
        'inc-any',
        call,
        args,
        () => ({ title: '1 todos', output: 'ok', metadata: {} }),
      ),
      /todowrite requires an active incumbency/,
    )
  })
})
}
