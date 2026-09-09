// Host tool codec semantics through its owner surface.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as toolModule from '@opencode-ai/plugin/tool'

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

test('WHAT[PROVIDER-PROJECTION-005] CODEC_arguments_text_reads_present_and_missing', () => {
  const args = makeArgs({ name: 'value', blank: '  ' })
  assert.equal(argumentText(args, 'name'), 'value')
  assert.equal(argumentText(args, 'missing'), '')
  assert.equal(argumentText(args, 'blank'), '  ')
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_arguments_optional_text_filters_blank', () => {
  const args = makeArgs({ name: 'value', blank: '  ' })
  assert.equal(argumentOptionalText(args, 'name'), 'value')
  assert.equal(argumentOptionalText(args, 'blank'), null)
  assert.equal(argumentOptionalText(args, 'missing'), null)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_arguments_optional_texts_collects_nonempty_strings', () => {
  const args = makeArgs({ items: ['a', '', '  ', 'b', null, 7] })
  assert.deepEqual(argumentOptionalTexts(args, 'items'), ['a', 'b', '7'])
  assert.equal(argumentOptionalTexts(args, 'missing'), null)
  const notArray = makeArgs({ items: 'plain' })
  assert.equal(argumentOptionalTexts(notArray, 'items'), null)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_arguments_optional_number_reads_floats', () => {
  const args = makeArgs({ timeout: 2.5, name: 'x' })
  assert.equal(argumentOptionalNumber(args, 'timeout'), 2.5)
  assert.equal(argumentOptionalNumber(args, 'missing'), null)
  assert.equal(argumentOptionalNumber(args, 'name'), null)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_arguments_null_raw_is_all_absent', () => {
  const args = makeArgs(null)
  assert.equal(argumentText(args, 'x'), '')
  assert.equal(argumentOptionalText(args, 'x'), null)
  assert.equal(argumentOptionalTexts(args, 'x'), null)
  assert.equal(argumentOptionalNumber(args, 'x'), null)
})

test('WHAT[VERIFICATION-SYSTEM-008] tool schema surface preserves native validation and optionality', () => {
  const schema = toolModule.tool.schema.object({
    source: schemaString(toolModule),
    described: schemaStringDescribed(toolModule, 'program source'),
    count: schemaNumber(toolModule),
    choice: schemaEnum(toolModule, ['a', 'b']),
    describedChoice: schemaEnumDescribed(toolModule, ['a', 'b'], 'pick one'),
    optionalChoice: schemaOptionalEnum(toolModule, ['a', 'b']),
    optionalDescribedChoice: schemaOptionalEnumDescribed(toolModule, ['a', 'b'], 'maybe'),
    target: schemaManagedOrHandle(toolModule, ['coder']),
    hint: schemaOptionalString(toolModule),
    describedHint: schemaOptionalStringDescribed(toolModule, 'hints'),
    estimate: schemaOptionalNumber(toolModule),
    budget: schemaOptionalNonNegativeIntegerDescribed(toolModule, 'delegator estimate'),
    names: schemaOptionalStringArray(toolModule),
  })
  const required = { source: '', described: 'program', count: 2.5, choice: 'a', describedChoice: 'b', target: 'handle-123' }
  assert.deepEqual(schema.parse(required), required)
  const complete = {
    ...required, optionalChoice: 'b', optionalDescribedChoice: 'a', hint: '', describedHint: 'hint',
    estimate: 1.5, budget: 0, names: ['one', 'two'],
  }
  assert.deepEqual(schema.parse(complete), complete)
  for (const mutation of [
    { source: 1 }, { described: false }, { count: '2' }, { choice: 'unknown' },
    { describedChoice: 'unknown' }, { optionalChoice: 'unknown' }, { optionalDescribedChoice: 'unknown' },
    { target: 1 }, { hint: 2 }, { describedHint: [] }, { estimate: '1' },
    { budget: -1 }, { budget: 0.5 }, { names: ['one', 2] },
  ]) {
    assert.equal(schema.safeParse({ ...complete, ...mutation }).success, false, JSON.stringify(mutation))
  }
})

