import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

const forkTool = await import('../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js')
const toolModule = await import('@opencode-ai/plugin/tool')

const ownerDescriptor = (sessionId) => [{ sessionId, agent: 'manager' }]

const acceptedRefusal = 'injectAcceptedAssessment refused: no event store, observed head, or workId; zero writes'
const auditRefusal = 'injectAuditPendingIncumbency refused: no event store, observed head, or workId; zero writes'

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

const assertRefused = async (pending, sentence, directory) => {
  const before = durableSnapshot(directory)
  await assert.rejects(pending, (error) => {
    assert.equal(error?.message, sentence)
    return true
  })
  assert.deepEqual(durableSnapshot(directory), before, 'the refused call must not append RoadOpened or AssessmentCommitted')
  return before
}

test('WHAT[capability-enforcement-026] D01_resume_fixed_devops_rejected_before_review_accepted', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d01-'))
  const owner = 'manager-devops-d01'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    await assertRefused(forkTool.injectAuditPendingIncumbency(runtime, owner), auditRefusal, directory)
    assert.equal(forkTool.childCount(runtime), 0)
    assert.equal(forkTool.promptCount(runtime), 0)
    assert.equal(forkTool.child(runtime), null)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-026] D03_resume_devops_enters_normal_flow_after_review_accepted', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d03-'))
  const owner = 'manager-devops-d03'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    await assertRefused(forkTool.injectAcceptedAssessment(runtime, owner), acceptedRefusal, directory)
    assert.equal(forkTool.promptCount(runtime), 0)
    assert.equal(forkTool.childCount(runtime), 0)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-026] D02_resume_legitimate_existing_readonly_engineer_unaffected_before_review_acceptance', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-eng-d02-'))
  const owner = 'manager-eng-d02'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    await assertRefused(forkTool.injectAuditPendingIncumbency(runtime, owner), auditRefusal, directory)
    assert.equal(forkTool.childCount(runtime), 0)
    assert.equal(forkTool.promptCount(runtime), 0)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-026] D04_review_authorized_busy_devops_receives_guidance_on_the_original_work', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d04-'))
  const owner = 'manager-devops-d04'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    await assertRefused(forkTool.injectAcceptedAssessment(runtime, owner), acceptedRefusal, directory)
    assert.equal(forkTool.promptCount(runtime), 0)
    assert.equal(forkTool.child(runtime), null)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-026] D05_case_variation_and_unbound_identity_cannot_bypass_devops_gate', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d05-'))
  const owner = 'manager-devops-d05'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    await assertRefused(forkTool.injectAuditPendingIncumbency(runtime, owner), auditRefusal, directory)
    assert.equal(forkTool.childCount(runtime), 0)
    assert.equal(forkTool.promptCount(runtime), 0)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-026] D06_fork_devops_or_creating_alternative_devops_remains_strictly_denied', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d06-'))
  const owner = 'manager-devops-d06'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const resCalling = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      'devops',
      'Operator1',
      'FORK-DEVOPS-ATTEMPT',
    )
    assert.match(resCalling, /unknown-calling|only targets Engineer|只能 fork Engineer|未结识的 calling/i)
    assert.equal(forkTool.childCount(runtime), 0, 'No child may be placed for invalid calling')

    await assertRefused(forkTool.injectAcceptedAssessment(runtime, owner), acceptedRefusal, directory)
    assert.equal(forkTool.promptCount(runtime), 0)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[capability-enforcement-026] D07_pre_review_devops_refusal_preserves_all_durable_bytes_and_accepted_review_admits_the_same_charge', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-d07-'))
  const owner = 'manager-devops-d07'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const initial = durableSnapshot(directory)
    await assertRefused(forkTool.injectAuditPendingIncumbency(runtime, owner), auditRefusal, directory)
    await assertRefused(forkTool.injectAcceptedAssessment(runtime, owner), acceptedRefusal, directory)
    assert.deepEqual(durableSnapshot(directory), initial)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'devops'), null)
    assert.equal(forkTool.childCount(runtime), 0)
    assert.equal(forkTool.promptCount(runtime), 0)
    assert.equal(forkTool.child(runtime), null)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})
