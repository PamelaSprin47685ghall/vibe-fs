import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

const root = resolve(import.meta.dirname, '../../..')
const read = (p) => readFileSync(resolve(root, p), 'utf8')

const fissionProduction = () => [
  'src/Wanxiangshu/Execution/Fission/Model.fs',
  'src/Wanxiangshu/Execution/Fission/Admission.fs',
  'src/Wanxiangshu/Execution/Fission/Runtime.fs',
  'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
].map(read).join('\n')

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-010] V1 Fission has no OpenCode session-fork path and owns durable replay anchors', () => {
  const code = fissionProduction()
  assert.doesNotMatch(code, /session\s*\.\s*fork|\/session\/[^"']*\/fork|CreateForkedSession|ForkSession/i)

  const facts = read('src/Wanxiangshu/Execution/Fission/Facts.fs')
  const fold = read('src/Wanxiangshu/Execution/Fission/Projection.fs') + read('src/Wanxiangshu/Execution/Fission/Fold.fs')
  assert.match(facts, /FissionAdmitted/)
  assert.match(facts, /FissionLaneMaterialized/)
  assert.match(facts, /FissionCompletionDelivered/)
  assert.match(facts, /FissionTakeoverStarted/)
  assert.match(facts, /FissionConverged/)
  assert.match(fold, /FissionAdmitted/)
  assert.match(fold, /FissionTakeoverStarted/)
  assert.match(fold, /FissionConverged/)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] Host convergence performs ring takeover before reporting the old logical owner', () => {
  const host = read('src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs')

  assert.match(host, /FissionFact\.FissionTakeoverStarted/)
  assert.match(host, /SendContinuation[\s\S]{0,1200}?ContinuationKind\.FissionHandoff[\s\S]{0,600}?AwaitMode\.Await/)
  assert.match(host, /turn\.PhysicalUserMessageId <> takeover\.PhysicalUserMessageId/)
  assert.match(host, /CompletedTurnClassifier\.partsSessionText turn\.Parts/)
  assert.match(host, /NotifyTerminal group\.OwnerSessionId \(TerminalOutcome\.Completed result\)/)
  assert.doesNotMatch(host, /TerminalText\s*=\s*aggregate/)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-012] Fission role eligibility comes from ToolPermission.Fission for current office vocabulary', () => {
  const roles = read('src/Wanxiangshu/Foundation/Roles.fs')
  const registry = read('src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs')
  assert.match(registry, /"fission"\s*->\s*fun r -> Roles\.isAllowed r ToolPermission\.Fission/)

  for (const role of ['Manager', 'Coder', 'Inspector', 'Browser', 'Inquiry']) {
    const block = new RegExp(`\\| Role\\.${role} ->[\\s\\S]{0,900}?ToolPermission\\.Fission`)
    assert.match(roles, block, `${role} must own the Fission consequence`)
  }
  for (const role of ['Orchestrator', 'DevOps', 'Reviewer', 'Blogger', 'Distiller']) {
    const arm = new RegExp(`\\| Role\\.${role} ->([^\\n]*(?:\\n(?!\\s*\\| Role\\.).*){0,16})`)
    const text = arm.exec(roles)?.[0] ?? ''
    assert.doesNotMatch(text, /ToolPermission\.Fission/, `${role} must not own Fission`)
  }
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-003] sibling creation is a distinct Host capability from managed-child creation', () => {
  const sessions = read('src/Wanxiangshu/OpenCode/Host/Sessions.fs')
  const port = read('src/Wanxiangshu/OpenCode/Host/OpenCodePort.fs')
  assert.match(sessions, /CreateSiblingSession/)
  assert.match(sessions, /TryGetParentSession/)
  assert.match(port, /CreateSession/)
  assert.match(port, /parentID/)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-013] request visibility and tool adapter both enforce origin before use', () => {
  const bootstrap = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  assert.match(bootstrap, /FissionRequestProjection\.apply/)
  assert.match(bootstrap, /SessionParents\.ContainsKey/)

  const tool = read('src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs')
  const executeAt = tool.indexOf('let private execute (scope: ToolRuntimeScope)')
  assert.ok(executeAt >= 0, 'fission execute adapter must exist')
  const executeBlock = tool.slice(executeAt)
  assert.match(executeBlock, /executeForCaller/, 'outer execute must enter the origin gate before parser work')
  assert.doesNotMatch(executeBlock, /FissionPrompt\.parse/, 'outer execute must not parse before origin admission')

  const callerAt = tool.indexOf('let private executeForCaller')
  const subsessionAt = tool.indexOf('let private executeForSubsession')
  assert.ok(callerAt >= 0, 'physical-origin helper must exist')
  const callerBlock = tool.slice(callerAt, executeAt)
  assert.match(callerBlock, /TryGetParentSession/, 'origin helper must precheck physical parent')
  assert.match(callerBlock, /Ok\(Some _\) -> return! executeForSubsession/)

  assert.ok(subsessionAt >= 0, 'eligible subsession parser helper must exist')
  const subsessionBlock = tool.slice(subsessionAt, callerAt)
  assert.match(subsessionBlock, /FissionPrompt\.parse/, 'eligible subsessions still use the canonical parser')
  assert.match(tool, /FissionAdmission\.admit/, 'Domain admission keeps the authoritative second origin gate')
})
