import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const hostSource = readFileSync(
  new URL('../../../src/Wanxiangshu/Change/Host/Host.fs', import.meta.url),
  'utf8',
)

const toolSource = readFileSync(
  new URL('../../../src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs', import.meta.url),
  'utf8',
)

const between = (source, begin, end) => {
  const start = source.indexOf(begin)
  assert.notEqual(start, -1, `missing ${begin}`)
  const finish = source.indexOf(end, start)
  assert.notEqual(finish, -1, `missing ${end}`)
  return source.slice(start, finish)
}

test('WHAT[RELAY-009] same-road charge uses exact physical authority admission before durable revision', () => {
  const continuation = between(hostSource, 'member _.ContinueManagerJob', '/// EXEC-019')
  const core = between(hostSource, 'let continueManagerJobCore', 'let runAuthorityUpdate')
  const advance = between(hostSource, 'let advanceAuthorityRevision', 'let continueManagerJobCore')

  assert.match(continuation, /callerProviderRun:\s*ProviderRunIdentity/)
  assert.match(continuation, /callerToolCallId:\s*ToolCallId/)
  assert.match(core, /requireJobRecord jobId/)
  assert.match(advance, /captureSnapshot record\.ManagerJobId/)
  assert.match(advance, /trySendGateContinuationPhysical/)
  assert.match(advance, /ContinuationKind\.ManagedDelegationAssignment/)
  assert.match(advance, /RelayEvent\.AuthorityRevisionAdvanced/)
  assert.doesNotMatch(advance, /HostSessionNudge\.sendContinuation\b/)
  assert.doesNotMatch(advance, /ContinuationKind\.ManagerGuard/)
})

test('WHAT[RELAY-009] commission forwards exact caller run and tool identities to authority update', () => {
  const continuation = between(toolSource, 'let private continueExistingCommission', 'let private commissionExistingByname')

  assert.match(continuation, /context\.ProviderRunId, context\.ToolCallId/)
  assert.match(continuation, /host\.ContinueManagerJob\([\s\S]*providerRun,[\s\S]*toolCallId,/)
})
