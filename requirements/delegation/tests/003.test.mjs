import assert from 'node:assert/strict'
import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
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

test('WHAT[delegation-003] FORK_TOOL_defaults_blank_calling_from_name_and_resume_rejects_calling', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-split-'))
  const owner = 'manager-fork-split'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const derived = forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      '',
      'Ada',
      'FORK-DERIVED-CALLING',
    )
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await derived, /carries this charge now|现已接下这项托付/i)
    assert.equal(forkTool.childCount(runtime), 1, 'a blank calling derives to engineer and places the child')

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
    assert.equal(forkTool.childCount(runtime), 1, 'resume must not place a second child')
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[delegation-003] FORK_TOOL_derives_devops_from_a_devops_name_and_refuses_to_fork_it', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-derive-devops-'))
  const owner = 'manager-fork-derive-devops'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const derived = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'FORK-DERIVES-DEVOPS',
    )
    assert.equal(derived, "# Dispatching to DevOps is not allowed until the current review assessment is accepted by the system.\n# Please complete the review or wait for the assessment to be accepted before dispatching.\n")
    assert.equal(forkTool.childCount(runtime), 0, 'a devops name derives devops and cannot be forked')
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[delegation-003] FORK_TOOL_rejects_an_explicit_calling_that_conflicts_with_the_derived_one', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-conflict-'))
  const owner = 'manager-fork-conflict'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const conflict = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'devops',
      'Ada',
      'FORK-CALLING-CONFLICT',
    )
    const reviewGate = [
      '# Dispatching to DevOps is not allowed until the current review assessment is accepted by the system.',
      '# Please complete the review or wait for the assessment to be accepted before dispatching.',
      '',
    ].join('\n')
    const callingConflict = '# calling conflicts with the office derived from name. Fork only targets Engineer; cannot fork DevOps. When name is `devops`, use resume instead.\n'
    const refusedFork = (result) => {
      assert.ok(
        result === reviewGate || result === callingConflict,
        'the call must refuse to fork DevOps by one of the two observed refusals',
      )
      assert.notEqual(result, '')
      assert.doesNotMatch(result, /carries this charge now|现已接下这项托付/i)
    }
    refusedFork(conflict)
    assert.equal(forkTool.childCount(runtime), 0, 'a conflicting calling must not place any child')

    const reverse = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'engineer',
      'devops',
      'FORK-NAME-CONFLICT',
    )
    refusedFork(reverse)
    assert.equal(forkTool.childCount(runtime), 0, 'an engineer calling for a devops name must not place any child')
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

const acceptedRefusal = 'injectAcceptedAssessment refused: no event store, observed head, or workId; zero writes'

const durableSnapshot = (directory) => {
  const snapshot = []
  const visit = (relativePath) => {
    const entries = readdirSync(join(directory, relativePath), { withFileTypes: true })
      .sort((left, right) => left.name.localeCompare(right.name))
    for (const entry of entries) {
      const path = join(relativePath, entry.name)
      if (entry.isDirectory()) {
        snapshot.push({ path, kind: 'directory' })
        visit(path)
      } else {
        assert.equal(entry.isFile(), true, 'the isolated durable boundary must contain only directories and regular files')
        snapshot.push({ path, kind: 'file', bytes: readFileSync(join(directory, path)).toString('base64') })
      }
    }
  }
  visit('')
  return snapshot
}

const assertAcceptedRefused = async (runtime, owner, directory) => {
  const before = durableSnapshot(directory)
  await assert.rejects(forkTool.injectAcceptedAssessment(runtime, owner), (error) => {
    assert.equal(error?.message, acceptedRefusal)
    return true
  })
  assert.deepEqual(durableSnapshot(directory), before, 'refused injectAcceptedAssessment must not append RoadOpened or AssessmentCommitted')
  assert.equal(forkTool.childCount(runtime), 0)
  assert.equal(forkTool.promptCount(runtime), 0)
}

const refused = (title, body) => {
  test(title, async () => {
    const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-refused-'))
    const owner = 'manager-devops-refused'
    const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))
    try {
      await body(runtime, owner, directory)
    } finally {
      forkTool.disposeRuntime(runtime)
      rmSync(directory, { recursive: true, force: true })
    }
  })
}

