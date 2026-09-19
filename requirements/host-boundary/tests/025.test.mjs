import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { createCounters, recordObservation, snapshot, projectRecord, tryEmit } = await import("../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js");

const causalRecord = {
  operation: 'ProviderTerminalObserved',
  logicalRunId: 'logical-7',
  sessionId: 'session-3',
  authorityRootUserMessageId: 'root-5',
  physicalUserMessageId: 'message-11',
  promptKey: null,
  providerRunIdentity: 'provider-13',
  participant: 'coder',
  role: 'coder',
  providerRequestKind: 'work-main',
  transition: { from: 'ProviderStarted', to: 'Terminal' },
  failureClass: 'ProviderPermanent',
  resolution: 'TerminalizeProviderStarted',
  capacityState: 'Released',
  capacityFence: null,
  hook: 'chat.message',
  policyClass: 'Workflow',
  recoveryDecision: 'ObserveOnly',
  persistenceCommitment: 'Committed',
}

test('WHAT[host-boundary-025] causal diagnostic schema preserves exact available correlation and explicit absence', () => {
  const projected = projectRecord(causalRecord)
  assert.deepEqual(projected, causalRecord)
  assert.equal(Object.isFrozen(projected), true)

  const unavailable = projectRecord({
    ...causalRecord,
    providerRunIdentity: null,
    failureClass: null,
    resolution: null,
    recoveryDecision: null,
  })
  assert.equal(unavailable.providerRunIdentity, null)
  assert.equal(unavailable.failureClass, null)
})
test('WHAT[host-boundary-025] causal diagnostics reject payload fields and redact credential/path material', () => {
  assert.throws(
    () => projectRecord({ ...causalRecord, prompt: 'user text' }),
    /unknown causal diagnostic field 'prompt'/,
  )

  const projected = projectRecord({
    ...causalRecord,
    participant: 'Bearer secret-value at /home/alice/private/key',
  })
  assert.equal(projected.participant.includes('secret-value'), false)
  assert.equal(projected.participant.includes('/home/alice'), false)
  assert.match(projected.participant, /\[REDACTED\]/)
})
test('WHAT[host-boundary-025] missing observation counters are process-local monotonic immutable snapshots', () => {
  const counters = createCounters()
  recordObservation(counters, 'IdentityConflict')
  recordObservation(counters, 'QueueFull')
  recordObservation(counters, 'FatalSettlement')
  recordObservation(counters, 'RecoveryManualIntervention')

  assert.deepEqual(snapshot(counters), {
    identityConflicts: 1,
    queueFull: 1,
    fatalSettlements: 1,
    recoveryObserveOnly: 0,
    recoveryResumeAdmission: 0,
    recoveryReconcileStartedProvider: 0,
    recoveryMarkTerminal: 0,
    recoveryFailClosed: 0,
    recoveryManualIntervention: 1,
    hookFailures: 0,
    fallbackAdvances: 0,
    streamAborts: 0,
  })
  assert.equal(Object.isFrozen(snapshot(counters)), true)
  assert.equal('duplicateFences' in snapshot(counters), false, 'capacity owner counters must not be duplicated locally')
})
test('WHAT[host-boundary-025] diagnostic adapter failure is transparent to caller state', () => {
  const business = { accepted: true }
  const emitted = tryEmit({ ...causalRecord, operation: 'invalid\noperation' })
  assert.equal(emitted, false)
  assert.deepEqual(business, { accepted: true })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const EventsSurface = await import("../../../dist/OpenCode/Host/EventsSurface.js");

const notify = (port, sessionId, outcome) => EventsSurface.notify(port, sessionId, outcome.kind, outcome.providerRun ?? '', outcome.error ?? outcome.value ?? '')
const completed = (providerRun = '') => ({ kind: 'Completed', providerRun })
const failed = (error) => ({ kind: 'Failed', error })
const aborted = (reason) => ({ kind: 'Aborted', error: reason })

test('WHAT[delegation-025] EVT_future_subscriber_does_not_replay_sticky_terminal', () => {
  const port = EventsSurface.create()
  EventsSurface.notify(port, 'ses-reused', 'Completed', 'run-old', 'old result')

  const seen = []
  const subscription = EventsSurface.subscribeFuture(port, (sessionId, outcome) => seen.push({ sessionId, outcome }))
  assert.deepEqual(seen, [], 'fresh work unit must not inherit the previous terminal')

  EventsSurface.notify(port, 'ses-reused', 'Completed', 'run-new', 'new result')
  assert.equal(seen.length, 1)
  assert.equal(seen[0].sessionId, 'ses-reused')
  assert.equal(seen[0].outcome.providerRun, 'run-new')
  EventsSurface.dispose(subscription)
})
test('WHAT[delegation-025] EVT_run_scoped_failure_preserves_authority_root_across_host_event_port', () => {
  const port = EventsSurface.create()
  const seen = []
  EventsSurface.subscribeFuture(port, (_, outcome) => seen.push(outcome))

  EventsSurface.notifyForAuthority(port, 'ses-causal-failure', 'Failed', 'root-2', 'provider exhausted')

  assert.equal(seen.length, 1)
  assert.equal(seen[0].kind, 'Failed')
  assert.equal(seen[0].text, 'provider exhausted')
  assert.equal(seen[0].authorityRoot, 'root-2')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { spawnSync } = await import("node:child_process");

const moduleUrl = new URL('../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js', import.meta.url).href

test('WHAT[host-boundary-025] known typed failure emits one redacted JSON line without stack', () => {
  const source = `
    import { emitKnownFailure } from ${JSON.stringify(moduleUrl)};
    emitKnownFailure({
      operation: 'ProviderFailureObserved',
      logicalRunId: 'logical-1',
      sessionId: 'session-2',
      physicalUserMessageId: 'message-3',
      providerRunIdentity: 'provider-4',
      participant: 'coder',
      role: 'Coder',
      providerRequestKind: 'work-main',
      transition: { from: 'ProviderStarted', to: 'Terminal' },
      failureClass: 'ProviderPermanent',
      resolution: 'TerminalizeProviderStarted',
      capacityState: 'Released',
      recoveryDecision: null,
      persistenceCommitment: 'Committed',
    });
  `
  const run = spawnSync(process.execPath, ['--input-type=module', '--eval', source], {
    encoding: 'utf8',
    env: { ...process.env, WANXIANGSHU_DIAG: '1' },
  })
  assert.equal(run.status, 0, run.stderr)
  const lines = run.stderr.trim().split('\n')
  assert.equal(lines.length, 1)
  const record = JSON.parse(lines[0])
  assert.equal(record.failureClass, 'ProviderPermanent')
  assert.equal(record.providerRunIdentity, 'provider-4')
  assert.equal(/stack|\.fs:\d|\.js:\d|\n\s*at /i.test(lines[0]), false)
  assert.equal(run.stdout, '')
})
}
