import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')

const walkFs = (dir) => {
  const absolute = join(ROOT, dir)
  return readdirSync(absolute).flatMap((name) => {
    const relative = join(dir, name)
    const full = join(ROOT, relative)
    return statSync(full).isDirectory() ? walkFs(relative) : relative.endsWith('.fs') ? [relative] : []
  })
}

test('WHAT[MANAGED-SESSION-017] fail-closed interrupt becomes Failed terminal so fork completion wakes parent', () => {
  const sessions = read('src/Wanxiangshu/OpenCode/Host/Sessions.fs')
  const pluginTransforms = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')
  const lifecycle = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs')
  const runtime = read('src/Wanxiangshu/Execution/Delegation/Fork/Runtime.fs')

  const hostPort = read('src/Wanxiangshu/OpenCode/Host/SessionHostPort.fs')
  const termination = sessions.match(/module ManagedSessionTermination =([\s\S]*?)type InjectedSessionPort/)
  assert.ok(termination, 'managed termination CE must be inspectable')
  assert.match(termination[1], /not \(sessionPort\.IsManagedChild sessionId\)/)
  assert.match(hostPort, /abstract IsManagedChild: sessionId: SessionId -> bool/)
  assert.match(sessions, /member _\.IsManagedChild\(sessionId\) = managedChild sessionId/)
  assert.match(
    termination[1],
    /cancelSessionChildren sessionId[\s\S]*?sessionPort\.AbortSession sessionId[\s\S]*?NotifyTerminal[\s\S]*?TerminalOutcome\.Failed\(TerminalStop\.forAuthority authorityRoot reason\)/,
  )
  assert.doesNotMatch(termination[1], /TerminalStop\.session reason/)
  assert.match(read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs'), /currentPhysicalUserMessage sessionId[\s\S]*?PhysicalUserMessageId\.promoteToAuthorityRoot/)

  const terminatePhysical = pluginTransforms.match(/let terminatePhysical sessionId reason physical =([\s\S]*?)let terminateSession/)
  assert.ok(terminatePhysical, 'physical managed termination owner must be inspectable')
  assert.match(
    terminatePhysical[1],
    /ManagedSessionTermination\.terminate[\s\S]*?physical\s*\|> PhysicalUserMessageId\.create\s*\|> PhysicalUserMessageId\.promoteToAuthorityRoot/,
  )

  const terminateSession = pluginTransforms.match(/let terminateSession: SessionTermination =([\s\S]*?)let freezeProviderAttemptPlan/)
  assert.ok(terminateSession, 'provider termination dispatch must be inspectable')
  assert.match(
    terminateSession[1],
    /wired\.CurrentPhysicalUserMessage\(SessionId\.value sessionId\)\s*\|> Option\.map \(terminatePhysical sessionId reason\)/,
  )
  assert.match(lifecycle, /let private stopBelongsToRun[\s\S]*?TerminalStop\.belongsTo root stop/)
  assert.match(lifecycle, /\| Failed stop when not \(stopBelongsToRun run stop\) -> Task\.FromResult\(\(\)\)/)
  assert.match(lifecycle, /\| Failed stop -> deliverFailedCompletion/)
  assert.match(runtime, /PulseAgentHandle/)
  assert.doesNotMatch(sessions, /attemptTerminations|TryTakeAttemptTermination|TerminateAttempt/)
})

test('WHAT[MANAGED-SESSION-017] invariant and tool fail-closed paths cannot use orphan InterruptAttempt', () => {
  const fatalOwners = [
    'src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs',
    'src/Wanxiangshu/OpenCode/Tools/ChronicleTool.fs',
    'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs',
  ]

  for (const path of fatalOwners) {
    const source = read(path)
    assert.doesNotMatch(source, /\.InterruptAttempt\(/, `${path} must not create a successor-less physical abort`)
    assert.doesNotMatch(source, /\.TerminateAttempt\(/, `${path} must not heap-bridge termination through a future abort`)
  }

  assert.match(read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs'), /ManagedSessionTermination\.terminate/)
  assert.match(read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'), /ManagedSessionTermination\.terminate/)
})

test('WHAT[MANAGED-SESSION-017] raw InterruptAttempt callers are restricted to workflows with an explicit successor owner', () => {
  // Relay clean break: Review/Finality owners are deleted, and retirement no
  // longer issues a session-scoped abort (RETIRE-008) — a late Host kill lands
  // in the successor's run on the reused session.
  const allowed = new Set([
    'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
    'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs',
    'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs',
  ])

  const callers = walkFs('src/Wanxiangshu')
    .filter((path) => /\.InterruptAttempt\b/.test(read(path)))
    .filter((path) => !path.endsWith('/Sessions.fs'))
    .filter((path) => !path.endsWith('Surface.fs'))
    .filter((path) => !path.endsWith('/DispatchSurface.fs'))
    .filter((path) => !path.endsWith('/ReviewHostSurface.fs'))
    .filter((path) => !path.endsWith('/FissionHostSurface.fs'))

  assert.deepEqual(new Set(callers), allowed)
})

test('WHAT[MANAGED-SESSION-017] failed Host abort rolls back Loop one-shot causes', () => {
  const loop = read('src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')

  const loopOutcome = loop.match(/member private this\.ApplyInterruptOutcome([\s\S]*?)member private this\.RunInterrupt/)
  assert.ok(loopOutcome, 'Loop abort outcome classifier must be inspectable')
  assert.match(loopOutcome[1], /\| Error _ ->[\s\S]*?this\.RollbackArm sessionId/)

  const loopAbort = loop.match(/member private this\.RunInterrupt([\s\S]*?)member private this\.Interrupt/)
  assert.ok(loopAbort, 'Loop abort observation must be inspectable')
  assert.match(loopAbort[1], /this\.ApplyInterruptOutcome/)
  assert.match(loopAbort[1], /with ex ->[\s\S]*?this\.RollbackArm sessionId/)
})

test('WHAT[MANAGED-SESSION-017] fatal termination never stores cross-callback cause state', () => {
  const sources = walkFs('src/Wanxiangshu').map((path) => [path, read(path)])
  for (const [path, source] of sources) {
    assert.doesNotMatch(source, /attemptTerminations|TryTakeAttemptTermination|AbortCause\.InternalTermination/, path)
  }
})
