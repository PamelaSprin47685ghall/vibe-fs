import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  interruptAttemptAdapterProbe,
  interruptRejectedAdapterProbe,
} from '../../../dist/OpenCode/Host/SessionsSurface.js'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[MANAGED-SESSION-016] Sessions adapter rejects root attempt interrupt and physically aborts a managed child exactly once', async () => {
  const observed = await interruptAttemptAdapterProbe()

  assert.equal(observed.created, true)
  assert.equal(observed.creationError, null)
  assert.equal(observed.rootRejected, true)
  assert.equal(
    observed.rootError,
    'MANAGED-SESSION-016: user-facing/root session may only be interrupted by the external user',
  )
  assert.equal(observed.transportCallsAfterRoot, 0)
  assert.equal(observed.childInterrupted, true)
  assert.equal(observed.transportCallsAfterChild, 1)
  assert.deepEqual(observed.abortedSessionIds, ['adapter-child'])
  assert.equal(observed.childStillManagedAfterInterrupt, true)
})

test('WHAT[MANAGED-SESSION-016] managed interrupt Host rejection is terminal after exactly one AbortSession attempt', async () => {
  const observed = await interruptRejectedAdapterProbe()

  assert.equal(observed.outcome, 'Error')
  assert.equal(observed.error, 'controlled Host rejected AbortSession')
  assert.equal(observed.attemptsBeforeRejection, 1)
  assert.equal(observed.abortAttempts, 1)
  assert.deepEqual(observed.abortedSessionIds, ['adapter-rejected-child'])
  assert.deepEqual(Array.from(observed.virtualTimes), [0, 10, 1000])
  assert.deepEqual(observed.trace, [
    't=0 AbortSession(adapter-rejected-child)',
    't=10 Error(controlled Host rejected AbortSession)',
    't=1000 quiescent attempts=1',
  ])
})

test('WHAT[MANAGED-SESSION-016] automatic sensors cannot interrupt user-facing root and use attempt-only port', () => {
  const bootstrap = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const loopSensor = read('src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')

  assert.match(
    bootstrap,
    /LoopSensor\.create[\s\S]*?\(fun sessionId ->\s+sessionPort\.InterruptAttempt sessionId\)/,
  )
  assert.match(
    loopSensor,
    /ownedSessions\.Contains key && sessionParents\.ContainsKey key/,
  )
  assert.match(
    loopSensor,
    /member\s+private\s+this\.RunInterrupt[\s\S]*?abortSession/,
  )
})

test('WHAT[MANAGED-SESSION-018] TurnAborted has no logical child-cancel authority', () => {
  const ordinary = read('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')
  const tools = read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs')
  const scope = read('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')

  assert.match(scope, /abstract CancelSessionChildren: string -> Task/)
  assert.match(scope, /member _\.CancelSessionChildren\(sessionId: string\) : Task/)
  assert.match(tools, /member _\.CancelSessionChildren\(sessionId: string\) : Task/)
  assert.match(tools, /CancelSessionChildren[\s\S]*?runtime\.CancelAndDrain\(\)/)

  const abortedBlock = ordinary.match(/let private handleAborted([\s\S]*?)let private applyJoinGuardNudge/)
  assert.ok(abortedBlock, 'TurnAborted branch must remain inspectable')
  assert.doesNotMatch(abortedBlock[1], /cancelSessionChildren|abortParent|AbortChildren|CancelAndDrain/)
  assert.match(
    abortedBlock[1],
    /eventPort\.NotifyTerminal\s+turn\.SessionId\s+\(TerminalOutcome\.Aborted\(TerminalStop\.forAuthority turn\.AuthorityRootUserMessageId reason\)\)/,
  )
})

test('WHAT[MANAGED-SESSION-016] Turn orchestration consumes typed outcome without cross-callback aborted registry PC', () => {
  const ordinary = read('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')
  const workflow = read('src/Wanxiangshu/Composition/Turn/Workflow.fs')
  const observer = read('src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs')

  // OrdinaryTurnWorkflow and TurnWorkflow must not receive or use abortedSessions mutable HashSet
  assert.doesNotMatch(ordinary, /abortedSessions/)
  assert.doesNotMatch(workflow, /abortedSessions/)
  assert.doesNotMatch(observer, /scope\.Sessions\.AbortedSessions/)
})
