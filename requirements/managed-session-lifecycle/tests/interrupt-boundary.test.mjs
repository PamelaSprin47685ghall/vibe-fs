import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[MANAGED-SESSION-016] internal attempt interrupt is sub-session-only and never cascades children', () => {
  const sessions = read('src/Wanxiangshu/OpenCode/Host/Sessions.fs')

  assert.match(sessions, /abstract InterruptAttempt: sessionId: SessionId -> Task<Result<unit, string>>/)
  assert.match(
    sessions,
    /member _\.InterruptAttempt\(sessionId\)[\s\S]*?if not \(managedChild sessionId\) then[\s\S]*?user-facing\/root[\s\S]*?else\s*interruptManagedAttempt sessionId/,
  )

  const attemptBlock = sessions.match(/let interruptManagedAttempt \(sessionId: SessionId\)([\s\S]*?)let abortManagedSession/)
  assert.ok(attemptBlock, 'attempt interrupt operation must be inspectable')
  assert.match(attemptBlock[1], /port\.AbortSession sessionId/)
  assert.doesNotMatch(attemptBlock[1], /abortChildren|detachChild/)

  const sessionBlock = sessions.match(/let abortManagedSession \(sessionId: SessionId\)([\s\S]*?)interface ISessionHostPort/)
  assert.ok(sessionBlock, 'logical abort operation must be inspectable')
  assert.match(sessionBlock[1], /detachChild sessionId/)
  assert.match(sessionBlock[1], /do! abortChildren sessionId/)
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
