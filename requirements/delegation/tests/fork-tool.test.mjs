// Fork tool payload and unknown-calling consequences through ForkSurface.
import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'

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
const ownerDescriptor = (sessionId) => [{ sessionId, agent: 'fast-manager' }]

test('WHAT[DELEG-019] FORK_TOOL_payload_has_assignment_and_requirements', () => {
  const wire = fork.render('en', {
    Assignment: 'inspect',
    CommissionerRecord: 'manager record',
    Attachment: 'attachment',
    RootRequirements: ['one', 'two'],
    Payload: 'payload',
  })
  assert.match(wire, /inspect/)
  assert.match(wire, /one/)
  assert.match(wire, /two/)
})
test('WHAT[DELEG-019] FORK_TOOL_unknown_calling_is_generic_denial', () => {
  assert.match(fork.unavailableCalling('en', false), /Unknown or unavailable calling/)
})
test('WHAT[DELEG-019] FORK_TOOL_orchestrator_unknown_calling_is_generic_denial', () => {
  assert.match(fork.unavailableCalling('en', true), /Unknown or unavailable calling/)
})

test('WHAT[DELEG-003] FORK_road_with_calling_is_independent_and_omitted_calling_continues_byname', () => {
  const independent = fork.chooseRoad('Manager', 'Ada', 'inspect the retry path')
  assert.equal(independent.ok, true)
  assert.equal(independent.road, 'Independent')
  assert.equal(independent.byname, 'Ada')
  assert.equal(independent.charge, 'inspect the retry path')
  assert.equal(independent.authorityTransferred, false)

  const continuation = fork.chooseRoad('', 'Ada', 'continue the retry path')
  assert.equal(continuation.ok, true)
  assert.equal(continuation.road, 'Continuation')
  assert.equal(continuation.byname, 'Ada')
  assert.equal(continuation.calling, null)
})

test('WHAT[DELEG-006] FORK_continuation_reuses_bound_managed_agent_and_does_not_rebind_tier', () => {
  const result = fork.reuseBinding('Ada', 'deep-inspector', 'fast-inspector', 'deep', 'continue the charge')
  assert.equal(result.ok, true)
  assert.equal(result.byname, 'Ada')
  assert.equal(result.managedAgent, 'deep-inspector')
  assert.equal(result.requestedAgent, 'fast-inspector')
  assert.equal(result.tier, 'deep')
  assert.equal(result.authorityTransferred, false)
  assert.equal(Object.hasOwn(result, 'agentId'), false)
})

