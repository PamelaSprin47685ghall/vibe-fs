import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const xwire = await import("../../../dist/Context/Prefix/XWireSurface.js");

const textMessage = (id, role, text) => ({
  info: { id, role },
  parts: [{ type: 'text', text }],
})

test('WHAT[prefix-stability-009] prefix replacement removes ordinary covered history but preserves covered user messages by stable Host identity', () => {
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

  assert.deepEqual(projected.map(item => item.info.id), ['y-prefix', 'covered-u', 'request-local', 'live-u'])
  assert.equal(projected[1], raw[0], 'covered user history must survive as the same Host object')
  assert.equal(projected[2], raw[1], 'request-local presentation must survive as the same Host object')
  assert.equal(projected[3], raw[3], 'live history must survive as the same Host object')
})
test('WHAT[prefix-stability-009] stable identity replacement grants no per-tool raw-history exemption', () => {
  const raw = [
    {
      info: { id: 'todo-call-msg', role: 'assistant' },
      parts: [{ type: 'tool-call', tool: 'todowrite', callID: 'todo-call-1', args: { todos: [], retainCheckpoints: 1 } }],
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
    ['y-prefix', 'request-local', 'live-u'],
  )
  assert.equal(projected[1], raw[1])
  assert.equal(projected[2], raw[4])
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
test.todo('WHAT[prefix-stability-009] actual Host transform derives proof and writeback identities from canonical XTrace despite request-local presentation changes; GAP-106')
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

{
const assert = (await import('node:assert/strict')).default
const { createHash } = await import('node:crypto')
const xwire = await import('../../../dist/Context/Prefix/XWireSurface.js')
const sha256 = (text) => createHash('sha256').update(text).digest('hex')

test('WHAT[prefix-stability-009] actual candidate pipeline rejects changed covered history but accepts an unchanged prefix with a different tail', async () => {
  const currentProjection = {
    messages: [
      { role: 'user', parts: [{ kind: 'text', text: 'task α' }] },
      { role: 'assistant', parts: [{ kind: 'text', text: 'completed\r\nwork' }] },
      { role: 'user', parts: [{ kind: 'text', text: 'live request' }] },
    ],
  }
  const frame = 'recorded work α'
  const written = []
  const input = {
    sessionId: 'ses-prefix-proof', prefixEpoch: 4, frameEpoch: 2,
    currentProjection, coverableCutoff: 2, requestCutoff: 2,
    coveredDigest: xwire.coveredPrefixDigest(currentProjection, 2),
    frames: [{
      kind: 'Entry',
      ref: 'frame-ref',
      digest: sha256(frame),
      coveredFrom: 0,
      coveredThrough: 2,
      cutoff: 2,
    }],
    port: {
      readBlob: async (ref) => {
        assert.equal(ref, 'frame-ref')
        return { ok: true, value: frame }
      },
      writeBlob: async (body) => {
        written.push(body)
        return { ok: true, value: { blobRef: `blobs/${sha256(body)}`, blobDigest: sha256(body) } }
      },
    },
  }
  const accepted = await xwire.candidateFromJournal(input)
  assert.equal(accepted.ok, true, accepted.error)
  assert.equal(accepted.probe.candidate.cutoff, 2)
  assert.equal(accepted.probe.candidate.prefixDigest, input.coveredDigest)
  assert.equal(written.length, 1)
  assert.ok(written[0].includes(frame))

  const changedTail = structuredClone(currentProjection)
  changedTail.messages[2].parts[0].text = 'new live request'
  const tailResult = await xwire.candidateFromJournal({ ...input, currentProjection: changedTail })
  assert.equal(tailResult.ok, true, tailResult.error)
  assert.deepEqual(tailResult.probe, accepted.probe)

  const changedHistory = structuredClone(currentProjection)
  changedHistory.messages[1].parts[0].text = 'different completed work'
  const refused = await xwire.candidateFromJournal({ ...input, currentProjection: changedHistory })
  assert.equal(refused.ok, false)
  assert.equal(refused.probe, null)
  assert.match(refused.error, /cutoff proof failed/)
})
}
