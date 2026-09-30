import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as blog from '../../../dist/Enforcer/BlogSurface.js'
import { bindManagedChild, withExecutablePlugin, withPlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'

const field = enforcer.fieldNames()[0]
const structured = (overrides = {}) => ({
  charge: '  Resolve the exact boundary.  ',
  occurrence: '  A decisive state transition happened.  ',
  settlement: '  The boundary is now established.  ',
  consequence: '  Later work can rely on that boundary.  ',
  tip: field,
  ...overrides,
})

test('WHAT[behavior-diagnosis-006] canonical field text trims whitespace and refuses empty input', () => {
  assert.deepEqual(blog.canonicalText('  state transition  '), { ok: true, value: 'state transition' })
  for (const value of ['   ', '', undefined]) {
    assert.deepEqual(blog.canonicalText(value), { ok: false, error: blog.emptyTextError })
  }
})

test('WHAT[behavior-diagnosis-006] codec requires all four structured fields and one tip', () => {
  const decoded = enforcer.decodeCall(structured())
  assert.equal(decoded.ok, true)
  assert.deepEqual(
    {
      charge: decoded.value.charge,
      occurrence: decoded.value.occurrence,
      settlement: decoded.value.settlement,
      consequence: decoded.value.consequence,
      tip: decoded.value.tip.fieldName,
    },
    {
      charge: 'Resolve the exact boundary.',
      occurrence: 'A decisive state transition happened.',
      settlement: 'The boundary is now established.',
      consequence: 'Later work can rely on that boundary.',
      tip: field,
    },
  )

  for (const name of ['charge', 'occurrence', 'settlement', 'consequence']) {
    const missing = structured()
    delete missing[name]
    const result = enforcer.decodeCall(missing)
    assert.equal(result.ok, false)
    assert.match(result.error, new RegExp(name))
  }
})

test('WHAT[behavior-diagnosis-006] codec rejects missing blank and nonstring tip with the same error', () => {
  for (const tip of [undefined, null, '', '  ', 1, false, {}, []]) {
    const result = enforcer.decodeCall(structured({ tip }))
    assert.equal(result.ok, false)
    assert.equal(result.error, 'missing required argument: tip')
  }
})

test('WHAT[behavior-diagnosis-006] legacy entry is recovery-only and cannot mix with structured fields', () => {
  const legacy = enforcer.decodeCall({ entry: '  old work  ', evidence: '  old proof  ', tip: field })
  assert.equal(legacy.ok, true)
  assert.equal(legacy.value.text, 'old work')
  assert.equal(legacy.value.evidence, 'old proof')

  const mixed = enforcer.decodeCall({ ...structured(), entry: 'legacy text' })
  assert.equal(mixed.ok, false)
  assert.match(mixed.error, /cannot be mixed/)
})

test('WHAT[behavior-diagnosis-006] structured cycle renders one natural paragraph without field labels', () => {
  const cycle = enforcer.canonicalCycle(structured())
  assert.equal(
    cycle.mergedText,
    'Resolve the exact boundary. A decisive state transition happened. The boundary is now established. Later work can rely on that boundary.',
  )
  assert.doesNotMatch(cycle.mergedText, /charge:|occurrence:|settlement:|consequence:|\n/)
  assert.equal(cycle.mergedEvidence, '')
})

test('WHAT[behavior-diagnosis-006] structured cycle collapses internal line breaks before joining the paragraph', () => {
  const cycle = enforcer.canonicalCycle(structured({
    occurrence: 'A decisive state\ntransition happened.',
    settlement: 'The boundary\r\nis now established.',
  }))
  assert.equal(
    cycle.mergedText,
    'Resolve the exact boundary. A decisive state transition happened. The boundary is now established. Later work can rely on that boundary.',
  )
})

test('WHAT[behavior-diagnosis-006] real chronicle provider schema exposes five described fields and no entry', async () => {
  await withPlugin(async (hooks) => {
    const args = hooks.tool.chronicle.args
    assert.deepEqual(Object.keys(args), ['charge', 'occurrence', 'settlement', 'consequence', 'tip'])
    for (const name of Object.keys(args)) {
      assert.ok(args[name].description?.trim().length > 0, `${name} must explain its distinct semantic role`)
    }
    assert.equal(Object.prototype.hasOwnProperty.call(args, 'entry'), false)
  })
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
        structured(),
        { sessionID, agent: 'blogger', messageID: `run-${sessionID}`, callID: `call-${sessionID}` },
      ),
      (error) => error?.message === 'CHRONICLE_NO_LIVE_CYCLE',
    )
    assert.deepEqual(runtime.abortedIds, [sessionID])
  })
})