test('WHAT[DELEG-024] FORK_TOOL_same_byname_reuse_dispatches_immediately_and_leaves_completion_to_join', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-reuse-'))
  const owner = 'manager-reuse'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    await forkTool.captureOwnerOpening(runtime, owner, 'ROOT-OPENING-MARKER')

    const first = forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'coder',
      'Ada',
      'FIRST-FORK-CHARGE',
    )
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.equal(forkTool.childCount(runtime), 1)
    const child = forkTool.child(runtime)
    assert.notEqual(child, null)
    assert.match(forkTool.prompt(runtime, 0), /FIRST-FORK-CHARGE/)
    assert.match(forkTool.prompt(runtime, 0), /ROOT-OPENING-MARKER/)
    assert.match(await first, /Ada/)

    assert.equal(await forkTool.settle(runtime, owner, 'FIRST-FORK-ANSWER', 'fork-run-1'), true)

    await forkTool.captureOwnerDeltaPart(runtime, owner, 'PARENT-FRESH-DELTA-MARKER', 'parent-run-2')

    const second = forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      '',
      'Ada',
      'SECOND-FORK-CHARGE',
    )

    await waitForPromptCount(runtime, 2)
    assert.equal(forkTool.acceptPrompt(runtime, 1), true)
    assert.equal(forkTool.child(runtime), child, 'same Byname must reuse the same physical child')
    assert.equal(forkTool.childCount(runtime), 1)

    const secondPrompt = forkTool.prompt(runtime, 1)
    assert.match(secondPrompt, /SECOND-FORK-CHARGE/)
    assert.match(secondPrompt, /commissioner_record\s*=/)
    assert.match(secondPrompt, /PARENT-FRESH-DELTA-MARKER/)
    assert.doesNotMatch(secondPrompt, /ROOT-OPENING-MARKER/)

    const secondResult = await second
    assert.match(secondResult, /Ada/)
    assert.doesNotMatch(secondResult, /SECOND-FORK-ANSWER/)
    assert.doesNotMatch(secondResult, /FIRST-FORK-ANSWER/)

    assert.equal(
      forkTool.durableLifecycleByname(runtime, owner, 'Ada'),
      'Active',
      'fork return must not imply that the reused child already completed',
    )

    assert.equal(await forkTool.settle(runtime, owner, 'SECOND-FORK-ANSWER', 'fork-run-2'), true)
    assert.equal(
      forkTool.durableLifecycleByname(runtime, owner, 'Ada'),
      'CompletedAwaitingJoin',
      'completion remains a separately consumable join fact',
    )
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
      'coder',
      'Ada',
      'UNCERTAIN-FORK-CHARGE',
    )

    assert.doesNotMatch(result, /could not be placed|无法托付/i)
    assert.match(result, /may already have been accepted|可能已经接收/i)
    assert.equal(forkTool.childCount(runtime), 1)
    assert.equal(forkTool.promptCount(runtime), 1, 'physical Host send was attempted exactly once')
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[DELEG-026] FORK_TOOL_transport_receipt_without_physical_acceptance_keeps_run_active', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-receipt-pending-'))
  const owner = 'manager-receipt-pending'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    forkTool.nextPromptAdmittedWithReceipt(runtime, 'accepted-without-message-id')

    const result = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'coder',
      'Ada',
      'RECEIPT-PENDING-FORK-CHARGE',
    )

    assert.doesNotMatch(result, /could not complete the charge|could not be placed|无法托付/i)
    assert.match(result, /may already have been accepted|可能已经接收/i)
    assert.equal(forkTool.childCount(runtime), 1)
    assert.equal(forkTool.promptCount(runtime), 1)
    assert.equal(
      forkTool.durableLifecycleByname(runtime, owner, 'Ada'),
      'Active',
      'transport admission without PhysicalAccepted must preserve the pending run for exact ingress evidence',
    )
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[PARTICIPANT-HORIZON-011] FORK_TOOL_abandoned_child_does_not_vanish_from_horizon_before_join', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-abandoned-horizon-'))
  const owner = 'manager-abandoned-horizon'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'coder', 'Ada', 'VISIBLE-CHARGE')
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)

    await forkTool.cancelOwnerChildren(runtime, owner)

    const roster = await forkTool.executeHorizon(runtime, owner)
    assert.match(roster, /Ada/, 'durable abandoned child must not become indistinguishable from never-created')
    assert.match(roster, /did not return|未从此项任务归来/i)
    assert.doesNotMatch(roster, /no one is currently away|当前没有.*在外/i)
    assert.equal(forkTool.abortCount(runtime), 1, 'authorized logical cancel still physically tears down the child')
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[MANAGED-SESSION-018] FORK_TOOL_process_detach_preserves_durable_active_child_for_restart', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-process-detach-'))
  const owner = 'manager-process-detach'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'coder', 'Ada', 'SURVIVE-PLUGIN-RELOAD')
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')

    await forkTool.detachToolRuntime(runtime)

    assert.equal(
      forkTool.durableLifecycleByname(runtime, owner, 'Ada'),
      'Active',
      'process/plugin shutdown has no authority to manufacture ParentCancelled',
    )
    assert.equal(forkTool.abortCount(runtime), 0, 'process/plugin detach must not call Host AbortSession for live child agents')
    assert.equal(forkTool.childCount(runtime), 1)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

