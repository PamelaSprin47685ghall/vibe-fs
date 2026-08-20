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
  const lifecycle = read('src/Wanxiangshu/Execution/Delegation/Fork/Host/RunLifecycle.fs')
  const runtime = read('src/Wanxiangshu/Execution/Delegation/Fork/Runtime.fs')

  const termination = sessions.match(/module ManagedSessionTermination =([\s\S]*?)type InjectedSessionPort/)
  assert.ok(termination, 'managed termination CE must be inspectable')
  assert.match(termination[1], /not \(sessionPort\.IsManagedChild sessionId\)/)
  assert.match(sessions, /abstract IsManagedChild: sessionId: SessionId -> bool/)
  assert.match(sessions, /member _\.IsManagedChild\(sessionId\) = managedChild sessionId/)
  assert.match(
    termination[1],
    /cancelSessionChildren sessionId[\s\S]*?sessionPort\.AbortSession sessionId[\s\S]*?NotifyTerminal[\s\S]*?TerminalOutcome\.Failed\(TerminalStop\.forAuthority authorityRoot reason\)/,
  )
  assert.doesNotMatch(termination[1], /TerminalStop\.session reason/)
  assert.match(read('src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fs'), /currentPhysicalUserMessage sessionId[\s\S]*?PhysicalUserMessageId\.promoteToAuthorityRoot/)
  assert.match(read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'), /wired\.CurrentPhysicalUserMessage[\s\S]*?PhysicalUserMessageId\.promoteToAuthorityRoot/)
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
  const allowed = new Set([
    'src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs',
    'src/Wanxiangshu/Mission/Finality/OpenCode/HostPort.fs',
    'src/Wanxiangshu/Mission/Review/OpenCode/JudgeTool.fs',
    'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs',
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

test('WHAT[MANAGED-SESSION-017] failed Host abort rolls back Loop and NeedHelp one-shot causes', () => {
  const loop = read('src/Wanxiangshu/OpenCode/Host/LoopSensor.fs')
  const needHelp = read('src/Wanxiangshu/Interaction/Dispatch/OpenCode/NeedHelpSensor.fs')

  const loopOutcome = loop.match(/member private this\.ApplyInterruptOutcome([\s\S]*?)member private this\.RunInterrupt/)
  assert.ok(loopOutcome, 'Loop abort outcome classifier must be inspectable')
  assert.match(loopOutcome[1], /\| Error _ ->[\s\S]*?this\.RollbackArm sessionId/)

  const loopAbort = loop.match(/member private this\.RunInterrupt([\s\S]*?)member private this\.Interrupt/)
  assert.ok(loopAbort, 'Loop abort observation must be inspectable')
  assert.match(loopAbort[1], /this\.ApplyInterruptOutcome/)
  assert.match(loopAbort[1], /with ex ->[\s\S]*?this\.RollbackArm sessionId/)

  const needHelpOutcome = needHelp.match(/member private this\.ApplyAbortOutcome([\s\S]*?)member private this\.AbortAndReport/)
  assert.ok(needHelpOutcome, 'NeedHelp abort outcome classifier must be inspectable')
  assert.match(needHelpOutcome[1], /\| Error reason ->[\s\S]*?RollbackArm/)

  const needHelpAbort = needHelp.match(/member private this\.AbortAndReport([\s\S]*?)member private this\.RequestAbort/)
  assert.ok(needHelpAbort, 'NeedHelp abort observation must be inspectable')
  assert.match(needHelpAbort[1], /this\.RollbackArm\(sessionId, providerRun\)/)
  assert.match(needHelpAbort[1], /with ex ->[\s\S]*?RollbackArm/)
})

test('WHAT[MANAGED-SESSION-017] fatal termination never stores cross-callback cause state', () => {
  const sources = walkFs('src/Wanxiangshu').map((path) => [path, read(path)])
  for (const [path, source] of sources) {
    assert.doesNotMatch(source, /attemptTerminations|TryTakeAttemptTermination|AbortCause\.InternalTermination/, path)
  }
})
