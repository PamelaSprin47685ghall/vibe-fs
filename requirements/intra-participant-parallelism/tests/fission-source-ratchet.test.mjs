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

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-002] fission tool exposes prompts as a string array without newline splitting', () => {
  const model = read('src/Wanxiangshu/Execution/Fission/Model.fs')
  const tool = read('src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs')

  assert.match(model, /let parse \(prompts: string list\)/)
  assert.doesNotMatch(model, /normalizeNewlines|\.Split\('\n'\)/)
  assert.match(tool, /args\.Texts "prompts"/)
  assert.match(tool, /ToolHostCodec\.stringArraySchema factory/)
  assert.doesNotMatch(tool, /args\.Text "prompts"/)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-010] V1 Fission has no OpenCode session-fork path and owns durable replay anchors', () => {
  const code = fissionProduction()
  assert.doesNotMatch(code, /session\s*\.\s*fork|\/session\/[^"']*\/fork|CreateForkedSession|ForkSession/i)

  const facts = read('src/Wanxiangshu/Execution/Fission/Facts.fs')
  const fold = read('src/Wanxiangshu/Execution/Fission/Projection.fs') + read('src/Wanxiangshu/Execution/Fission/Fold.fs')
  assert.match(facts, /FissionAdmitted/)
  assert.match(facts, /FissionLaneMaterialized/)
  assert.match(facts, /FissionCompletionDelivered/)
  assert.match(facts, /FissionTakeoverClaimed/)
  assert.match(facts, /FissionTakeoverStarted/)
  assert.match(facts, /FissionConverged/)
  assert.match(fold, /FissionAdmitted/)
  assert.match(fold, /FissionTakeoverClaimed/)
  assert.match(fold, /FissionTakeoverStarted/)
  assert.match(fold, /FissionConverged/)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-009] Host convergence performs ring takeover before reporting the old logical owner', () => {
  const host = read('src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs')
  const bootstrap = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const facts = read('src/Wanxiangshu/Execution/Fission/Facts.fs')
  const projection = read('src/Wanxiangshu/Execution/Fission/Projection.fs')

  assert.match(host, /FissionFact\.FissionTakeoverClaimed/)
  assert.match(host, /SendContinuation[\s\S]{0,1200}?ContinuationKind\.FissionHandoff[\s\S]{0,600}?AwaitMode\.Await/)
  assert.doesNotMatch(
    host,
    /TaskCompletionSource<PhysicalUserMessageId>|acceptedPhysicalId\.Task/,
    'lane-terminal observation must not block waiting for a future chat.message physical id',
  )
  assert.match(facts, /FissionTakeoverClaimed[\s\S]{0,500}?PromptKey:\s*PromptKey/)
  assert.doesNotMatch(host, /acceptedDispatchForPromptKey|takeoverPhysicalMessage|TakeoverTurnDisposition/)
  assert.match(host, /turn\.SessionId\s*=\s*takeover\.LaneSessionId/)
  assert.match(host, /CompletedTurnClassifier\.partsSessionText turn\.Parts/)
  assert.match(host, /NotifyTerminal group\.OwnerSessionId \(TerminalOutcome\.Completed result\)/)
  assert.doesNotMatch(host, /TerminalText\s*=\s*aggregate/)

  assert.match(host, /FissionRing\.finalLane group\.LaneCount/)
  assert.doesNotMatch(host, /LastMaterializedLaneIndex/)
  assert.doesNotMatch(projection, /LastMaterializedLaneIndex/)

  const physicalEndAt = bootstrap.indexOf('onPhysicalExecutionEnd =')
  assert.ok(physicalEndAt >= 0, 'physical execution terminal callback must exist')
  const physicalEndBlock = bootstrap.slice(physicalEndAt, bootstrap.indexOf('let! subscriptionResult', physicalEndAt))
  assert.match(physicalEndBlock, /reconciler\.NotifyProjectionChanged\(sessionId, physicalUserMessageId\)/)
  assert.match(physicalEndBlock, /FissionHost\.observePhysicalExecutionEnd/)
  assert.doesNotMatch(physicalEndBlock, /FissionProjection\.tryMembershipOfLane|let isCurrentPhysical|let isFissionLane/)
  assert.match(host, /let observePhysicalExecutionEnd/)
  assert.match(host, /tryCurrentPhysical sessionId/)
  assert.match(host, /FissionProjection\.tryMembershipOfLane/)
  assert.match(
    host,
    /if isCurrentPhysical && isFissionLane then[\s\S]{0,120}?kick sessionId/,
    'a successful exact Fission lane terminal must open a snapshot reconcile occasion even when OpenCode drops session.idle',
  )

  assert.match(host, /let routeAttemptAborted/)
  assert.match(host, /FissionRuntime\.isSilentInterrupt sessionId/)
  assert.match(bootstrap, /FissionHost\.routeAttemptAborted/)
  assert.doesNotMatch(bootstrap, /FissionRuntime\.isSilentInterrupt/)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-014] Assistance and degeneration guard remain control-plane owners before Fission settlement', () => {
  const observer = read('src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs')
  const host = read('src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs')

  const businessTurn = observer.slice(observer.indexOf('let private observeBusinessTurn'))
  const assistanceAt = businessTurn.indexOf('scope.HandleAssistanceTurn context')
  const ordinaryPathAt = businessTurn.indexOf('observeWithoutAssistance ()', assistanceAt)
  assert.ok(assistanceAt >= 0 && ordinaryPathAt > assistanceAt, 'NEEDHELP assistance must establish its successor before entering the Fission/ordinary settlement path')
  assert.match(observer, /AssistanceTurnDisposition\.ClaimedButUnresolved[\s\S]{0,250}?closeUnresolvedAssistance/)
  assert.match(observer, /let private closeUnresolvedAssistance[\s\S]{0,700}?FissionHost\.failLaneIfActive/)
  assert.match(observer, /FissionHost\.observeLaneTurn[\s\S]{0,300}?abortCause/)
  assert.match(host, /AbortCause\.DegenerationGuard[\s\S]{0,300}?FissionSettlementObservation\.DegenerationInterrupted/)
  assert.match(host, /FissionLaneSettlementDecision\.YieldToTurnWorkflow/)
  assert.match(host, /FissionTakeoverSettlementDecision\.YieldToTurnWorkflow/)
})

