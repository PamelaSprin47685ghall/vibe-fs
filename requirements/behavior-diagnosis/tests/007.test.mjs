import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");


test('WHAT[BD-007] CHRONICLE_unknown_tip_is_repaired_at_runtime', () => {
  const result = blog.execute({ hasFlight: true, sessionId: 'ses-blog', entry: 'entry', tip: 'not-a-field' })
  assert.equal(result.ok, true)
  assert.equal(result.text, 'remembered')
  assert.equal(result.error, null)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");

const firstField = () => enforcer.fieldNames()[0]
const firstRule = () => enforcer.tryFindByField(firstField())

test('WHAT[BD-007] ENFORCER_023_nonempty_unknown_tip_resolves', () => {
  const result = enforcer.decodeCall({ text: 'entry', tip: 'not-a-catalog-field' })
  assert.equal(result.ok, true)
  assert.ok(enforcer.fieldNames().includes(result.value.tip.fieldName))
})
test('WHAT[BD-007] ENFORCER_024_misspelled_tip_maps_to_nearest_rule', () => {
  const result = enforcer.decodeCall({ text: 'entry', tip: 'primitive-obsessin' })
  assert.equal(result.ok, true)
  assert.equal(result.value.tip.fieldName, 'primitive-obsession')
})
test('WHAT[BD-007] ENFORCER_021_valid_field_maps_exact_rule_id', () => {
  const field = firstField()
  const rule = firstRule()
  assert.ok(rule, `catalog must resolve ${field}`)

  const result = enforcer.decodeCall({ text: 'work log entry', tip: field })
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.deepEqual(result.value, {
    text: 'work log entry',
    evidence: null,
    tip: {
      ruleId: rule.ruleId,
      fieldName: rule.fieldName,
      lexicalOrder: rule.lexicalOrder,
    },
  })
})
test('WHAT[BD-007] ENFORCER_021_tip_trims_whitespace_before_lookup', () => {
  const field = firstField()
  const rule = firstRule()
  const result = enforcer.decodeCall({ text: 'entry', tip: `  ${field}  ` })
  assert.equal(result.ok, true)
  assert.equal(result.value.tip.ruleId, rule.ruleId)
  assert.equal(result.value.tip.fieldName, rule.fieldName)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");
const observation = await import("../../../dist/Enforcer/ObservationSurface.js");

const fields = enforcer.fieldNames()
const cycleRecord = (n, field) => ({
  mainSessionId: 'ses-main',
  bloggerSessionId: 'ses-blog',
  run: `msg_tip_${n}`,
  toolCallIds: [`call-${n}`],
  textRef: `blob-t${n}`,
  textDigest: `sha-t${n}`,
  tipRuleId: field,
  fieldNameAtCommit: field,
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
})

test('WHAT[BD-007] ENFORCER_TIP_06_unknown_tip_resolves_to_catalog', () => {
  const r = enforcer.decodeCall({ text: 'entry', tip: 'totally-unknown-field' })
  assert.equal(r.ok, true)
  assert.ok(fields.includes(r.value.tip.fieldName))
})
test('WHAT[BD-007] ENFORCER_TIP_07_valid_field_maps_rule_id_exactly', () => {
  const field = 'primitive-obsession'
  const rule = enforcer.tryFindByField(field)
  const r = enforcer.decodeCall({ text: 'entry', tip: field })
  assert.ok(rule)
  assert.equal(r.ok, true)
  assert.equal(r.value.tip.ruleId, rule.ruleId)
  assert.equal(r.value.tip.fieldName, field)
})
}
