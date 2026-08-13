// tests/unit/plugin/tool-host-codec-full.test.mjs — VERIFY-009 coverage: the one
// dynamic JS boundary for tool definitions and invocations.
//
// HostToolArguments decoding, the schema DSL over a fake factory, register/registry/
// hide against a fake Host `tool`, promptText / attachAbort context paths, the TOML
// renderers, looksLikeHandleId and the fnv1a digest.

import assert from 'node:assert/strict'
import test from 'node:test'

import { readdirSync } from 'node:fs'
import { join } from 'node:path'

import { listItems, toList } from '../support/domain.mjs'

const fableLibraryDir = join(
  process.cwd(),
  'dist',
  'fable_modules',
  readdirSync('dist/fable_modules').find((entry) => entry.startsWith('fable-library-js.')),
)
const { curry2 } = await import(join(fableLibraryDir, 'Util.js'))

// Record function fields are emitted uncurried by Fable; curry2 recovers the
// original curried function from the `curried` WeakMap.
const attachAbort = (ctx, callback) => curry2(ctx.AttachAbort)(callback)

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolArguments__OptionalNumber_Z721C83C5: optionalNumber,
  HostToolArguments__OptionalText_Z721C83C5: optionalText,
  HostToolArguments__OptionalTexts_Z721C83C5: optionalTexts,
  HostToolArguments__Text_Z721C83C5: argText,
  ToolHostCodec_decodeContext: decodeContext,
  ToolHostCodec_digest: digest,
  ToolHostCodec_enumSchema: enumSchema,
  ToolHostCodec_enumSchemaDescribed: enumSchemaDescribed,
  ToolHostCodec_factory: makeFactory,
  ToolHostCodec_hide: hide,
  ToolHostCodec_looksLikeHandleId: looksLikeHandleId,
  ToolHostCodec_managedOrHandleSchema: managedOrHandleSchema,
  ToolHostCodec_numberSchema: numberSchema,
  ToolHostCodec_optionalEnumSchema: optionalEnumSchema,
  ToolHostCodec_optionalEnumSchemaDescribed: optionalEnumSchemaDescribed,
  ToolHostCodec_optionalNumberSchema: optionalNumberSchema,
  ToolHostCodec_optionalStringArraySchema: optionalStringArraySchema,
  ToolHostCodec_optionalStringSchema: optionalStringSchema,
  ToolHostCodec_optionalStringSchemaDescribed: optionalStringSchemaDescribed,
  ToolHostCodec_register: register,
  ToolHostCodec_registry: registry,
  ToolHostCodec_stringSchema: stringSchema,
  ToolHostCodec_stringSchemaDescribed: stringSchemaDescribed,
  ToolHostCodec_tomlObject: tomlObject,
  ToolHostCodec_tomlObjectWithInstructions: tomlObjectWithInstructions,
  ToolHostCodec_tomlTable: tomlTable,
  ToolHostCodec_TomlValue,
  ToolSpec,
} = await import('../../../dist/Infrastructure/OpenCode/Codec/ToolHostCodec.js')

// ── HostToolArguments ────────────────────────────────────────────────────────

test('CODEC_arguments_text_reads_present_and_missing', () => {
  const args = makeArgs({ name: 'value', blank: '  ' })
  assert.equal(argText(args, 'name'), 'value')
  assert.equal(argText(args, 'missing'), '')
  assert.equal(argText(args, 'blank'), '  ')
})

test('CODEC_arguments_optional_text_filters_blank', () => {
  const args = makeArgs({ name: 'value', blank: '  ' })
  assert.equal(optionalText(args, 'name'), 'value')
  assert.equal(optionalText(args, 'blank'), undefined)
  assert.equal(optionalText(args, 'missing'), undefined)
})

test('CODEC_arguments_optional_texts_collects_nonempty_strings', () => {
  const args = makeArgs({ items: ['a', '', '  ', 'b', null, 7] })
  assert.deepEqual(listItems(optionalTexts(args, 'items')), ['a', 'b', '7'])
  assert.equal(optionalTexts(args, 'missing'), undefined)
  // A non-array value fails closed to None.
  const notArray = makeArgs({ items: 'plain' })
  assert.equal(optionalTexts(notArray, 'items'), undefined)
})

test('CODEC_arguments_optional_number_reads_floats', () => {
  const args = makeArgs({ timeout: 2.5, name: 'x' })
  assert.equal(optionalNumber(args, 'timeout'), 2.5)
  assert.equal(optionalNumber(args, 'missing'), undefined)
  assert.equal(optionalNumber(args, 'name'), undefined, 'a string is not a number')
})

test('CODEC_arguments_null_raw_is_all_absent', () => {
  const args = makeArgs(null)
  assert.equal(argText(args, 'x'), '')
  assert.equal(optionalText(args, 'x'), undefined)
  assert.equal(optionalTexts(args, 'x'), undefined)
  assert.equal(optionalNumber(args, 'x'), undefined)
})

// ── schema DSL over a fake factory ───────────────────────────────────────────

