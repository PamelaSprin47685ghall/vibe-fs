import assert from 'node:assert/strict'
import test from 'node:test'
import path from 'node:path'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'

const ROOT = path.resolve(import.meta.dirname, '../../..')

test('WHAT[process-execution-012] request vocabulary is compiled separately from process and PTY runtime', () => {
  const inventory = buildSubsystemInventory({ compileInventory: readCompileShardInventory({ repositoryRoot: ROOT }) })
  assert.equal(inventory.ok, true, inventory.violations.join('\n'))
  const owners = relative => {
    const file = path.join(ROOT, 'src/Wanxiangshu', relative)
    return [...inventory.projects.values()].filter(project => project.implementationFiles.includes(file))
  }
  const [request] = owners('Process/ProcessRequest.fs')
  assert.ok(request)
  for (const relative of ['Process/NodeProcessHost.fs', 'Process/ProcessRunner.fs', 'Process/PtySession.fs', 'Process/Spool.fs']) {
    const [runtime] = owners(relative)
    assert.ok(runtime)
    assert.notEqual(runtime.shard, request.shard, `${relative} must not share the request vocabulary compilation unit`)
  }
})

const { integrationTest } = await import('../../verification-system/tests/support/tier-gate.mjs')

integrationTest('WHAT[process-execution-012] an isolated Fable consumer uses request and PTY contracts but cannot reach physical adapters', async () => {
  const { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { execFileSync } = await import('node:child_process')
  const { pathToFileURL } = await import('node:url')
  const { walk } = await import('../../../scripts/lib/walk.mjs')
  const { compileOwnerProject, planOwnerCompile } = await import('../../../scripts/lib/owner-compile.mjs')
  const source = path.join(ROOT, 'src/Wanxiangshu')
  const project = path.join(source, 'Wanxiangshu.Owner.process-execution.process-processrequest.fsproj')
  const plan = planOwnerCompile({ projectPath: project })
  assert.deepEqual(plan.compileItems.map(file => path.relative(source, file)), [
    'Process/ProcessRequest.fsi', 'Process/ProcessRequest.fs', 'Process/PtyTypes.fsi', 'Process/PtyTypes.fs',
  ])
  const scratch = mkdtempSync(path.join(tmpdir(), 'wxs-process-contract-'))
  const compile = async (name, body, selected = plan) => {
    const directory = path.join(scratch, name)
    const items = selected.compileItems.map(file => {
      const target = path.join(directory, path.relative(source, file))
      mkdirSync(path.dirname(target), { recursive: true })
      cpSync(file, target)
      return target
    })
    const probe = path.join(directory, 'Consumer.fs')
    writeFileSync(probe, body)
    return compileOwnerProject({ projectPath: selected.projectPath, scratchRoot: directory, stdio: 'pipe', compilePlan: { ...selected, compileItems: [...items, probe] } })
  }
  try {
    const positive = await compile('legal', `module ContractConsumer
open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Process
let request = { FileName = "printf"; Arguments = [ "hello" ]; WorkingDirectory = None; Environment = None; Stdin = None }
let estimate = { EstimatedRuntime = RuntimeSeconds 1.0; EstimatedOutput = OutputBytes 64L; EstimatedMemory = EstimatedMemory.Medium }
let context = { WorkingDirectory = None; HardLimit = TimeSpan.FromSeconds 1.0 }
let execute (run: RunProcess) = run request estimate context CancellationToken.None
let terminal = PtyCommand.Signal PtySignal.Interrupt
`)
    assert.equal(positive.ok, true, String(positive.stdout) + String(positive.stderr))
    for (const [name, expression] of [
      ['spawn', 'NodeProcessHost.spawn'], ['runner', 'ProcessRunner.run'],
      ['session', 'PtySession.create "id" null'], ['spool', 'Spool.delete "path"'],
      ['read-plan', 'ReadPlan.ClosedImmediate'],
    ]) {
      const negative = await compile(name, `module IllegalConsumer\nopen Wanxiangshu.Process\nlet leaked = ${expression}\n`)
      assert.equal(negative.ok, false, `${name} must not be reachable through the pure contract closure`)
      assert.match(String(negative.stdout) + String(negative.stderr), /not defined|FS0039/)
    }
    for (const [name, body] of [
      ['unknown-is-not-end', 'let invalid: ProgramEnd = EndEvidence.Unconfirmed'],
      ['signal-is-not-code', 'let invalid: int = ProgramEnd.Signaled "SIGTERM"'],
    ]) {
      const negative = await compile(name, `module IllegalEvidence\nopen Wanxiangshu.Process\n${body}\n`)
      assert.equal(negative.ok, false)
      assert.match(String(negative.stdout) + String(negative.stderr), /expected to have type|type.*does not match|FS0001/s)
    }
    const gateway = planOwnerCompile({ projectPath: path.join(source, 'Wanxiangshu.Owner.change-integration.git-gateway.fsproj') })
    const consumer = await compile('gateway', `module GatewayConsumer
open System
open System.Threading.Tasks
open Wanxiangshu.Process
open Wanxiangshu.Git
open Wanxiangshu.Persistence.EventStore
let mutable calls = 0
let execute: RunProcess = fun command estimate context cancellation ->
    calls <- calls + 1
    let (RuntimeSeconds seconds) = estimate.EstimatedRuntime
    let evidence = String.concat ":" [ command.FileName; String.concat "," command.Arguments; defaultArg command.WorkingDirectory "missing"; string seconds; string context.HardLimit.TotalSeconds; string cancellation.IsCancellationRequested ]
    Task.FromResult(ProcessResult.Observed { End = EndEvidence.Confirmed(ProgramEnd.Exited 17); StopRequested = None; Output = Some(CapturedOutput.InMemory(evidence, "injected")); Fault = None })
let raw =
    { new IGitRawStore with
        member _.WriteBlob _ = failwith "unexpected blob write"
        member _.WriteTree _ = failwith "unexpected tree write"
        member _.ReadObject _ = failwith "unexpected object read"
        member _.ReadTree _ = failwith "unexpected tree read"
        member _.ReadRef _ = Task.FromResult(Ok None) }
let verifyUnknownPush () = task {
    let mutable pushes = 0
    let mutable now = DateTimeOffset.Parse "2026-10-18T00:00:00Z"
    let deadline = Deadline.ofBudget now (TimeSpan.FromSeconds 1.0)
    let snapshot = { RootOid = RootOid.create (GitObjectId.create (String.replicate 40 "a")) }
    let runner args = task {
        if List.head args <> "push" then failwith "unknown publication must not rediscover"
        pushes <- pushes + 1
        now <- now.AddSeconds 2.0
        return Error(ProcessResult.Observed { End = EndEvidence.Unconfirmed; StopRequested = None; Output = None; Fault = Some "original publication failure" })
    }
    let! result = GitGateway.converge raw "/owned/unknown-publication" runner 3 "origin" None (fun _ -> Task.FromResult(Ok snapshot)) deadline (fun () -> now)
    if pushes <> 1 then failwith "unknown publication was sent again"
    match result with
    | Error(ConvergeError.Transport reason) when reason.Contains "original publication failure" -> return ()
    | _ -> return failwith "original execution failure was replaced"
}
let exercise () = task {
    do! verifyUnknownPush()
    let! result = GitGateway.createProcessRunner execute "/owned/gateway" [ "status"; "--short" ]
    match result with
    | Ok exited -> return sprintf "%d|%d|%s|%s" calls exited.ExitCode exited.Stdout exited.Stderr
    | Error failure -> return failwith (ProcessResult.describe failure)
}
`, gateway)
    assert.equal(consumer.ok, true, String(consumer.stdout) + String(consumer.stderr))
    const dependencies = JSON.parse(readFileSync(path.join(ROOT, 'package.json'))).dependencies
    for (const name of ['proper-lockfile', 'wanxiangli', '@fable-org/fable-library-js']) {
      assert.ok(dependencies[name], `${name} must be a declared runtime dependency`)
      const destination = path.join(scratch, 'node_modules', name)
      mkdirSync(path.dirname(destination), { recursive: true })
      symlinkSync(path.join(ROOT, 'node_modules', name), destination, 'dir')
    }
    const outputs = walk(path.join(scratch, 'gateway'), ['.js'])
    const probes = outputs.filter(file => path.basename(file) === 'Consumer.js')
    assert.equal(probes.length, 1, `the compiled public consumer must have one emitted entry: ${outputs.join(', ')}`)
    const moduleUrl = pathToFileURL(probes[0]).href
    const output = execFileSync(process.execPath, ['--input-type=module', '-e', `import { exercise } from ${JSON.stringify(moduleUrl)}; process.stdout.write(await exercise());`], { encoding: 'utf8' })
    assert.equal(output, '1|17|git:status,--short:/owned/gateway:120:3600:false|injected')
    const retired = await compile('retired-default', 'module IllegalDefault\nopen Wanxiangshu.Git\nlet leaked = GitGateway.createDefaultRunner "/repo"\n', gateway)
    assert.equal(retired.ok, false, 'the old default runner entry must be removed')
    assert.match(String(retired.stdout) + String(retired.stderr), /not defined|FS0039/)
    const pty = planOwnerCompile({ projectPath: path.join(source, 'Wanxiangshu.Owner.delegation.delegation-pty-port.fsproj') })
    assert.equal(pty.compileItems.some(file => /Process\/(?:Pty|PtyBackend|PtySession)\.fs$/.test(file)), false)
    const ptyConsumer = await compile('pty-consumer', `module PtyConsumer
open Wanxiangshu.Execution.Delegation
let lookup (capability: DelegationPtyCapability) name = capability.TryPtyByName name
let openTerminal (capability: DelegationPtyCapability) command agent = capability.ForkPty(command, agent, None)
`, pty)
    assert.equal(ptyConsumer.ok, true, String(ptyConsumer.stdout) + String(ptyConsumer.stderr))
    const physicalPty = await compile('pty-physical', 'module IllegalPty\nopen Wanxiangshu.Process\nlet leaked = PtyBackend.createPort ()\n', pty)
    assert.equal(physicalPty.ok, false, 'the real PTY consumer contract must not import its physical adapter')
    assert.match(String(physicalPty.stdout) + String(physicalPty.stderr), /not defined|FS0039/)
  } finally {
    rmSync(scratch, { recursive: true, force: true })
  }
})
