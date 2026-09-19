import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const planner = await import("../../../dist/Context/Companion/CompressionSurface.js");
const companion = await import("../../../dist/Context/Companion/ProjectionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })
const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => ({
  probeId: id,
  basedOnEpoch: 0,
  candidate: snapshotAt(cutoff),
})

test('WHAT[prefix-stability-015] COMPANION_013_the_plan_reuses_the_snapshot_s_own_synthetic_id', () => {
  // Not re-derived. That id was fixed when the candidate was built and is what the
  // provider has already seen for this epoch; a second derivation site would make any
  // drift a cold boundary on every later request.
  const snapshot = snapshotAt(3, { seal: 'seal-fixed' })

  assert.equal(prefix.forSnapshot(snapshot, companion.memoryPreamble, 'body').memoryId, 'synthetic-seal-fixed')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const pair = await import("../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js");

const {
  tryInject,
  isPairProgrammingThought,
  skipAutoInjectedRequested,
  source,
  text,
  stableCallId,
} = pair
const inject = async (session, raw, markerText = text) => {
  const result = await tryInject(session, markerText, raw)
  assert.equal(result.ok, true, `HOST-013 transform must commit the pair: ${result.error ?? ''}`)
  return result.value
}
const userMsg = (id, body = 'hello') => ({
  info: { id, role: 'user' },
  parts: [{ type: 'text', text: body }],
})
const assistantText = (id) => ({
  info: { id, role: 'assistant' },
  parts: [{ type: 'text', text: 'ok' }],
})
const toolCall = (id, tool, callID) => ({
  info: { id, role: 'assistant' },
  parts: [{
    type: 'tool',
    tool,
    callID,
    state: { status: 'pending', input: {}, time: { start: 0 } },
  }],
})
const toolResult = (id, tool, callID, output = 'ok') => ({
  info: { id, role: 'assistant' },
  parts: [{
    type: 'tool',
    tool,
    callID,
    state: { status: 'completed', input: {}, output, time: { start: 0, end: 0 } },
  }],
})
const pairMessages = (messages) => messages.filter((m) => isPairProgrammingThought(m))
const guidanceSuffix = (markerText) => `\0\uFEFF${markerText}`
const terminalOutputOf = (messages, id) => messages.find((m) => m.info.id === id).parts[0].state.output

test('WHAT[prefix-stability-015] PPT_tryInject_call_id_is_stable_per_session_and_ordinal', () => {
  assert.equal(stableCallId('ses_1', 1n), stableCallId('ses_1', 1n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_1', 2n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_2', 1n))
})
test('WHAT[prefix-stability-015] PPT_tryInject_without_session_id_still_appends_stable_pair', async () => {
  // Without a session id the guidance still lands deterministically on the
  // terminal real tool result; a session-less trailing-user turn passes through.
  const raw = [toolCall('c1', 'bash', 't1'), toolResult('r1', 'bash', 't1', 'out1')]
  const out = await inject(undefined, raw)
  assert.ok(out)
  assert.equal(out.length, 2)
  assert.equal(pairMessages(out).length, 0)
  assert.equal(terminalOutputOf(out, 'r1'), `out1${guidanceSuffix(text)}`)
  assert.deepEqual(await inject(undefined, out), out, 'session-less replay stays byte-identical')
})
}
