// PREFIX-STABILITY-001 / PREFIX-STABILITY-013 — the append-only provider prefix
// law. ProviderProjection is the sole authority: tools/system/identity and the
// complete message prefix all participate in the cache identity.

import assert from 'node:assert/strict'
import test from 'node:test'

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

const W1 = wire([msg('m1', 'user', 'first')])
const W2 = wire([msg('m1', 'user', 'first'), msg('m2', 'assistant', 'second')])
const W3 = wire([
  msg('m1', 'user', 'first'),
  msg('m2', 'assistant', 'second'),
  msg('m3', 'user', 'third'),
])

test('WHAT[PREFIX-STABILITY-001] PREFIX_STABILITY_append_only_law_holds_within_one_epoch', () => {
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W2), true, 'W1 ⊏ W2')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W3), true, 'W1 ⊏ W3')
  assert.equal(providerProjection.isAppendOnlyPrefix(W2, W3), true, 'W2 ⊏ W3')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, W1), true, 'same wire is a prefix of itself')
})

test('WHAT[PREFIX-STABILITY-001] PREFIX_STABILITY_modified_historical_bytes_break_the_law', () => {
  const changed = wire([msg('m1', 'user', 'FIRST CHANGED')])
  assert.equal(providerProjection.isAppendOnlyPrefix(changed, W2), false, 'a changed first message is not a prefix')
  assert.equal(providerProjection.isAppendOnlyPrefix(W1, changed), false, 'nor is the old first message a prefix of the changed one')
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
