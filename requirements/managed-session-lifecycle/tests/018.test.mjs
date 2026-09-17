import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { interruptAttemptAdapterProbe, interruptRejectedAdapterProbe, interruptTerminatedAdapterProbe } = await import("../../../dist/OpenCode/Host/SessionsSurface.js");

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[MANAGED-SESSION-018] TurnAborted has no logical child-cancel authority', () => {
  const ordinary = read('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')
  const tools = read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs')
  const scope = read('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')

  const sessionOwner = read('src/Wanxiangshu/OpenCode/Host/SessionRuntimeOwner.fs')

  assert.match(sessionOwner, /abstract CancelSessionChildren: string -> Task/)
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[MANAGED-SESSION-018] shutdown detaches session runtimes before journal release without logical cancel', () => {
  const scope = read('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')
  const sessionOwner = read('src/Wanxiangshu/OpenCode/Host/SessionRuntimeOwner.fs')
  const tools = read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs')
  const deletion = read('src/Wanxiangshu/OpenCode/Host/HostSessionDeletion.fs')
  const preparation = read('src/Wanxiangshu/OpenCode/Host/TurnRuntimePreparation.fs')
  const observer = read('src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs')
  const scheduler = read('src/Wanxiangshu/Composition/Turn/Scheduler.fs')
  const bootstrap = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')

  assert.match(sessionOwner, /abstract DisposeSession: string -> Task/)
  assert.match(sessionOwner, /abstract DisposeExecutorRuntime: string -> Task/)
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
})
test('WHAT[MANAGED-SESSION-018] fork terminal callbacks drain before either detach or authorized parent cancel', () => {
  const runtime = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs')
  const lifecycle = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs')
  assert.equal(existsSync(join(ROOT, 'src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/OneShotTool.fs')), false, 'OneShotTool.fs must be physically removed')

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
})
test('WHAT[MANAGED-SESSION-018] TurnAborted publishes attempt terminal without child cascade', () => {
  const ordinary = read('src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs')

  const abortedBlock = ordinary.match(/let private handleAborted([\s\S]*?)let private applyJoinGuardNudge/)
  assert.ok(abortedBlock)
  assert.doesNotMatch(abortedBlock[1], /cancelSessionChildren|abortParent|AbortChildren|CancelAndDrain/)
  assert.match(abortedBlock[1], /TerminalOutcome\.Aborted/)
})
}
