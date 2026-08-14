// PREFIX-STABILITY-001 / PREFIX-STABILITY-013 — the append-only provider prefix
// law (ARCH-004 / cache.md PREFIX LAW), with `ProviderProjection.isAppendOnlyPrefix`
// as the sole authority (cache.md §1: no second "almost a prefix" helper).
//
// The decisive cases: append within one epoch holds; a changed tool set breaks
// the law even when the message sequence would otherwise prefix; a changed
// identity (provider/model/variant) breaks it; modified historical bytes break
// it; the reverse order is not a prefix.

import assert from 'node:assert/strict'
import test from 'node:test'
import { providerProjection, toList } from '../../verification-system/tests/support/domain.mjs'

const { isAppendOnlyPrefix } = providerProjection

const decodeMessages = (raw) => providerProjection.decodeMessageView(toList(raw)).Messages

const wire = (messages, { tools = ['read', 'write'], system = ['sys'], provider = 'openai', model = 'gpt-4o', variant = 'deep' } = {}) => ({
  ProviderId: provider,
  ModelId: model,
  Variant: variant,
  System: system,
  Tools: tools,
  Messages: messages,
})

const msg = (id, role, text) => ({ info: { id, role }, parts: [{ type: 'text', text }] })

const W1 = wire(decodeMessages([msg('m1', 'user', 'first')]))
const W2 = wire(
  decodeMessages([
    msg('m1', 'user', 'first'),
    msg('m2', 'assistant', 'second'),
  ]),
)
const W3 = wire(
  decodeMessages([
    msg('m1', 'user', 'first'),
    msg('m2', 'assistant', 'second'),
    msg('m3', 'user', 'third'),
  ]),
)

test('PREFIX_STABILITY_append_only_law_holds_within_one_epoch', () => {
  assert.equal(isAppendOnlyPrefix(W1, W2), true, 'W1 ⊏ W2')
  assert.equal(isAppendOnlyPrefix(W1, W3), true, 'W1 ⊏ W3')
  assert.equal(isAppendOnlyPrefix(W2, W3), true, 'W2 ⊏ W3')
  assert.equal(isAppendOnlyPrefix(W1, W1), true, 'same wire is a prefix of itself')
})

test('PREFIX_STABILITY_modified_historical_bytes_break_the_law', () => {
  const changed = wire(decodeMessages([msg('m1', 'user', 'FIRST CHANGED')]))
  assert.equal(isAppendOnlyPrefix(changed, W2), false, 'a changed first message is not a prefix')
  assert.equal(isAppendOnlyPrefix(W1, changed), false, 'nor is the old first message a prefix of the changed one')
})

test('PREFIX_STABILITY_tool_set_change_breaks_the_law_even_if_messages_prefix', () => {
  // A changed tool set invalidates the KV cache entirely; treating it as an
  // append would report a cache hit the provider will not honour.
  const fewerTools = wire(decodeMessages([msg('m1', 'user', 'first')]), { tools: ['read'] })
  assert.equal(isAppendOnlyPrefix(fewerTools, W2), false, 'tools must be identical, not merely prefixed')
  assert.equal(isAppendOnlyPrefix(W1, fewerTools), false)
})

test('PREFIX_STABILITY_identity_or_system_change_breaks_the_law', () => {
  const otherProvider = wire(decodeMessages([msg('m1', 'user', 'first')]), { provider: 'anthropic' })
  const otherModel = wire(decodeMessages([msg('m1', 'user', 'first')]), { model: 'gpt-4o-mini' })
  const otherVariant = wire(decodeMessages([msg('m1', 'user', 'first')]), { variant: 'fast' })
  const otherSystem = wire(decodeMessages([msg('m1', 'user', 'first')]), { system: ['sys-2'] })

  for (const [name, other] of [
    ['provider', otherProvider],
    ['model', otherModel],
    ['variant', otherVariant],
    ['system', otherSystem],
  ]) {
    assert.equal(isAppendOnlyPrefix(other, W2), false, `${name} change is a cold boundary, not an append`)
  }
})

test('PREFIX_STABILITY_reverse_order_is_not_a_prefix', () => {
  assert.equal(isAppendOnlyPrefix(W2, W1), false, 'a longer history is not a prefix of a shorter one')
  assert.equal(isAppendOnlyPrefix(W3, W1), false)
})
