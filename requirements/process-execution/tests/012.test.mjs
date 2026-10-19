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
  const { cpSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
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
      ['js-run-loop', 'JsRunLoop.execute'],
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
