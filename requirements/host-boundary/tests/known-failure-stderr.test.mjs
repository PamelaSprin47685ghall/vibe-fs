import assert from 'node:assert/strict'
import test from 'node:test'
import { spawnSync } from 'node:child_process'

const moduleUrl = new URL('../../../dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js', import.meta.url).href

test('WHAT[HOST-BOUNDARY-025] known typed failure emits one redacted JSON line without stack', () => {
  const source = `
    import { emitKnownFailure } from ${JSON.stringify(moduleUrl)};
    emitKnownFailure({
      operation: 'ProviderFailureObserved',
      logicalRunId: 'logical-1',
      sessionId: 'session-2',
      physicalUserMessageId: 'message-3',
      providerRunIdentity: 'provider-4',
      effectiveAgent: 'coder',
      role: 'Coder',
      providerRequestKind: 'work-main',
      transition: { from: 'ProviderStarted', to: 'Terminal' },
      failureClass: 'ProviderPermanent',
      retryDecision: 'NoRetry',
      fallbackDecision: 'NoFallback',
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
