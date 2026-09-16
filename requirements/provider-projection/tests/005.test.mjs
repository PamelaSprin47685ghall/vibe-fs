import assert from 'node:assert/strict'
import test from 'node:test'
import * as Projection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as toolModule from '@opencode-ai/plugin/tool'

const message = (role, text) => ({ role, parts: [{ kind: 'text', text }] })
const textMessage = message
const emptySnapshot = () => Projection.projectionSnapshot(Projection.semanticProjection([]))
const row = (role, text, hostMessageId = null, hostIsPhysical = false) => ({
  message: message(role, text),
  hostMessageId,
  hostIsPhysical,
})

const H = (text) => `H(${text})`
const snapshot = (messages = []) => Projection.projectionSnapshot(Projection.semanticProjection(messages))
const base = (key, rows) => Projection.replaceMessageBase({ key, rows })
const insert = (key, anchor, rows) => Projection.insertMessageRows({ key, anchor, rows })
const before = (index) => ({ kind: 'BeforeMessageIndex', index })
const append = { kind: 'Append' }

// Host tool codec semantics through its owner surface.

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

test('WHAT[PROVIDER-PROJECTION-005] generic message intents replace feature-owned constructors', () => {
  assert.equal(typeof Projection.replaceMessageBase, 'function')
  assert.equal(typeof Projection.insertMessageRows, 'function')
  assert.equal(Projection.useStrengthMirror, undefined)
  assert.equal(Projection.strengthCandidate, undefined)
  assert.equal(Projection.strengthPromoted, undefined)
  assert.equal(Projection.strengthReplicaLocal, undefined)
})

test('WHAT[PROVIDER-PROJECTION-005] surface exposes only generic projection constructors', () => {
  assert.equal(typeof Projection.replaceMessageBase, 'function')
  assert.equal(typeof Projection.insertMessageRows, 'function')
  assert.equal(typeof Projection.plan, 'function')
  assert.equal(typeof Projection.renderMessagesWithHostIds, 'function')
  assert.equal(Projection.keepPhysicalPrefix, undefined)
  assert.equal(Projection.activatePrefixEpoch, undefined)
  assert.equal(Projection.insertBlogFrames, undefined)
  assert.equal(Projection.insertRepair, undefined)
  assert.equal(Projection.useStrengthMirror, undefined)
})

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