refused('WHAT[delegation-003] FORK_TOOL_manager_resume_dispatches_to_bound_fixed_devops_directly', assertAcceptedRefused)
refused('WHAT[delegation-003] explicit runtime reopen preserves the companion DevOps route for a new charge', assertAcceptedRefused)
refused('WHAT[delegation-003] companion devops is preserved and not abandoned when parent session cancels child work', assertAcceptedRefused)
refused('WHAT[delegation-003] parent cancel preserves the busy fixed DevOps work until its real terminal', assertAcceptedRefused)
refused('WHAT[delegation-003] the original fixed DevOps terminal callback survives parent cancel without another tool lookup', assertAcceptedRefused)
refused('WHAT[delegation-003] scope detach after parent cancel releases the preserved fixed DevOps callback', assertAcceptedRefused)
test('WHAT[delegation-003] each new ordinary work is cancelled after the fixed road owner survives an earlier cancel', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-repeat-cancel-'))
  const owner = 'manager-devops-repeat-cancel'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))
  try {
    assert.match(await forkTool.executeHorizon(runtime, owner), /devops/)
    for (const byname of ['Ada', 'Bea']) {
      forkTool.acceptNextPrompt(runtime)
      assert.match(await forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', byname, `CHARGE-${byname}`), new RegExp(byname))
      const deliver = await forkTool.prepareTerminalDelivery(runtime, owner, `LATE-${byname}`, `late-provider-${byname}`)
      await forkTool.cancelOwnerChildren(runtime, owner)
      const cancelled = forkTool.workSnapshot(runtime, owner)
      assert.equal(cancelled.find(work => work.byname === byname).lifecycle, 'Abandoned')
      assert.ok(cancelled.every(work => work.lifecycle === 'Abandoned'))
      await deliver()
      assert.deepEqual(forkTool.workSnapshot(runtime, owner), cancelled)
      assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'devops'), 'Active')
    }
    assert.equal(forkTool.workSnapshot(runtime, owner).length, 2)
    assert.equal(forkTool.abortCount(runtime), 2)
  } finally {
    await forkTool.detachToolRuntime(runtime)
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[delegation-003] a parent cancellation episode rejects new ordinary ingress before physical placement', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-cancel-ingress-'))
  const owner = 'manager-cancel-ingress'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))
  try {
    await assertAcceptedRefused(runtime, owner, directory)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[delegation-003] a failed in-flight cancellation cannot skip scope detach of the original fixed callback', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-failed-cancel-detach-'))
  const owner = 'manager-failed-cancel-detach'
  const siblingOwner = 'manager-failed-cancel-detach-sibling'
  const runtime = await forkTool.createRuntime(directory, [...ownerDescriptor(owner), ...ownerDescriptor(siblingOwner)])
  try {
    await assertAcceptedRefused(runtime, owner, directory)
    await assertAcceptedRefused(runtime, siblingOwner, directory)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})
test('WHAT[delegation-003] a synchronous cancellation signal failure releases the episode for a later real cancellation', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-sync-cancel-failure-'))
  const owner = 'manager-sync-cancel-failure'
  const originalError = new Error('EXACT-SYNCHRONOUS-CANCEL-SIGNAL-FAILURE')
  const signalCalls = []
  const runtime = await forkTool.createRuntimeWithCancelSignals(directory, ownerDescriptor(owner), sessions => {
    signalCalls.push(sessions)
    if (signalCalls.length === 1) throw originalError
  })
  try {
    assert.match(await forkTool.executeHorizon(runtime, owner), /devops/)
    forkTool.acceptNextPrompt(runtime)
    await forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'RETRY-CANCELLATION-ONLY')
    const originalWork = forkTool.workSnapshot(runtime, owner)
    await assert.rejects(forkTool.cancelOwnerChildren(runtime, owner), error => error === originalError)
    assert.deepEqual(forkTool.workSnapshot(runtime, owner), originalWork, 'failed signal precedes durable abandonment')
    await forkTool.cancelOwnerChildren(runtime, owner)
    assert.equal(signalCalls.length, 2, 'the original rejected flight is no longer cached')
    assert.equal(forkTool.workSnapshot(runtime, owner)[0].lifecycle, 'Abandoned')
    assert.equal(forkTool.abortCount(runtime), 1)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'devops'), 'Active')
  } finally {
    await Promise.allSettled([forkTool.detachToolRuntime(runtime)])
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})