test('CODEC_schema_dsl_builds_each_shape', () => {
  const toolModule = {
    tool: {
      schema: {
        string: () => ({
          schema: 'string',
          describe: (description) => ({
            schema: 'string-described',
            description,
            optional: () => ({ schema: 'string-described-optional', description }),
          }),
          optional: () => ({ schema: 'string-optional' }),
        }),
        number: () => ({ schema: 'number', optional: () => ({ schema: 'number-optional' }) }),
        enum: (values) => ({
          describe: (description) => ({
            optional: () => ({ schema: 'enum-described-optional', values, description }),
            value: { schema: 'enum-described', values, description },
          }),
          optional: () => ({ schema: 'enum-optional', values }),
          value: { schema: 'enum', values },
        }),
        array: (inner) => ({ schema: 'array', inner, optional: () => ({ schema: 'array-optional', inner }) }),
        union: (parts) => ({ schema: 'union', parts }),
      },
    },
  }
  const factory = makeFactory(toolModule)

  const unwrap = (hostSchema) => hostSchema.fields[0]

  assert.equal(unwrap(stringSchema(factory)).schema, 'string')
  const describedString = unwrap(stringSchemaDescribed('program source', factory))
  assert.equal(describedString.schema, 'string-described')
  assert.equal(describedString.description, 'program source')

  const optStringDescribed = unwrap(optionalStringSchemaDescribed('hints', factory))
  assert.equal(optStringDescribed.schema, 'string-described-optional')
  assert.equal(optStringDescribed.description, 'hints')
  assert.equal(unwrap(numberSchema(factory)).schema, 'number')

  const described = unwrap(enumSchemaDescribed(toList(['a', 'b']), 'pick one', factory))
  assert.equal(described.value.schema, 'enum-described')

  const plain = unwrap(enumSchema(toList(['x']), factory))
  assert.equal(plain.value.schema, 'enum')

  const optional = unwrap(optionalEnumSchema(toList(['y']), factory))
  assert.deepEqual(optional, { schema: 'enum-optional', values: ['y'] })

  const optionalDescribed = unwrap(optionalEnumSchemaDescribed(toList(['z']), 'maybe', factory))
  assert.deepEqual(optionalDescribed, { schema: 'enum-described-optional', values: ['z'], description: 'maybe' })

  const managed = unwrap(managedOrHandleSchema(toList(['fast-coder']), factory))
  assert.equal(managed.schema, 'union')

  const optString = unwrap(optionalStringSchema(factory))
  assert.deepEqual(optString, { schema: 'string-optional' })

  const optNumber = unwrap(optionalNumberSchema(factory))
  assert.deepEqual(optNumber, { schema: 'number-optional' })

  const optArray = unwrap(optionalStringArraySchema(factory))
  assert.equal(optArray.schema, 'array-optional')
})

// ── register / registry / hide ───────────────────────────────────────────────

test('CODEC_register_applies_tool_with_uncurried_execute_and_bounds_result', async () => {
  const registrations = []
  const fakeTool = (definition) => {
    registrations.push(definition)
    return { registered: definition.description, execute: definition.execute }
  }
  const factory = makeFactory({ tool: fakeTool })

  const spec = new ToolSpec('demo', 'a demo tool', [], async (_args, _ctx) => 'x'.repeat(60000))
  const registered = register(factory, spec)

  assert.equal(registrations.length, 1)
  assert.equal(registrations[0].description, 'a demo tool')

  // Execute is uncurried (args, context) and the result passes ToolResultBound.
  const output = await registered.execute({}, { sessionID: 'ses_demo' })
  assert.ok(output.length < 60000, 'output must be bounded, not the raw 60k string')
})

test('CODEC_registry_maps_specs_by_name', () => {
  const factory = makeFactory({ tool: (definition) => ({ def: definition }) })
  const first = new ToolSpec('one', 'first', [], async () => '1')
  const second = new ToolSpec('two', 'second', [], async () => '2')

  const built = registry(factory, toList([first, second]))
  assert.ok(built.one, 'registry must key by spec name')
  assert.ok(built.two)
})

test('CODEC_hide_defines_non_enumerable_property', () => {
  const target = {}
  hide(target, 'secret', () => 'hidden')
  assert.equal(target.secret(), 'hidden')
  assert.equal(Object.keys(target).includes('secret'), false, 'hidden entries stay off enumeration')
})

// ── decodeContext promptText / attachAbort ───────────────────────────────────

test('CODEC_prompt_text_prefers_message_parts', () => {
  const ctx = decodeContext({
    sessionID: 'ses_p',
    message: { parts: [{ text: 'hello ' }, { text: 'world' }] },
    prompt: 'fallback prompt',
  })
  assert.equal(ctx.PromptText, 'hello world')
})

test('CODEC_prompt_text_falls_back_to_prompt_then_input', () => {
  const fromPrompt = decodeContext({ sessionID: 's', prompt: 'the prompt' })
  assert.equal(fromPrompt.PromptText, 'the prompt')

  const fromInput = decodeContext({ sessionID: 's', input: 'the input' })
  assert.equal(fromInput.PromptText, 'the input')

  const none = decodeContext({ sessionID: 's' })
  assert.equal(none.PromptText, undefined)
})

