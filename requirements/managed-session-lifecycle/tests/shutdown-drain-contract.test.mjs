import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

test('WHAT[MANAGED-SESSION-009] shutdown ownership drains session runtimes before journal release', () => {
  const scope = read('src/Wanxiangshu/OpenCode/Host/PluginRuntimeScope.fs')
  const tools = read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs')
  const deletion = read('src/Wanxiangshu/OpenCode/Host/HostSessionDeletion.fs')
  const preparation = read('src/Wanxiangshu/OpenCode/Host/TurnRuntimePreparation.fs')
  const observer = read('src/Wanxiangshu/OpenCode/Host/HostTurnObserver.fs')
  const scheduler = read('src/Wanxiangshu/Composition/Turn/Scheduler.fs')
  const bootstrap = read('src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs')

  assert.match(scope, /abstract DisposeSession: string -> Task/)
  assert.match(scope, /abstract DisposeExecutorRuntime: string -> Task/)
  assert.match(scope, /do! active\.DisposeSession sessionId/)
  assert.match(scope, /captureTaskFailure \(SharedAgentJournal\.releaseAsync journal\)/)

  assert.match(tools, /member _\.DisposeSession\(sessionId: string\) : Task/)
  assert.match(tools, /member _\.DisposeExecutorRuntime\(sessionId: string\) : Task/)
  assert.match(tools, /do! runtime\.CancelAndDrain\(\)/)
  assert.match(tools, /do! host\.CancelAndDrain\(\)/)
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

test('WHAT[MANAGED-SESSION-009] fork terminal callbacks are runtime-owned and drained before parent cancel', () => {
  const runtime = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs')
  const lifecycle = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs')
  const oneShot = read('src/Wanxiangshu/Execution/Delegation/Handle/OpenCode/OneShotTool.fs')

  assert.match(runtime, /let startOwnedWork \(work: unit -> Task\) : Task/)
  assert.match(runtime, /let stopOwnedWorkAndDrain \(\) : Task/)
  assert.match(runtime, /do! stopOwnedWorkAndDrain \(\)/)
  assert.match(runtime, /member internal _\.TrackOwnedWork\(work: unit -> Task\)/)
  assert.match(runtime, /member this\.FailRun\(run: PendingHostRun, error: string\) : Task =\s*startOwnedWork/)

  assert.match(lifecycle, /trackOwnedWork: \(unit -> Task\) -> unit/)
  assert.match(lifecycle, /fun _ outcome ->\s*trackOwnedWork \(fun \(\) ->/)
  assert.doesNotMatch(lifecycle, /fun _ outcome ->[\s\S]{0,240}\|> ignore/)

  assert.match(oneShot, /scope\.RunOwnedWork\(fun \(\) ->/)
  assert.doesNotMatch(oneShot, /settleCompletedTerminal scope childId terminal succeed latch completion\s*\|> ignore/)
})
