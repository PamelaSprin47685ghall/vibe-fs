import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[MANAGED-SESSION-018] shutdown detaches session runtimes before journal release without logical cancel', () => {
  const scope = read('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')
  const tools = read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs')
  const deletion = read('src/Wanxiangshu/OpenCode/Host/HostSessionDeletion.fs')
  const preparation = read('src/Wanxiangshu/OpenCode/Host/TurnRuntimePreparation.fs')
  const observer = read('src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs')
  const scheduler = read('src/Wanxiangshu/Composition/Turn/Scheduler.fs')
  const bootstrap = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')
  const finalityPorts = read('src/Wanxiangshu/Mission/Finality/Ports.fs')
  const finalityBlessing = read('src/Wanxiangshu/Mission/Finality/Blessing.fs')
  const finalityHostPort = read('src/Wanxiangshu/Mission/Finality/OpenCode/HostPort.fs')

  assert.match(scope, /abstract DisposeSession: string -> Task/)
  assert.match(scope, /abstract DisposeExecutorRuntime: string -> Task/)
  assert.match(scope, /do! active\.DisposeSession sessionId/)
  assert.match(scope, /captureTaskFailure \(SharedAgentJournal\.releaseAsync journal\)/)

  assert.match(tools, /member _\.DisposeSession\(sessionId: string\) : Task/)
  assert.match(tools, /member _\.DisposeExecutorRuntime\(sessionId: string\) : Task/)
  assert.match(tools, /for runtime in forkRuntimes do\s*do! runtime\.DetachAndDrain\(\)/)
  assert.match(tools, /for host in orchestrators do\s*do! host\.DetachAndDrain\(\)/)
  assert.match(tools, /CancelSessionChildren[\s\S]*?runtime\.CancelAndDrain\(\)/)
  assert.match(tools, /DisposeSession\(sessionId: string\)[\s\S]*?runtime\.CancelAndDrain\(\)/)
  assert.match(tools, /member _\.RunOwnedWork\(start: unit -> Task\) : bool/)
  assert.match(tools, /let! ownedFailure = stopOwnedWorkAndDrain \(\)/)
  assert.doesNotMatch(tools, /runtime\.Cancel\(\)/)

  assert.match(deletion, /do! scope\.DisposeSession\(SessionId\.value sessionId\)/)
  assert.match(preparation, /disposeExecutorRuntime: string -> Task/)
  assert.match(observer, /do! TurnRuntimePreparation\.prepare scope\.DisposeExecutorRuntime turn/)
  assert.match(scheduler, /\?durableUnavailable: unit -> bool/)
  assert.match(scheduler, /not accepting \|\| isDurableUnavailable \(\)/)
  assert.match(scheduler, /if isDurableUnavailable \(\) then\s*closeAdmission \(\)/)
  assert.match(bootstrap, /durableUnavailable = Some\(fun \(\) -> journal \|> Option\.exists AgentJournal\.isPoisoned\)/)
  assert.match(finalityPorts, /AbortReviewer: SessionId -> Task/)
  assert.match(finalityBlessing, /do! FinalityReviewerPort\.abortAll reviewerPort members/)
  assert.match(finalityHostPort, /let! _ = scope\.Sessions\.InterruptAttempt reviewerSessionId/)
  assert.doesNotMatch(finalityHostPort, /AbortReviewer[\s\S]{0,160}InterruptAttempt reviewerSessionId \|> ignore/)
})

test('WHAT[MANAGED-SESSION-009] provider transform is admitted into plugin shutdown ownership', () => {
  const scope = read('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')
  const hooks = read('src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs')
  const interop = read('src/Wanxiangshu/OpenCode/Host/PluginHostInterop.fs')
  const policy = read('src/Wanxiangshu/OpenCode/Host/HookPolicy.fs')

  assert.match(scope, /member this\.RunOwnedWork\(start: unit -> Task\) : Task/)
  assert.match(scope, /let ownedWorkDrain = this\.StopOwnedWorkAndDrain\(\)/)
  assert.match(hooks, /let ownedTransform[\s\S]{0,220}scope\.RunOwnedWork\(fun \(\) -> transform inObj outObj\)/)
  assert.match(
    hooks,
    /let messagesTransform\s*=\s*registeredHook HookKey\.MessagesTransform \(curriedHook \(box ownedTransform\)\)/,
  )
  assert.match(
    policy,
    /\| HookKey\.MessagesTransform ->\s*\{ HostKey = "experimental\.chat\.messages\.transform"\s*DiagnosticOperation = "plugin-hook-messages-transform-failed"/,
  )
  assert.match(
    interop,
    /let registeredHook \(key: HookKey\) \(adaptedHook: obj\) : string \* obj =\s*let metadata = HookPolicy\.metadata key \|> HookPolicy\.validate\s*metadata\.HostKey, policyAwareHook metadata\.DiagnosticOperation adaptedHook/,
  )
})

test('WHAT[MANAGED-SESSION-018] fork terminal callbacks drain before either detach or authorized parent cancel', () => {
  const runtime = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs')
  const lifecycle = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs')
  const oneShot = read('src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/OneShotTool.fs')

  assert.match(runtime, /let startOwnedWork \(work: unit -> Task\) : Task/)
  assert.match(runtime, /let stopOwnedWorkAndDrain \(\) : Task/)
  assert.match(runtime, /do! stopOwnedWorkAndDrain \(\)/)
  assert.match(runtime, /member this\.DetachAndDrain\(\) : Task/)
  const detachBlock = runtime.match(/member this\.DetachAndDrain\(\) : Task =([\s\S]*?)member this\.Cancel\(\)/)
  assert.ok(detachBlock, 'process detach implementation must be inspectable')
  assert.doesNotMatch(detachBlock[1], /HandleController\.cancelChildren|sessions\.AbortSession|teardownChildren/)
  assert.match(runtime, /member internal _\.TrackOwnedWork\(work: unit -> Task\)/)
  assert.match(runtime, /member this\.FailRun\(run: PendingHostRun, error: string\) : Task =\s*startOwnedWork/)

  assert.match(lifecycle, /trackOwnedWork: \(unit -> Task\) -> unit/)
  assert.match(lifecycle, /fun _ outcome ->\s*trackOwnedWork \(fun \(\) ->/)
  assert.doesNotMatch(lifecycle, /fun _ outcome ->[\s\S]{0,240}\|> ignore/)

  assert.match(oneShot, /scope\.RunOwnedWork\(fun \(\) ->/)
  assert.doesNotMatch(oneShot, /settleCompletedTerminal scope childId terminal succeed latch completion\s*\|> ignore/)
})

test('WHAT[MANAGED-SESSION-018] TurnAborted publishes attempt terminal without child cascade', () => {
  const ordinary = read('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')

  const abortedBlock = ordinary.match(/let private handleAborted([\s\S]*?)let private applyJoinGuardNudge/)
  assert.ok(abortedBlock)
  assert.doesNotMatch(abortedBlock[1], /cancelSessionChildren|abortParent|AbortChildren|CancelAndDrain/)
  assert.match(abortedBlock[1], /TerminalOutcome\.Aborted/)
})
