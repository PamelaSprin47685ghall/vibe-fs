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

const ownerDescriptor = (sessionId) => [{ sessionId, agent: 'manager' }]

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

test('WHAT[DELEG-003] FORK_TOOL_requires_calling_and_resume_rejects_calling', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-split-'))
  const owner = 'manager-fork-split'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const blankCalling = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      '',
      'Ada',
      'FORK-NEEDS-CALLING',
    )
    assert.match(blankCalling, /calling is required|必须携带 calling/i)
    assert.equal(forkTool.childCount(runtime), 0, 'blank calling must not place any child')

    const rejectedCalling = await forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      'engineer',
      'Ada',
      'RESUME-REJECTS-CALLING',
    )
    assert.match(rejectedCalling, /never calls a new one|从不叫起新人/i)
    assert.match(rejectedCalling, /use fork|用 fork/i)
    assert.equal(forkTool.childCount(runtime), 0, 'resume must not place any child')
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[DELEG-003] FORK_TOOL_manager_resume_dispatches_to_bound_fixed_devops_directly', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-resume-'))
  const owner = 'manager-devops-resume'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    setTimeout(() => {
      forkTool.acceptPrompt(runtime, 0)
    }, 50)

    const resumed = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-FIRST-CHARGE',
    )
    const result = await resumed
    assert.match(result, /devops/)
    assert.match(result, /carries this charge now|现已接下这项托付/i)

    assert.equal(await forkTool.settle(runtime, owner, 'DEVOPS-FIRST-ANSWER', 'devops-run-1'), true)
    const joined = await forkTool.executeJoin(runtime, owner)
    assert.match(joined, /DEVOPS-FIRST-ANSWER/)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[DELEG-003] manager resume preserves companion devops across restart and normalizes state for new charges', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-restart-'))
  const owner = 'manager-devops-restart'
  const runtime1 = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    setTimeout(() => {
      forkTool.acceptPrompt(runtime1, 0)
    }, 50)

    const resumed1 = await forkTool.executeManagerResume(
      runtime1,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-FIRST-CHARGE',
    )
    assert.match(resumed1, /devops/)
    assert.match(resumed1, /carries this charge now|现已接下这项托付/i)

    assert.equal(await forkTool.settle(runtime1, owner, 'DEVOPS-FIRST-ANSWER', 'devops-run-1'), true)
    const joined1 = await forkTool.executeJoin(runtime1, owner)
    assert.match(joined1, /DEVOPS-FIRST-ANSWER/)

    // Simulate process restart: dispose old runtime, create fresh runtime using the same directory/journal
    forkTool.disposeRuntime(runtime1)
    const runtime2 = await forkTool.createRuntime(directory, ownerDescriptor(owner))

    try {
      // Horizon on fresh runtime is cleared (current process handles empty)
      const horizonView = await forkTool.executeHorizon(runtime2, owner)
      assert.ok(!horizonView.includes('DEVOPS-FIRST-CHARGE'))

      // Companion DevOps is NOT cleaned up, and its state is normalized to accept new charges
      waitForPromptCount(runtime2, 1).then(() => {
        forkTool.acceptPrompt(runtime2, 0)
      })

      const resumed2 = await forkTool.executeManagerResume(
        runtime2,
        toolModule,
        owner,
        '',
        'devops',
        'DEVOPS-RESTART-CHARGE',
      )
      assert.match(resumed2, /devops/)
      assert.match(resumed2, /carries this charge now|现已接下这项托付/i)

      assert.equal(await forkTool.settle(runtime2, owner, 'DEVOPS-SECOND-ANSWER', 'devops-run-2'), true)
      const joined2 = await forkTool.executeJoin(runtime2, owner)
      assert.match(joined2, /DEVOPS-SECOND-ANSWER/)
    } finally {
      forkTool.disposeRuntime(runtime2)
    }
  } finally {
    // runtime1 already disposed
  }
})

test('WHAT[DELEG-003] companion devops is preserved and not abandoned when parent session cancels child work', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-cancel-'))
  const owner = 'manager-devops-cancel'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    setTimeout(() => {
      forkTool.acceptPrompt(runtime, 0)
    }, 50)

    const resumed = await forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-CHARGE-BEFORE-CANCEL',
    )
    assert.match(resumed, /devops/)
    assert.match(resumed, /carries this charge now|现已接下这项托付/i)

    // Trigger cancelOwnerChildren
    await forkTool.cancelOwnerChildren(runtime, owner)

    // DevOps handle must remain Active (not Abandoned)
    const lifecycle = forkTool.durableLifecycleByname(runtime, owner, 'devops')
    assert.notEqual(lifecycle, 'Abandoned', 'Companion devops must not be abandoned on parent cancel')

    // Verify DevOps is still intact and ready to accept new assignments
    waitForPromptCount(runtime, 2).then(() => {
      forkTool.acceptPrompt(runtime, 1)
    })

    const resumedAfterCancel = await forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-CHARGE-AFTER-CANCEL',
    )
    assert.match(resumedAfterCancel, /devops/)
    assert.match(resumedAfterCancel, /carries this charge now|现已接下这项托付/i)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
