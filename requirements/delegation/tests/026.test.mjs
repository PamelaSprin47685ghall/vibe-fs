import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { mkdtempSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const fork = await import("../../../dist/Execution/Delegation/Fork/Surface.js");
const forkTool = await import("../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js");

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
  int: () => schemaNode(`${kind}-int`, extra),
  nonnegative: () => schemaNode(`${kind}-nonnegative`, extra),
})
const toolModule = {
  tool: {
    schema: {
      string: () => schemaNode('string'),
      number: () => schemaNode('number'),
      enum: (values) => schemaNode('enum', { values }),
      array: (inner) => schemaNode('array', { inner }),
    },
  },
}
const waitForPromptCount = (runtime, count) => forkTool.awaitPromptCount(runtime, count)
const ownerDescriptor = (sessionId) => [{ sessionId, agent: 'manager' }]

test('WHAT[DELEG-026] RESUME_synchronous_admission_with_async_work_and_join_isolation', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-resume-sync-admit-'))
  const owner = 'manager-resume-sync-admit'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    // 1. Initial fork
    const first = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'INITIAL-CHARGE')
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await first, /Ada/)
    assert.equal(await forkTool.settle(runtime, owner, 'FIRST-ANSWER', 'fork-run-1'), true)
    const firstJoined = await forkTool.executeJoin(runtime, owner)
    assert.match(firstJoined, /FIRST-ANSWER/)

    // 2. Resume dispatch without physical acceptance must not complete successfully or create ghost run
    forkTool.nextPromptAdmittedWithReceipt(runtime, 'accepted-receipt-only')
    const unconfirmedResume = await forkTool.executeManagerResume(runtime, toolModule, owner, '', 'Ada', 'UNCONFIRMED-CHARGE')
    assert.match(unconfirmedResume, /uncertain|不确定|may already have been accepted|可能已/i)
    assert.doesNotMatch(unconfirmedResume, /carries this charge now|现已接下这项托付/i)

    // 3. Join sees nothing to join because unconfirmed dispatch was not installed into pending runs
    const joinResult = await forkTool.executeJoin(runtime, owner)
    assert.match(joinResult, /NothingToJoin|无可等待|没有|nothing away to receive/i)

    // 4. Confirmed resume accepts and publishes assignment
    const second = forkTool.executeManagerResume(runtime, toolModule, owner, '', 'Ada', 'CONFIRMED-CHARGE')
    await waitForPromptCount(runtime, 3)
    assert.equal(forkTool.acceptPrompt(runtime, 1), true)
    assert.match(await second, /Ada/)
    assert.match(await second, /carries this charge now|现已接下这项托付/i)

    // 5. Completion with matching authority root settles the new assignment
    assert.equal(await forkTool.settle(runtime, owner, 'SECOND-ANSWER', 'fork-run-2'), true)
    const secondJoined = await forkTool.executeJoin(runtime, owner)
    assert.match(secondJoined, /SECOND-ANSWER/)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
test('WHAT[DELEG-026] FORK_TOOL_acceptance_unknown_never_claims_charge_was_not_placed', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-unknown-'))
  const owner = 'manager-unknown'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    forkTool.nextPromptAcceptanceUnknown(runtime, 'connection closed after request write')

    const result = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'engineer',
      'Ada',
      'UNCERTAIN-FORK-CHARGE',
    )

    assert.doesNotMatch(result, /could not be placed|无法托付/i)
    assert.match(result, /uncertain|不确定/i)
    assert.match(result, /may already have been accepted|可能已/i)
    assert.equal(forkTool.childCount(runtime), 1)
    assert.equal(forkTool.promptCount(runtime), 1, 'physical Host send was attempted exactly once')
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
test('WHAT[DELEG-026] FORK_TOOL_unconfirmed_dispatch_reports_uncertain_and_never_leaves_ghost_run_for_join', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-unconfirmed-'))
  const owner = 'manager-unconfirmed'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    forkTool.nextPromptAdmittedWithReceipt(runtime, 'accepted-without-message-id')

    const result = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'engineer',
      'Ada',
      'RECEIPT-PENDING-FORK-CHARGE',
    )

    // Unconfirmed dispatch (transport receipt without physical acceptance) must report uncertain,
    // never claim placement success (carries this charge now)
    assert.match(result, /uncertain|不确定|may already have been accepted|可能已/i)
    assert.doesNotMatch(result, /carries this charge now|现已接下这项托付/i)

    // Join must NOT hang waiting on an unconfirmed dispatch; it must report NothingToJoin / no active runs
    const joinResult = await forkTool.executeJoin(runtime, owner)
    assert.match(joinResult, /NothingToJoin|无可等待|没有|nothing away to receive/i)

    assert.equal(forkTool.promptCount(runtime), 1)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");


test('WHAT[DELEG-026] reusable delegation has no durable program-counter/state-machine vocabulary', () => {
  const handoff = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Handoff.fs', import.meta.url), 'utf8')
  const ledger = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/HandoffLedger.fs', import.meta.url), 'utf8')
  const facts = readFileSync(new URL('../../../src/Wanxiangshu/Execution/Delegation/Facts.fs', import.meta.url), 'utf8')

  for (const source of [handoff, ledger, facts]) {
    assert.doesNotMatch(source, /WorkUnitStarted|WorkUnitFinished|WorkUnitAbandoned|ActiveWorkUnit|CurrentStage|NextAction/)
  }
  assert.doesNotMatch(ledger, /advanceHandoff|DelegateSessionId/)
  assert.match(ledger, /DelegationHandoffCompleted/)
})
test('WHAT[DELEG-026] fork admission bookkeeping that can fail happens before dispatch', () => {
  const forkTool = readFileSync(
    new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs', import.meta.url),
    'utf8',
  )

  const newFork = forkTool.slice(forkTool.indexOf('let private commitNewManagerFork'), forkTool.indexOf('let private finishNewManagerFork'))
  assert.ok(newFork.indexOf('recordFissionAffinity') < newFork.indexOf('runManagerFork'))

  const reuse = forkTool.slice(forkTool.indexOf('let private commitIdleReuse'), forkTool.indexOf('let private reuseWhileIdle'))
  assert.ok(reuse.indexOf('recordFissionAffinity') < reuse.indexOf('runManagerReuse'))
})
}
