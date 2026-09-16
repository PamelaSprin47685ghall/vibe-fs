import assert from 'node:assert/strict'
import test from 'node:test'
import * as planner from '../../../dist/Context/Companion/CompressionSurface.js'
import * as companion from '../../../dist/Context/Companion/ProjectionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: prefix-stability.
//
// CTX-010 / COMPANION-009/010/013 / HOST-006 epoch-related prefix-plan assertions:
// a discarded probe leaves the committed epoch in place, the probe plan and the
// committed plan are built the same way, Snapshot=None means raw history, a
// retired snapshot and a never-promoted one produce the same plan, the memory is
// wrapped as low-trust context, the plan reuses the snapshot's own synthetic id,
// and the required blob follows the choice not the committed state.



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

// Split from tests/unit/host/pair-thought-transform.test.mjs (cutover Wave 2a);
// owner: prefix-stability. PPT: PairProgrammingThoughtTransform — HOST-013
// universal cursor mode: zero synthetic skill messages on every provider.
// Guidance travels only as a NUL+BOM suffix on the terminal real tool result;
// the durable occurrence (ordinal/call-id 稳定性、skip-auto-injected 环境门、
// 同 occurrence 恢复、replay 去重) is journal-level, never a wire message.
// NUL+BOM error-result 断言归 provider-projection；
// PAIR_HINT marker 正文 craft 归 cognitive-environment。



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
// Universal cursor mode: the only guidance carrier is a NUL+BOM suffix on the
// terminal real tool result. There are no synthetic skill messages, so every
// shape assertion below counts pairMessages === 0 and checks suffix bytes.
const guidanceSuffix = (markerText) => `\0\uFEFF${markerText}`
const terminalOutputOf = (messages, id) => messages.find((m) => m.info.id === id).parts[0].state.output

test('WHAT[PREFIX-STABILITY-015] COMPANION_013_the_plan_reuses_the_snapshot_s_own_synthetic_id', () => {
  // Not re-derived. That id was fixed when the candidate was built and is what the
  // provider has already seen for this epoch; a second derivation site would make any
  // drift a cold boundary on every later request.
  const snapshot = snapshotAt(3, { seal: 'seal-fixed' })

  assert.equal(prefix.forSnapshot(snapshot, companion.memoryPreamble, 'body').memoryId, 'synthetic-seal-fixed')
})

test('WHAT[PREFIX-STABILITY-015] PPT_tryInject_call_id_is_stable_per_session_and_ordinal', () => {
  assert.equal(stableCallId('ses_1', 1n), stableCallId('ses_1', 1n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_1', 2n))
  assert.notEqual(stableCallId('ses_1', 1n), stableCallId('ses_2', 1n))
})

test('WHAT[PREFIX-STABILITY-015] PPT_tryInject_without_session_id_still_appends_stable_pair', async () => {
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
