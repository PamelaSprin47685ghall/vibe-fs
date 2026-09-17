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

test('WHAT[PARTICIPANT-HORIZON-010] FORK_TOOL_manager_horizon_presents_bound_fixed_devops_initially', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-horizon-'))
  const owner = 'manager-devops-horizon'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const roster = await forkTool.executeHorizon(runtime, owner)
    assert.match(roster, /devops/, 'initial manager horizon must present bound fixed devops')
    assert.doesNotMatch(roster, /nothing needs your attention|没有事物需要你的注意|empty/i)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtemp } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

const OWNER = 'owner-ce'
const descriptor = [{ sessionId: OWNER, agent: 'manager' }]

test('WHAT[DELEG-010] SYNC_SERIALIZATION_reuses_dedicated_child_after_completion', async () => {
  const h = await sync.create(await mkdtemp(join(tmpdir(), 'wxs-sync-ce-ref-')), descriptor)
  try {
    const first = sync.invoke(h, 'owner-ce', 'Engineer', 'first')
    await sync.awaitPromptCount(h, 'owner-ce', 'Engineer', 1)
    assert.equal(sync.promptOrigin(h, 'owner-ce', 'Engineer', 0), 'AgentOwnerRoot')
    assert.equal(sync.acceptPrompt(h, 'owner-ce', 'Engineer', 0), true)
    const child = sync.child(h, 'owner-ce', 'Engineer')
    assert.equal(await sync.settle(h, 'owner-ce', 'Engineer', 'first answer', 'run-first'), true)
    assert.equal((await first).ok, true)

    const second = sync.invoke(h, 'owner-ce', 'Engineer', 'second')
    const secondAdmission = await Promise.race([
      second.then((value) => ({ kind: 'result', value })),
      sync.awaitPromptCount(h, 'owner-ce', 'Engineer', 2).then(() => ({ kind: 'prompt' })),
    ])
    assert.deepEqual(secondAdmission, { kind: 'prompt' })
    assert.equal(sync.promptOrigin(h, 'owner-ce', 'Engineer', 1), 'ManagedDelegationAssignment')
    assert.equal(sync.acceptPrompt(h, 'owner-ce', 'Engineer', 1), true)
    assert.equal(sync.child(h, 'owner-ce', 'Engineer'), child)
    assert.equal(await sync.settle(h, 'owner-ce', 'Engineer', 'second answer', 'run-second'), true)
    assert.equal((await second).ok, true)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtemp } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");

const live = async (owner) => sync.create(
  await mkdtemp(join(tmpdir(), 'wxs-sync-delegate-')),
  [{ sessionId: owner, agent: 'manager' }],
)
const waitForChild = async (h, owner, role) => {
  await sync.awaitPromptCount(h, owner, role, 1)
  return sync.child(h, owner, role)
}
const waitForPromptCount = async (h, owner, role, count) => {
  await sync.awaitPromptCount(h, owner, role, count)
  assert.equal(sync.acceptPrompt(h, owner, role, count - 1), true)
}
const settle = async (h, owner, role, answer, run = 'run-1') => sync.settle(h, owner, role, answer, run)
const remainsPending = async (promise) =>
  Promise.race([
    promise.then((value) => ({ kind: 'resolved', value })),
    new Promise((resolve) => setImmediate(() => resolve({ kind: 'pending' }))),
  ])
const verifyReusableHandoff = async (role) => {
  const owner = `owner-handoff-${role.toLowerCase()}`
  const h = await live(owner)
  try {
    await sync.captureOwnerOpening(h, owner, 'ROOT-OPENING-MARKER')

    const first = sync.invoke(h, owner, role, 'FIRST-CHARGE')
    await waitForPromptCount(h, owner, role, 1)
    assert.equal(sync.handoffFrontier(h, owner, role), null)
    assert.match(sync.prompt(h, owner, role, 0), /ROOT-OPENING-MARKER/)

    assert.equal(await settle(h, owner, role, 'FIRST-ANSWER', 'run-first'), true)
    const firstResult = await first
    assert.equal(firstResult.ok, true)
    assert.match(firstResult.value, /FIRST-ANSWER/)
    const firstFrontier = sync.handoffFrontier(h, owner, role)
    assert.notEqual(firstFrontier, null)

    await sync.captureOwnerDeltaPart(h, owner, 'PARENT-DELTA-ONLY-MARKER', 'parent-run-2')

    const second = sync.invoke(h, owner, role, 'SECOND-CHARGE')
    await waitForPromptCount(h, owner, role, 2)
    const secondPrompt = sync.prompt(h, owner, role, 1)
    assert.match(secondPrompt, /SECOND-CHARGE/)
    assert.match(secondPrompt, /parent_delta_work_record\s*=/)
    assert.match(secondPrompt, /PARENT-DELTA-ONLY-MARKER/)
    assert.doesNotMatch(secondPrompt, /ROOT-OPENING-MARKER/)
    assert.equal(sync.handoffFrontier(h, owner, role), firstFrontier)

    assert.equal(
      await sync.settleWithAuthorityRoot(h, owner, role, 'STALE-ANSWER', 'run-stale', 'old-authority-root'),
      false,
    )
    assert.deepEqual(await remainsPending(second), { kind: 'pending' })

    assert.equal(await settle(h, owner, role, 'SECOND-ANSWER', 'run-second'), true)
    const secondResult = await second
    assert.equal(secondResult.ok, true)
    assert.match(secondResult.value, /SECOND-ANSWER/)
    assert.doesNotMatch(secondResult.value, /FIRST-ANSWER/)
    assert.notEqual(sync.handoffFrontier(h, owner, role), firstFrontier)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
}

test('WHAT[DELEG-010] SYNC_RUNTIME_same_role_reuses_one_child_after_completion', async () => {
  const h = await live('owner-sync')
  try {
    const first = sync.invoke(h, 'owner-sync', 'Engineer', 'first')
    const firstChild = await waitForChild(h, 'owner-sync', 'Engineer')
    await waitForPromptCount(h, 'owner-sync', 'Engineer', 1)
    assert.equal(await settle(h, 'owner-sync', 'Engineer', 'first answer', 'run-first'), true)
    const firstResult = await first
    assert.equal(firstResult.ok, true)
    assert.match(firstResult.value, /Recent work/)
    assert.match(firstResult.value, /first answer/)

    const second = sync.invoke(h, 'owner-sync', 'Engineer', 'second')
    assert.equal(await waitForChild(h, 'owner-sync', 'Engineer'), firstChild)
    const immediate = await Promise.race([
      second.then((value) => ({ kind: 'resolved', value })),
      new Promise((resolve) => setImmediate(() => resolve({ kind: 'pending' }))),
    ])
    assert.deepEqual(immediate, { kind: 'pending' }, 'fresh reuse must remain pending until its own completion')
    await waitForPromptCount(h, 'owner-sync', 'Engineer', 2)
    assert.equal(await settle(h, 'owner-sync', 'Engineer', 'second answer', 'run-second'), true)
    const secondResult = await second
    assert.equal(secondResult.ok, true)
    assert.match(secondResult.value, /Recent work/)
    assert.match(secondResult.value, /second answer/)
    assert.doesNotMatch(secondResult.value, /first answer/)
    assert.equal(sync.childCount(h), 1)
  } finally { sync.dispose(h) }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const sync = await import("../../../dist/Execution/Delegation/SyncDelegate/Surface.js");


test('WHAT[DELEG-010] EXEC_026_tier_is_an_ignored_compat_param', () => {
  for (const tier of ['Fast', 'Deep']) {
    const value = sync.vocabulary('Engineer', tier, 'owner-reuse-scope')
    assert.equal(value.agent, 'engineer')
    assert.equal(value.role, 'engineer')
  }
})
test('WHAT[DELEG-010] EXEC_026_agentNameFor_returns_bare_inspector_coder', () => {
  assert.equal(sync.vocabulary('Engineer', 'Fast', 's').agent, 'engineer')
  assert.equal(sync.vocabulary('Engineer', 'Deep', 's').agent, 'engineer')
  assert.equal(sync.vocabulary('Coder', 'Fast', 's').agent, 'coder')
  assert.equal(sync.vocabulary('Coder', 'Deep', 's').agent, 'coder')
})
test('WHAT[DELEG-010] EXEC_026_ReuseScopeId_create_value_and_equals', () => {
  assert.equal(sync.vocabulary('Engineer', 'Fast', 'owner-reuse-scope').scope, 'owner-reuse-scope')
  assert.equal(sync.vocabulary('Engineer', 'Fast', 'other-scope').scope, 'other-scope')
})
test('WHAT[DELEG-010] EXEC_026_DedicatedDelegateKey_binds_scope_and_role', () => {
  assert.equal(sync.vocabulary('Engineer', 'Fast', 'scope-1').role, 'engineer')
  assert.equal(sync.vocabulary('Coder', 'Fast', 'scope-1').role, 'coder')
})
}
