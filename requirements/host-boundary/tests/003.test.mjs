import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HostSignalSurface = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");
const CompactionPolicySurface = await import("../../../dist/Host/Contract/CompactionPolicySurface.js");

const requiredSettings = CompactionPolicySurface.requiredSettings()
const judgeFirstTurn = (pseudoRuns) => CompactionPolicySurface.judgeFirstTurn('ses_probe', pseudoRuns)

test('WHAT[HOST-BOUNDARY-003] HOST_003_retry_signal_is_a_typed_wake_never_a_run_identity_carrier', () => {
  const retry = HostSignalSurface.tryDecode({ type: 'session.status', sessionID: 'ses_retry', properties: { status: { type: 'retry', attempt: 3, reason: 'again' } } })
  assert.equal(retry.kind, 'ProviderRetry')
  assert.equal(retry.sessionId, 'ses_retry')
  assert.equal('messageId' in retry, false)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[HOST-BOUNDARY-003] coarse attempt abort never aborts every current chat execution', () => {
  const bootstrapSource = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const abortBranch = bootstrapSource.match(/\| AttemptAborted failure ->([\s\S]*?)\| SessionDeleted/)

  assert.ok(abortBranch, 'AttemptAborted branch must remain explicit')
  assert.doesNotMatch(abortBranch[1], /SignalChatRecoverySession|ChatExecutionRecoveryLifecycleEvent\.SessionAborted/)
  assert.match(abortBranch[1], /FissionHost\.routeAttemptAborted/)
  assert.match(abortBranch[1], /reconciler\.Signal signal/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const HostSignalSurface = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");
const HostSignalSubscribeSurface = await import("../../../dist/OpenCode/Host/HostSignalSubscribeSurface.js");

process.env.WANXIANGSHU_NO_FATAL_EXIT = '1'
const idleRaw = (sessionId) => ({ type: 'session.status', sessionID: sessionId, properties: { status: { type: 'idle' } } })
const dedicatedIdleRaw = (sessionId) => ({ type: 'session.idle', properties: { sessionID: sessionId } })
const retryRaw = (sessionId) => ({ type: 'session.status', sessionID: sessionId, properties: { status: { type: 'retry', attempt: '2', message: 'rate limited' } } })
const deletedRaw = (sessionId, parentID) => ({ type: 'session.deleted', sessionID: sessionId, properties: { parentID } })
const errorRaw = (sessionId, name = 'TimeoutError') => ({ type: 'session.error', sessionID: sessionId, properties: { error: { name } } })
const trySubscribe = async (input = {}) => HostSignalSubscribeSurface.trySubscribe(input, () => {})

test('WHAT[HOST-BOUNDARY-003] MISC_signals_session_id_of_all_cases', () => {
  for (const raw of [idleRaw('s1'), retryRaw('s1'), deletedRaw('s1', 'root'), errorRaw('s1')]) {
    assert.equal(HostSignalSurface.tryDecode(raw).sessionId, 's1')
  }
})
}