test('WHAT[INTRA-PARTICIPANT-PARALLELISM-012] Fission role eligibility comes from ToolPermission.Fission for current office vocabulary', () => {
  const roles = read('src/Wanxiangshu/Foundation/Roles.fs')
  const registry = read('src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs')
  const owner = read('src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs')
  assert.match(registry, /"fission",\s*FissionTool\.admission/)
  assert.match(owner, /let\s+admission:\s*ToolAdmission\s*=\s*fun\s+_\s+r\s*->\s*Roles\.isAllowed\s+r\s+ToolPermission\.Fission/)

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
  const fissionHost = read('src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs')
  assert.match(fissionHost, /module FissionHostRequestProjection/)
  assert.match(fissionHost, /FissionRequestProjection\.apply/)
  assert.match(fissionHost, /ModelRouting\.RoutedChatExecution\.PluginManaged/)
  assert.doesNotMatch(bootstrap, /FissionRequestProjection\.apply|tools\?fission\s*<-/)
  assert.match(bootstrap, /FissionHostRequestProjection\.projectRouted/)
  assert.match(bootstrap, /SessionParents\.ContainsKey/)
  const externalProjectionAt = bootstrap.indexOf('FissionHostRequestProjection.projectExternalManaged hasPhysicalParent decoded output')
  const explicitResumeAt = bootstrap.indexOf('if explicitResume then')
  assert.ok(externalProjectionAt >= 0 && externalProjectionAt < explicitResumeAt, 'root tool deny must precede explicit-resume routing bypass')

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
