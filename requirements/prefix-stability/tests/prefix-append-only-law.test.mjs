// PREFIX-STABILITY-001 / PREFIX-STABILITY-013 — the append-only provider prefix
// law. ProviderProjection is the sole authority: tools/system/identity and the
// complete message prefix all participate in the cache identity.

import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as providerProjection from '../../../dist/Participant/Provider/Projection/Surface.js'

const wire = (
  messages,
  {
    tools = ['read', 'write'],
    system = ['sys'],
    providerId = 'openai',
    modelId = 'gpt-4o',
    variant = 'deep',
  } = {},
) => ({
  modelId,
  messages,
  providerId,
  system,
  tools,
  variant,
})

const msg = (id, role, text) => ({
  parts: [{ kind: 'text', text }],
  role,
  id,
})

const textPart = fc.record({ kind: fc.constant('text'), text: fc.string({ maxLength: 48 }) })
const reasoningPart = fc.record({ kind: fc.constant('reasoning'), text: fc.string({ maxLength: 48 }) })
const toolCallPart = fc.record({
  kind: fc.constant('tool-call'),
  callId: fc.string({ maxLength: 24 }),
  name: fc.string({ maxLength: 24 }),
  args: fc.string({ maxLength: 48 }),
})
const toolResultPart = fc.record({
  kind: fc.constant('tool-result'),
  callId: fc.string({ maxLength: 24 }),
  result: fc.string({ maxLength: 48 }),
})
const mediaPart = fc.record({
  kind: fc.constant('media'),
  mediaType: fc.option(fc.string({ maxLength: 24 }), { nil: null }),
  contentDigest: fc.string({ maxLength: 48 }),
})
const wirePart = fc.oneof(textPart, reasoningPart, toolCallPart, toolResultPart, mediaPart)
const message = fc.record({
  role: fc.constantFrom('user', 'assistant'),
  parts: fc.array(wirePart, { minLength: 1, maxLength: 6 }),
})
const mutationTarget = fc
  .tuple(
    fc.constantFrom('user', 'assistant'),
    textPart,
    reasoningPart,
    toolCallPart,
    toolResultPart,
    mediaPart,
  )
  .map(([role, ...parts]) => ({ role, parts }))
const metadata = fc.record({
  tools: fc.array(fc.string({ maxLength: 16 }), { maxLength: 8 }),
  system: fc.array(fc.string({ maxLength: 32 }), { maxLength: 4 }),
  providerId: fc.string({ minLength: 1, maxLength: 16 }),
  modelId: fc.string({ minLength: 1, maxLength: 16 }),
  variant: fc.string({ maxLength: 16 }),
})

const W1 = wire([msg('m1', 'user', 'first')])
const W2 = wire([msg('m1', 'user', 'first'), msg('m2', 'assistant', 'second')])
const W3 = wire([
  msg('m1', 'user', 'first'),
  msg('m2', 'assistant', 'second'),
  msg('m3', 'user', 'third'),
])

const changed = (value) => `${value}\u0000changed`

const replacePart = (messageValue, index, replacement) => ({
  ...messageValue,
  parts: messageValue.parts.map((part, partIndex) => (partIndex === index ? replacement : part)),
})

const historicalMutations = (target) => {
  const mutations = [{ name: 'role', message: { ...target, role: changed(target.role) } }]

  target.parts.forEach((part, index) => {
    const add = (name, replacement) => mutations.push({ name, message: replacePart(target, index, replacement) })
    if (part.kind === 'text' || part.kind === 'reasoning') {
      add(`${part.kind}.text`, { ...part, text: changed(part.text) })
    } else if (part.kind === 'tool-call') {
      add('tool-call.callId', { ...part, callId: changed(part.callId) })
      add('tool-call.name', { ...part, name: changed(part.name) })
      add('tool-call.args', { ...part, args: changed(part.args) })
    } else if (part.kind === 'tool-result') {
      add('tool-result.callId', { ...part, callId: changed(part.callId) })
      add('tool-result.result', { ...part, result: changed(part.result) })
    } else {
      add('media.mediaType', {
        ...part,
        mediaType: part.mediaType === null ? 'changed' : changed(part.mediaType),
      })
      add('media.contentDigest', { ...part, contentDigest: changed(part.contentDigest) })
    }
  })

  return mutations
}

test('WHAT[PREFIX-STABILITY-001] PREFIX_STABILITY_append_only_law_holds_within_one_epoch', () => {
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W2), true, 'W1 ⊏ W2')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W3), true, 'W1 ⊏ W3')
  assert.equal(providerProjection.isAppendOnlyPrefix(W2, W3), true, 'W2 ⊏ W3')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W1), true, 'same wire is a prefix of itself')

  fc.assert(
    fc.property(
      metadata,
      fc.array(message, { maxLength: 24 }),
      fc.array(message, { minLength: 1, maxLength: 24 }),
      (identity, base, extension) => {
        assert.equal(
          providerProjection.isAppendOnlyPrefix(wire(base, identity), wire([...base, ...extension], identity)),
          true,
        )
      },
    ),
    { seed: 0x50524658, numRuns: 1_000 },
  )
})