test('WHAT[VERIFICATION-SYSTEM-008] tool schema surface does not unwrap native literal values', () => {
  const constrainedHost = { tool: { schema: { string: () => toolModule.tool.schema.literal('allowed') } } }
  const schema = schemaString(constrainedHost)
  assert.equal(schema.parse('allowed'), 'allowed')
  assert.equal(schema.safeParse('other').success, false)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_registry_maps_specs_by_name', () => {
  const built = registryNames({ tool: (definition) => ({ def: definition }) }, ['one', 'two'])
  assert.ok(built.one)
  assert.ok(built.two)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_hide_defines_non_enumerable_property', () => {
  const target = {}
  hide(target, 'secret', () => 'hidden')
  assert.equal(target.secret(), 'hidden')
  assert.equal(Object.prototype.propertyIsEnumerable.call(target, 'secret'), false)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_prompt_text_prefers_message_parts', () => {
  const ctx = contextDecode({ sessionID: 'ses_p', message: { parts: [{ text: 'hello ' }, { text: 'world' }] }, prompt: 'fallback prompt' })
  assert.equal(contextView(ctx).promptText, 'hello world')
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_prompt_text_falls_back_to_prompt_then_input', () => {
  assert.equal(contextView(contextDecode({ sessionID: 's', prompt: 'the prompt' })).promptText, 'the prompt')
  assert.equal(contextView(contextDecode({ sessionID: 's', input: 'the input' })).promptText, 'the input')
  assert.equal(contextView(contextDecode({ sessionID: 's' })).promptText, null)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_prompt_text_blank_parts_fall_through', () => {
  assert.equal(contextView(contextDecode({ sessionID: 's', message: { parts: [{ text: '   ' }] }, prompt: 'real prompt' })).promptText, 'real prompt')
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_attach_abort_without_signal_is_noop_unsubscribe', () => {
  const ctx = contextDecode({ sessionID: 's' })
  let fired = false
  const unsubscribe = contextAttachAbort(ctx, () => { fired = true })
  unsubscribe()
  assert.equal(fired, false)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_attach_abort_fires_immediately_on_aborted_signal', () => {
  const signal = { aborted: true, addEventListener: () => {}, removeEventListener: () => {} }
  const ctx = contextDecode({ sessionID: 's', abort: signal })
  let fired = false
  contextAttachAbort(ctx, () => { fired = true })
  assert.equal(fired, true)
})

test('WHAT[PROVIDER-PROJECTION-005] CODEC_attach_abort_subscribes_and_unsubscribes', () => {
  const listeners = []
  const signal = {
    aborted: false,
    addEventListener: (name, listener) => listeners.push([name, listener]),
    removeEventListener: (name, listener) => {
      const index = listeners.findIndex(([n, l]) => n === name && l === listener)
      if (index >= 0) listeners.splice(index, 1)
    },
  }
  const ctx = contextDecode({ sessionID: 's', abortSignal: signal })
  let fired = false
  const unsubscribe = contextAttachAbort(ctx, () => { fired = true })
  assert.equal(listeners.length, 1)
  listeners[0][1]()
  assert.equal(fired, true)
  unsubscribe()
  assert.equal(listeners.length, 0)
})

test('WHAT[PROVIDER-PROJECTION-008] CODEC_toml_object_renders_scalar_fields', () => {
  const text = tomlObject([{ name: 'name', value: 'demo' }, { name: 'count', value: 3 }, { name: 'big', value: 9n }, { name: 'flag', value: true }])
  assert.match(text, /name = "demo"/)
  assert.match(text, /count = 3/)
  assert.match(text, /big = 9/)
  assert.match(text, /flag = true/)
})

test('WHAT[PROVIDER-PROJECTION-008] CODEC_toml_object_renders_nested_table', () => {
  const text = tomlObject([{ name: 'meta', value: { key: 'v' } }])
  assert.match(text, /\[meta\]/)
  assert.match(text, /key = "v"/)
})

test('WHAT[PROVIDER-PROJECTION-009] CODEC_toml_object_with_instructions_prepends_them', () => {
  const text = tomlObjectWithInstructions(['do this first'], [{ name: 'name', value: 'demo' }])
  assert.ok(text.indexOf('do this first') < text.indexOf('name = "demo"'))
})

test('WHAT[PROVIDER-PROJECTION-008] CODEC_toml_table_renders_array_of_tables', () => {
  const text = tomlTable('item', [[{ name: 'id', value: 'a' }], [{ name: 'id', value: 'b' }]])
  assert.equal(text.match(/\[\[item\]\]/g)?.length ?? 0, 2)
})

test('WHAT[PROVIDER-PROJECTION-003] CODEC_looks_like_handle_id_shape', () => {
  assert.equal(looksLikeHandleId('ab12cd'), true)
  assert.equal(looksLikeHandleId('zz9900'), true)
  assert.equal(looksLikeHandleId('ab12'), false)
  assert.equal(looksLikeHandleId('ab12cdef'), false)
  assert.equal(looksLikeHandleId('AB12CD'), false)
  assert.equal(looksLikeHandleId('ab-12c'), false)
  assert.equal(looksLikeHandleId(''), false)
  assert.equal(looksLikeHandleId('      '), false)
})

test('WHAT[PROVIDER-PROJECTION-003] CODEC_digest_is_true_fnv1a_32bit', () => {
  const reference = (text) => {
    let hash = 2166136261n
    for (const byte of new TextEncoder().encode(text)) hash = ((hash ^ BigInt(byte)) * 16777619n) & 0xffffffffn
    return 'fnv1a:' + hash.toString(16).padStart(8, '0')
  }
  for (const text of ['', 'a', 'wanxiangshu', 'the quick brown fox jumps over the lazy dog', 'fnv1a must wrap at 32 bits']) assert.equal(digest(text), reference(text))
  assert.notEqual(digest('a'), digest('b'))
  assert.match(digest('anything'), /^fnv1a:[0-9a-f]{8}$/)
})
