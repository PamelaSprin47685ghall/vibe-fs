import assert from 'node:assert/strict'
import test from 'node:test'
import { sandbox, createCase, casebook } from './support/casebook.mjs'

test('WHAT[knowledge-reuse-002] captured fields and distinct invocation identities survive the actual event store', async () => {
  const local = sandbox()
  try {
    const first = casebook.caseIdentityForInvocation('same-session', 'invocation-1')
    const second = casebook.caseIdentityForInvocation('same-session', 'invocation-2')
    assert.notEqual(first, second)
    const { baseline } = await createCase(local, first)
    await createCase(local, second)
    const actual = await casebook.fetchCaseByIdentity(local.store, first)
    assert.equal(actual.identity, first)
    assert.equal(actual.q, 'Question?')
    assert.equal(actual.a, 'Answer B')
    assert.equal(actual.sourceTrace, 'trace-1')
    assert.deepEqual(actual.relatedPaths, ['subject.txt'])
    assert.equal(actual.completionFileState, baseline)
    assert.equal(actual.maintenanceFileState, baseline)
    assert.equal((await casebook.fetchCaseByIdentity(local.store, second)).identity, second)
  } finally { local.close() }
})
