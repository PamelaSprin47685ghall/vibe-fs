import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import { bindManagedChild, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const field = enforcer.fieldNames()[0]

test('WHAT[behavior-diagnosis-006] canonical entry trims whitespace and refuses empty input', () => {
  assert.deepEqual(blog.canonicalText('  work entry  '), { ok: true, value: 'work entry' })
  for (const entry of ['   ', '', undefined]) {
    assert.deepEqual(blog.canonicalText(entry), { ok: false, error: blog.emptyTextError })
  }
})

test('WHAT[behavior-diagnosis-006] codec rejects missing blank and nonstring tip with the same error', () => {
  for (const tip of [undefined, null, '', '  ', 1, false, {}, []]) {
    const result = enforcer.decodeCall({ entry: 'work', tip })
    assert.equal(result.ok, false)
    assert.equal(result.error, 'missing required argument: tip')
  }
})

test('WHAT[behavior-diagnosis-006] valid entry and evidence do not become tip identities', () => {
  const result = enforcer.decodeCall({ entry: '  work  ', evidence: '  proof  ', tip: field })
  assert.equal(result.ok, true)
  assert.equal(result.value.text, 'work')
  assert.equal(result.value.evidence, 'proof')
  assert.equal(result.value.tip.fieldName, field)
  assert.equal(enforcer.hasValidText(enforcer.decodeCall({ entry: ' ', tip: field }).value), false)
})

test('WHAT[behavior-diagnosis-006] NoLiveCycle is typed before Host error encoding', () => {
  assert.deepEqual(enforcer.chronicleExecutionContract(true), { kind: 'Completed', value: 'provider-result' })
  assert.deepEqual(enforcer.chronicleExecutionContract(false), { kind: 'NoLiveCycle' })
})

test('WHAT[behavior-diagnosis-006] real plugin aborts obsolete Blogger before exposing NoLiveCycle', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'blogger-no-live-cycle'
    await bindManagedChild(runtime, 'ses-manager', sessionID, 'blogger')
    await hooks['chat.message'](
      { sessionID, agent: 'blogger' },
      {
        message: {
          id: `root-${sessionID}`, role: 'user', sessionID, agent: 'blogger',
          model: { providerID: 'host', modelID: 'placeholder' },
        },
        parts: [],
      },
    )
    await assert.rejects(
      () => hooks.tool.chronicle.execute(
        { entry: 'work', tip: field },
        { sessionID, agent: 'blogger', messageID: `run-${sessionID}`, callID: `call-${sessionID}` },
      ),
      (error) => error?.message === 'CHRONICLE_NO_LIVE_CYCLE',
    )
    assert.deepEqual(runtime.abortedIds, [sessionID])
  })
})