test('CODEC_prompt_text_blank_parts_fall_through', () => {
  const ctx = decodeContext({
    sessionID: 's',
    message: { parts: [{ text: '   ' }] },
    prompt: 'real prompt',
  })
  assert.equal(ctx.PromptText, 'real prompt')
})

test('CODEC_attach_abort_without_signal_is_noop_unsubscribe', () => {
  const ctx = decodeContext({ sessionID: 's' })
  let fired = false
  const unsubscribe = attachAbort(ctx, () => {
    fired = true
  })
  unsubscribe()
  assert.equal(fired, false)
})

test('CODEC_attach_abort_fires_immediately_on_aborted_signal', () => {
  const signal = {
    aborted: true,
    addEventListener: () => {},
    removeEventListener: () => {},
  }
  const ctx = decodeContext({ sessionID: 's', abort: signal })
  let fired = false
  attachAbort(ctx, () => {
    fired = true
  })
  assert.equal(fired, true, 'an already-aborted signal must fire the callback immediately')
})

test('CODEC_attach_abort_subscribes_and_unsubscribes', () => {
  const listeners = []
  const signal = {
    aborted: false,
    addEventListener: (name, listener, _opts) => listeners.push([name, listener]),
    removeEventListener: (name, listener) => {
      const index = listeners.findIndex(([n, l]) => n === name && l === listener)
      if (index >= 0) listeners.splice(index, 1)
    },
  }
  const ctx = decodeContext({ sessionID: 's', abortSignal: signal })
  let fired = false
  const unsubscribe = attachAbort(ctx, () => {
    fired = true
  })
  assert.equal(listeners.length, 1)

  listeners[0][1]()
  assert.equal(fired, true)

  unsubscribe()
  assert.equal(listeners.length, 0, 'unsubscribe removes the listener')
})

// ── TOML renderers ───────────────────────────────────────────────────────────

test('CODEC_toml_object_renders_scalar_fields', () => {
  const text = tomlObject(
    toList([
      ['name', new ToolHostCodec_TomlValue(0, ['demo'])],
      ['count', new ToolHostCodec_TomlValue(1, [3])],
      ['big', new ToolHostCodec_TomlValue(2, [9n])],
      ['flag', new ToolHostCodec_TomlValue(3, [true])],
    ]),
  )
  assert.match(text, /name = "demo"/)
  assert.match(text, /count = 3/)
  assert.match(text, /big = 9/)
  assert.match(text, /flag = true/)
})

test('CODEC_toml_object_renders_nested_table', () => {
  const text = tomlObject(
    toList([['meta', new ToolHostCodec_TomlValue(4, [toList([['key', new ToolHostCodec_TomlValue(0, ['v'])]])])]]),
  )
  assert.match(text, /\[meta\]/)
  assert.match(text, /key = "v"/)
})

test('CODEC_toml_object_with_instructions_prepends_them', () => {
  const text = tomlObjectWithInstructions(toList(['do this first']), toList([['name', new ToolHostCodec_TomlValue(0, ['demo'])]]))
  assert.ok(text.indexOf('do this first') < text.indexOf('name = "demo"'))
})

test('CODEC_toml_table_renders_array_of_tables', () => {
  const text = tomlTable(
    'item',
    toList([
      toList([['id', new ToolHostCodec_TomlValue(0, ['a'])]]),
      toList([['id', new ToolHostCodec_TomlValue(0, ['b'])]]),
    ]),
  )
  const blocks = text.match(/\[\[item\]\]/g) ?? []
  assert.equal(blocks.length, 2)
})

// ── looksLikeHandleId / digest ───────────────────────────────────────────────

test('CODEC_looks_like_handle_id_shape', () => {
  assert.equal(looksLikeHandleId('ab12cd'), true)
  assert.equal(looksLikeHandleId('zz9900'), true)
  assert.equal(looksLikeHandleId('ab12'), false, 'too short')
  assert.equal(looksLikeHandleId('ab12cdef'), false, 'too long')
  assert.equal(looksLikeHandleId('AB12CD'), false, 'uppercase rejected')
  assert.equal(looksLikeHandleId('ab-12c'), false, 'punctuation rejected')
  assert.equal(looksLikeHandleId(''), false)
  assert.equal(looksLikeHandleId('      '), false)
})

test('CODEC_digest_is_true_fnv1a_32bit', () => {
  // Independent reference implementation: 32-bit wrapping multiply (BigInt).
  const reference = (text) => {
    let hash = 2166136261n
    for (const byte of new TextEncoder().encode(text)) {
      hash = ((hash ^ BigInt(byte)) * 16777619n) & 0xffffffffn
    }
    return 'fnv1a:' + hash.toString(16).padStart(8, '0')
  }

  for (const text of ['', 'a', 'wanxiangshu', 'the quick brown fox jumps over the lazy dog', 'fnv1a must wrap at 32 bits']) {
    assert.equal(digest(text), reference(text), `digest('${text}') must be true FNV-1a 32-bit`)
  }
  assert.notEqual(digest('a'), digest('b'))
  assert.match(digest('anything'), /^fnv1a:[0-9a-f]{8}$/)
})
