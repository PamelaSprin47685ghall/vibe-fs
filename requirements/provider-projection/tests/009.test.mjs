import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { parse: parseToml } = await import("smol-toml");
const join = await import("../../../dist/Execution/Delegation/Fork/JoinResultRendererSurface.js");


test('WHAT[provider-projection-009] MISC_join_render_batch_preserves_an_empty_literal_entry', () => {
  const wire = join.renderAgentCompletion('english', 'x', '')
  assert.match(wire, /# x has returned\./)
  assert.deepEqual(parseToml(wire), {})
})
test('WHAT[provider-projection-009] MISC_join_render_batch_child_material_is_a_real_multiline_TOML_entry', () => {
  const report = 'Work summary\ndid the thing\n\nRecent work\nok'
  const wire = join.renderAgentCompletion('english', 'coder', report)
  assert.match(wire, /# coder has returned\./)
  assert.match(wire, /^# Work summary$/m)
  assert.match(wire, /^# did the thing$/m)
  assert.doesNotMatch(wire, /^\[\[returned\]\]$/m)
  assert.deepEqual(parseToml(wire), {})
  assert.ok(wire.includes('# did the thing'), 'each line of report is rendered as comment')
  assert.equal(wire.includes('work_record ='), false)
  assert.equal(wire.includes('view ='), false)
  assert.equal(wire.includes('byName'), false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const toolModule = await import("@opencode-ai/plugin/tool");

const codec = await import('../../../dist/OpenCode/Codec/ToolHostSurface.js')
const {
  makeArguments: makeArgs,
  argumentText,
  argumentOptionalText,
  argumentOptionalTexts,
  argumentOptionalNumber,
  schemaString,
  schemaStringDescribed,
  schemaNumber,
  schemaEnum,
  schemaEnumDescribed,
  schemaOptionalEnum,
  schemaOptionalEnumDescribed,
  schemaManagedOrHandle,
  schemaOptionalString,
  schemaOptionalStringDescribed,
  schemaOptionalNumber,
  schemaOptionalNonNegativeIntegerDescribed,
  schemaOptionalStringArray,
  registryNames,
  hide,
  contextDecode,
  contextView,
  contextAttachAbort,
  tomlObject,
  tomlObjectWithInstructions,
  tomlTable,
  looksLikeHandleId,
  digest,
} = codec

test('WHAT[provider-projection-009] CODEC_toml_object_with_instructions_prepends_them', () => {
  const text = tomlObjectWithInstructions(['do this first'], [{ name: 'name', value: 'demo' }])
  assert.ok(text.indexOf('do this first') < text.indexOf('name = "demo"'))
})
}
