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
  const sensor = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/NeedHelpSensor.fs')

  assert.match(sensor, /let hasPhysicalParent sessionId =\s*sessionParents\.ContainsKey/)
  assert.match(sensor, /isOwned sessionId && hasPhysicalParent sessionId/)
  assert.match(
    bootstrap,
    /LoopSensor\.create[\s\S]*?\(fun sessionId ->\s+sessionPort\.InterruptAttempt sessionId\)/,
  )
  assert.match(
    bootstrap,
    /NeedHelpSensor\(\s*NeedHelpSensor\.createEligibilityPredicate[\s\S]*?\(fun sessionId -> sessionPort\.InterruptAttempt sessionId\)\s*\)/,
  )
  assert.match(
    sensor,
    /let isEligibleRole[\s\S]*?Role\.Blogger[\s\S]*?Role\.Distiller[\s\S]*?false/,
  )
})

test('WHAT[MANAGED-SESSION-016] logical TurnAborted durably cancels runtime children before physical cascade and terminal', () => {
  const ordinary = read('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')
  const tools = read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs')
  const scope = read('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')

  assert.match(scope, /abstract CancelSessionChildren: string -> Task/)
  assert.match(scope, /member _\.CancelSessionChildren\(sessionId: string\) : Task/)
  assert.match(tools, /member _\.CancelSessionChildren\(sessionId: string\) : Task/)
  assert.match(tools, /CancelSessionChildren[\s\S]*?runtime\.CancelAndDrain\(\)/)

  assert.match(
    ordinary,
    /let private handleAborted[\s\S]*?do! cancelSessionChildren turn\.SessionId[\s\S]*?do! sessionPort\.AbortChildren turn\.SessionId[\s\S]*?eventPort\.NotifyTerminal\s+turn\.SessionId\s+\(TerminalOutcome\.Aborted\(TerminalStop\.forAuthority turn\.AuthorityRootUserMessageId reason\)\)/,
  )
})
