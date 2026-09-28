import assert from 'node:assert/strict'
import test from 'node:test'
import { writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { sandbox, createCase, casebook, eventStore } from './support/casebook.mjs'

test('WHAT[knowledge-reuse-004] explicit completion freezing preserves B while later maintenance advances to C and D', async () => {
  const local = sandbox()
  try {
    const { identity, baseline } = await createCase(local)
    const initial = await casebook.fetchCaseByIdentity(local.store, identity)
    assert.equal(initial.completionFileState, baseline)
    assert.equal(initial.maintenanceFileState, baseline)
    for (const version of ['C', 'D']) {
      writeFileSync(join(local.dir, 'subject.txt'), `version-${version}`)
      const target = await casebook.freezeCompletionState(local.store, local.dir, ['subject.txt'])
      const result = await casebook.refreshWithDiff(local.store, identity, `diff ${version}`, target, { q: 'Question?', a: `Answer ${version}` })
      assert.equal(result.ok, true)
      const current = await casebook.fetchCaseByIdentity(local.store, identity)
      assert.equal(current.completionFileState, baseline)
      assert.equal(current.maintenanceFileState, target)
      assert.equal(current.sourceTrace, initial.sourceTrace)
      assert.equal(current.a, `Answer ${version}`)
    }
    const original = JSON.parse(baseline)['subject.txt']
    assert.equal(new TextDecoder().decode(await eventStore.readPayload(local.store, original.payloadRef)), 'version-B')
  } finally { local.close() }
})

test('WHAT[knowledge-reuse-004] supplied in-memory baseline produces the actual old and new file content', async () => {
  const local = sandbox()
  try {
    await createCase(local)
    const baseline = await casebook.freezeCompletionState(local.dir, ['subject.txt'])
    writeFileSync(join(local.dir, 'subject.txt'), 'version-C')
    const diff = await casebook.computeMaintenanceDiff(local.dir, baseline)
    assert.equal(diff.hasDiff, true)
    assert.match(diff.diffSummary, /-version-B/)
    assert.match(diff.diffSummary, /\+version-C/)
  } finally { local.close() }
})

test.todo('WHAT[knowledge-reuse-004] GAP-160: actual completion freezes at the logical boundary and DevOps maintenance never creates an Engineer source')