test('WHAT[PREFIX-STABILITY-001] PREFIX_STABILITY_modified_historical_bytes_break_the_law', () => {
  const changedWire = wire([msg('m1', 'user', 'FIRST CHANGED')])
  assert.equal(providerProjection.isAppendOnlyPrefix(changedWire, W2), false, 'a changed first message is not a prefix')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, changedWire), false, 'nor is the old first message a prefix of the changed one')

  fc.assert(
    fc.property(
      metadata,
      mutationTarget,
      fc.array(message, { maxLength: 23 }),
      fc.array(message, { minLength: 1, maxLength: 4 }),
      (identity, target, historyTail, extension) => {
        const previous = [target, ...historyTail]
        assert.equal(
          providerProjection.isAppendOnlyPrefix(wire(previous, identity), wire([...previous, ...extension], identity)),
          true,
        )

        for (const mutation of historicalMutations(target)) {
          assert.equal(
            providerProjection.isAppendOnlyPrefix(
              wire(previous, identity),
              wire([mutation.message, ...historyTail, ...extension], identity),
            ),
            false,
            `historical ${mutation.name} bytes must invalidate the prefix`,
          )
        }
      },
    ),
    { seed: 0x50524659, numRuns: 1_000 },
  )
})

test('WHAT[PREFIX-STABILITY-001] historical-byte property rejects a length-only mutant with a replayable shrink path', () => {
  const lengthOnlyProperty = fc.property(metadata, mutationTarget, (identity, target) => {
    const previous = wire([target], identity)
    const next = wire([{ ...target, role: changed(target.role) }, target], identity)
    assert.equal(providerProjection.isAppendOnlyPrefix(previous, next), false)
    const lengthOnlyMutant = next.messages.length >= previous.messages.length
    assert.equal(lengthOnlyMutant, false)
  })
  const mutationResult = fc.check(lengthOnlyProperty, { seed: 0x5052465a, numRuns: 100 })

  assert.equal(mutationResult.failed, true)
  assert.equal(mutationResult.seed, 0x5052465a)
  assert.notEqual(mutationResult.counterexamplePath, '')
  assert.ok(mutationResult.counterexample.length > 0)
  assert.equal(
    fc.check(lengthOnlyProperty, {
      seed: mutationResult.seed,
      path: mutationResult.counterexamplePath,
      numRuns: 1,
    }).failed,
    true,
  )
})

test('WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_tool_set_change_breaks_the_law_even_if_messages_prefix', () => {
  const fewerTools = wire([msg('m1', 'user', 'first')], { tools: ['read'] })
  assert.equal(providerProjection.isAppendOnlyPrefix(fewerTools, W2), false, 'tools must be identical, not merely prefixed')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, fewerTools), false)
})

test('WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_identity_or_system_change_breaks_the_law', () => {
  const otherProvider = wire([msg('m1', 'user', 'first')], { providerId: 'anthropic' })
  const otherModel = wire([msg('m1', 'user', 'first')], { modelId: 'gpt-4o-mini' })
  const otherVariant = wire([msg('m1', 'user', 'first')], { variant: 'fast' })
  const otherSystem = wire([msg('m1', 'user', 'first')], { system: ['sys-2'] })

  for (const [name, other] of [
    ['provider', otherProvider],
    ['model', otherModel],
    ['variant', otherVariant],
    ['system', otherSystem],
  ]) {
    assert.equal(providerProjection.isAppendOnlyPrefix(other, W2), false, `${name} change is a cold boundary, not an append`)
  }
})

test('WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_reverse_order_is_not_a_prefix', () => {
  assert.equal(providerProjection.isAppendOnlyPrefix(W2, W1), false, 'a longer history is not a prefix of a shorter one')
  assert.equal(providerProjection.isAppendOnlyPrefix(W3, W1), false)
})

test('WHAT[PREFIX-STABILITY-011] PREFIX_STABILITY_epoch_switches_are_fact_driven_not_estimate_driven', () => {
  const drift = wire([msg('m1', 'user', 'FIRST CHANGED')])
  assert.equal(providerProjection.isAppendOnlyPrefix(drift, W2), false, 'drift is real and byte-level')

  // PrefixSurface exposes only fact-driven epoch transitions. There is no
  // estimate/limit/token/elapsed/repair channel to use as a masking switch.
  for (const forbidden of ['estimate', 'limit', 'token', 'elapsed', 'repair', 'mask', 'drift']) {
    assert.equal(typeof prefix[forbidden], 'undefined', `${forbidden} must not be an epoch API`)
  }

  const committed = prefix.applyRebase(
    {
      previousEpoch: 0,
      nextEpoch: 1,
      candidate: prefix.snapshot({
        ref: 'blob-frozen-4',
        frozenDigest: 'frozen-4',
        cutoff: 4,
        prefixDigest: 'prefix-4',
        sealRoot: 'seal-4',
        syntheticId: 'synthetic-seal-4',
      }),
    },
    prefix.empty,
  )
  assert.equal(committed.ok, true, committed.ok ? '' : committed.error)
  assert.equal(prefix.epochOf(committed.value), 1n)
  assert.ok(committed.value.snapshot)
})
