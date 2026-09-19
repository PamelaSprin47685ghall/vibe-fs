import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const xwire = await import("../../../dist/Context/Prefix/XWireSurface.js");

const textMessage = (id, role, text) => ({
  info: { id, role },
  parts: [{ type: 'text', text }],
})

test('WHAT[prefix-stability-009] prefix replacement removes covered history by stable Host identity', () => {
  const raw = [
    textMessage('covered-u', 'user', 'old user'),
    textMessage('request-local', 'assistant', 'request-local presentation only'),
    textMessage('covered-a', 'assistant', 'old answer'),
    textMessage('live-u', 'user', 'live request'),
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['covered-u', 'covered-a'],
    null,
    'y-prefix',
    'compressed canonical X',
  )

  assert.deepEqual(projected.map(item => item.info.id), ['y-prefix', 'request-local', 'live-u'])
  assert.equal(projected[1], raw[1], 'request-local presentation must survive as the same Host object')
  assert.equal(projected[2], raw[3], 'live history must survive as the same Host object')
})
test('WHAT[prefix-stability-009] stable identity replacement preserves survivor order and todowrite round objects', () => {
  const raw = [
    {
      info: { id: 'todo-call-msg', role: 'assistant' },
      parts: [{ type: 'tool-call', tool: 'todowrite', callID: 'todo-call-1', args: { planComplete: false } }],
    },
    textMessage('request-local', 'assistant', 'request-local presentation only'),
    {
      info: { id: 'todo-result-msg', role: 'tool' },
      parts: [{ type: 'tool-result', callID: 'todo-call-1', result: { ok: true } }],
    },
    textMessage('covered-ordinary', 'assistant', 'replace me'),
    textMessage('live-u', 'user', 'live request'),
  ]

  const projected = xwire.replacePrefixByHostIds(
    raw,
    ['todo-call-msg', 'todo-result-msg', 'covered-ordinary'],
    null,
    'y-prefix',
    'compressed canonical X',
  )

  assert.deepEqual(
    projected.map(item => item.info.id),
    ['y-prefix', 'todo-call-msg', 'request-local', 'todo-result-msg', 'live-u'],
  )
  assert.equal(projected[1], raw[0])
  assert.equal(projected[2], raw[1])
  assert.equal(projected[3], raw[2])
  assert.equal(projected[4], raw[4])
})
test('WHAT[prefix-stability-009] transport suppression removes only exact stale Host ids', () => {
  const retryMessage = (id, text) => ({
    info: { id, role: 'user' },
    parts: [{ type: 'text', text, metadata: { wanxiangshu_origin: 'ProviderRetryAttempt' } }],
  })
  const raw = [
    textMessage('root', 'user', 'root'),
    retryMessage('retry-old', 'retry old'),
    textMessage('business-assistant', 'assistant', 'must survive'),
    retryMessage('retry-current', 'retry current'),
  ]

  const projected = xwire.suppressHostMessagesByIds(raw, ['retry-old'])

  assert.deepEqual(projected.map(item => item.info.id), ['root', 'business-assistant', 'retry-current'])
  assert.equal(projected[0], raw[0])
  assert.equal(projected[1], raw[2], 'unaddressed assistant semantics must survive as the same Host object')
  assert.equal(projected[2], raw[3], 'current retry continuation must survive as the same Host object')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { resolve } = await import("node:path");
const { default: test } = await import("node:test");
const providerCodec = await import("../../../dist/OpenCode/Codec/ProviderProjectionSurface.js");
const providerProjection = await import("../../../dist/Participant/Provider/Projection/Surface.js");
const XWireSurface = await import("../../../dist/Context/Prefix/XWireSurface.js");

const semanticView = (raw) => providerProjection.semanticProjection(providerCodec.decodeMessageView(raw).messages)
const stage2Snapshot = (raw, committed = null) => ({
  currentProjection: semanticView(raw),
  committedPrefix: committed,
})

test('WHAT[prefix-stability-009] CTX_011_step5_cutoff_digest_truncates_exactly_at_the_cutoff', () => {
  const snapshot = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'first' }] },
    { info: { id: 'm2', role: 'assistant' }, parts: [{ type: 'text', text: 'second' }] },
    { info: { id: 'm3', role: 'user' }, parts: [{ type: 'text', text: 'third' }] },
  ])

  const truncated = {
    ...snapshot.currentProjection,
    messages: snapshot.currentProjection.messages.slice(0, 2),
  }
  assert.equal(
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 2),
    XWireSurface.coveredPrefixDigest(truncated, 2),
  )
  assert.notEqual(
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 2),
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 99),
    'a real cutoff changes the digest',
  )

  // cutoff 0 proves the EMPTY prefix — the load-bearing CTX-011 step-5 shape.
  const empty = { ...snapshot.currentProjection, messages: [] }
  assert.equal(
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 0),
    XWireSurface.coveredPrefixDigest(empty, 0),
  )

  // An out-of-range cutoff keeps every message; the selector clamps before this
  // proof is requested, but the proof itself remains total.
  assert.equal(
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, 99),
    XWireSurface.coveredPrefixDigest(snapshot.currentProjection, snapshot.currentProjection.messages.length),
  )
})
test('WHAT[prefix-stability-009] CTX_011_step5_the_proof_reads_the_SNAPSHOT_not_a_stale_closure', () => {
  // The digest must be recomputed from X's CURRENT projection each attempt — a
  // closure captured once would re-prove yesterday's numbering.
  const before = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'old' }] },
  ])
  const after = stage2Snapshot([
    { info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'old' }] },
    { info: { id: 'm2', role: 'user' }, parts: [{ type: 'text', text: 'new' }] },
  ])

  // Same cutoff over a 1-message and a 2-message projection: the grown one keeps
  // its second message, so the proof cannot be the same.
  assert.notEqual(
    XWireSurface.coveredPrefixDigest(before.currentProjection, 2),
    XWireSurface.coveredPrefixDigest(after.currentProjection, 2),
    'the same cutoff over a grown projection must not produce the same proof',
  )
})
test('WHAT[prefix-stability-009] prefix_proof_and_writeback_use_canonical_XTrace_not_request_local_message_positions', () => {
  const wireSource = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Context/Prefix/Wire.fs'),
    'utf8',
  )
  const adapterSource = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Composition/Durable/AgentJournalPortAdapter.fs'),
    'utf8',
  )

  // Wire reads canonical XTrace through the port; the adapter is where the
  // canonical materialization call lives (it alone knows AgentJournal).
  assert.match(wireSource, /CurrentProjection/)
  assert.match(adapterSource, /XTraceMaterialization\.currentProjection/)
  assert.match(wireSource, /XTraceProjection\.tryTurnOfHostMessageId/)
  assert.match(wireSource, /XTraceProjection\.hostMessageIdsBeforeTurn/)
  assert.match(wireSource, /replacePrefixByHostIds/)
  assert.doesNotMatch(
    wireSource,
    /ProviderWireCapture\.decodeMessageView\(rawMessages\)[\s\S]{0,500}ProjectionRenderer\.cutoffDigest/,
    'step-5 proof must not hash the mutable request presentation',
  )
})
test('WHAT[prefix-stability-009] prefix lifecycle and rendering stay with the prefix owner', () => {
  const ownerSource = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Context/Prefix/Projection.fs'),
    'utf8',
  )
  const providerIntentSource = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Participant/Provider/Projection/Intent.fs'),
    'utf8',
  )
  const providerRendererSource = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Participant/Provider/Projection/Renderer.fs'),
    'utf8',
  )

  assert.match(ownerSource, /type PrefixActivation/)
  assert.match(ownerSource, /type PrefixProjectionIntent/)
  assert.match(ownerSource, /type PrefixRendered/)
  assert.match(ownerSource, /let render \(intent: PrefixProjectionIntent\)/)
  assert.doesNotMatch(providerIntentSource, /PrefixActivation|KeepPhysicalPrefix|ActivatePrefixEpoch|ReanchorAfterCompaction/)
  assert.doesNotMatch(providerRendererSource, /PrefixActivation|RenderedPrefix|renderPrefix/)
})
test('WHAT[prefix-stability-009] retry_transport_rows_retire_only_at_a_real_cold_horizon', () => {
  const wireSource = readFileSync(
    resolve(import.meta.dirname, '../../../src/Wanxiangshu/Context/Prefix/Wire.fs'),
    'utf8',
  )

  assert.match(wireSource, /let private staleProviderRetryMessageIds/)
  assert.match(wireSource, /Some messageId <> currentPhysical/)
  assert.match(wireSource, /let retryTransportRetirement/)
  assert.match(
    wireSource,
    /PrefixPresentationHorizon\.Current\s*->\s*Set\.empty[\s\S]*?PrefixPresentationHorizon\.TentativeCold\s*->\s*staleProviderRetryMessageIds rawMessages/,
    'same-horizon retry rows must remain byte-stable; only a real cold presentation may retire them',
  )
  assert.match(
    wireSource,
    /renderPrefixMessages state rawMessages PrefixProjectionIntent\.Keep PrefixPresentationHorizon\.Current/,
    'ordinary presentation must preserve the current physical prefix',
  )
  assert.match(wireSource, /renderPrefixMessages state rawMessages prefixIntent presentationHorizon/)
  assert.match(wireSource, /presentationHorizonForProbe/)
  assert.match(wireSource, /XPrefixProjection\.render intent/)
  assert.match(wireSource, /ProjectionMessageEdit\.suppressHostMessagesByIds prefixed staleTransport/)
  assert.doesNotMatch(wireSource, /ProjectionIntent\.(?:SuppressTransportOnly|ReanchorAfterCompaction)/)
  assert.doesNotMatch(wireSource, /Projection(?:Planner\.plan|Renderer\.renderPrefix)/)
})
test('WHAT[prefix-stability-009] compiled retry retirement preserves Current and only removes stale retry rows at TentativeCold', () => {
  const retry = (id) => ({
    info: { id, role: 'user', metadata: { wanxiangshu_origin: 'ProviderRetryAttempt' } },
    parts: [{ type: 'text', text: id }],
  })
  const ordinary = {
    info: { id: 'ordinary-1', role: 'user' },
    parts: [{ type: 'text', text: 'ordinary' }],
  }
  const messages = [retry('retry-stale'), ordinary, retry('retry-current')]

  assert.deepEqual(XWireSurface.retiredRetryMessageIds('Current', messages), [])
  assert.deepEqual(
    XWireSurface.retiredRetryMessageIds('TentativeCold', messages),
    ['retry-stale'],
    'the current physical retry remains on the new cold horizon',
  )
})
}
