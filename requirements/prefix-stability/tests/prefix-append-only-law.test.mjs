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
import { prefixEpochProjection as prefix, providerProjection, toList } from '../../verification-system/tests/support/domain.mjs'

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

test('WHAT[PREFIX-STABILITY-001] PREFIX_STABILITY_append_only_law_holds_within_one_epoch', () => {
  assert.equal(isAppendOnlyPrefix(W1, W2), true, 'W1 ⊏ W2')
  assert.equal(isAppendOnlyPrefix(W1, W3), true, 'W1 ⊏ W3')
  assert.equal(isAppendOnlyPrefix(W2, W3), true, 'W2 ⊏ W3')
  assert.equal(isAppendOnlyPrefix(W1, W1), true, 'same wire is a prefix of itself')
})

test('WHAT[PREFIX-STABILITY-001] PREFIX_STABILITY_modified_historical_bytes_break_the_law', () => {
  const changed = wire(decodeMessages([msg('m1', 'user', 'FIRST CHANGED')]))
  assert.equal(isAppendOnlyPrefix(changed, W2), false, 'a changed first message is not a prefix')
  assert.equal(isAppendOnlyPrefix(W1, changed), false, 'nor is the old first message a prefix of the changed one')
})

test('WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_tool_set_change_breaks_the_law_even_if_messages_prefix', () => {
  // A changed tool set invalidates the KV cache entirely; treating it as an
  // append would report a cache hit the provider will not honour.
  const fewerTools = wire(decodeMessages([msg('m1', 'user', 'first')]), { tools: ['read'] })
  assert.equal(isAppendOnlyPrefix(fewerTools, W2), false, 'tools must be identical, not merely prefixed')
  assert.equal(isAppendOnlyPrefix(W1, fewerTools), false)
})

test('WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_identity_or_system_change_breaks_the_law', () => {
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

test('WHAT[PREFIX-STABILITY-013] PREFIX_STABILITY_reverse_order_is_not_a_prefix', () => {
  assert.equal(isAppendOnlyPrefix(W2, W1), false, 'a longer history is not a prefix of a shorter one')
  assert.equal(isAppendOnlyPrefix(W3, W1), false)
})

test('WHAT[PREFIX-STABILITY-011] PREFIX_STABILITY_epoch_switches_are_fact_driven_not_estimate_driven', () => {
  // HOST-013 行为约束 5：同一 epoch 内的前缀漂移不得用 PrefixEpoch 切换掩盖；
  // 禁止读 limit、做 token 估算（CTX-002）。投影侧的证明：epoch 切换
  // （applyRebase/applyReanchor）只接受事实输入（epoch 序 + candidate snapshot
  // / observedRun），没有任何 wire 字节、limit、token、elapsed 或「修补」通道——
  // 漂移在投影层无处可藏，只能由 transform 的 append-only 纪律负责（H13 系）。
  const drift = wire(decodeMessages([msg('m1', 'user', 'FIRST CHANGED')]))
  assert.equal(isAppendOnlyPrefix(drift, W2), false, 'drift is real and byte-level')

  // 无估算/修补通道：与 CTX-010 的「无 rollback 类别」同一断言模式，投影的
  // 输入面就是它暴露的键集合；出现估算键即证明 epoch 可被非事实驱动。
  const forbidden = ['estimate', 'limit', 'token', 'elapsed', 'repair', 'mask', 'drift']
  assert.deepEqual(
    prefix.forbiddenApiFragments(forbidden),
    [],
    'projection must expose no estimate/limit/token/repair channel for epoch switching',
  )

  // 提交一个新 epoch 也不改写已呈现字节：rebase 输入是 candidate 事实
  // （cutoff/digest/seal），不是 wire 内容——漂移的 wire 与 epoch 状态无关。
  const committed = prefix.applyRebase(
    { previousEpoch: 0, nextEpoch: 1, candidate: prefix.snapshot({ ref: 'blob-frozen-4', digest: 'frozen-4', cutoff: 4, prefixDigest: 'prefix-4', sealRoot: 'seal-4', syntheticId: 'synthetic-seal-4' }) },
    prefix.empty,
  )
  assert.equal(committed.ok, true, committed.ok ? '' : committed.error)
  assert.equal(prefix.epochOf(committed.value), 1n)
  assert.equal(prefix.hasSnapshot(committed.value), true)
})
