import assert from 'node:assert/strict'
import test from 'node:test'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { assertEffectIsInjected, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const causal = await import('../../../dist/Execution/Session/Wait/Surface.js')
const boundaryGate = await import('../../../scripts/checks/causal-wait-boundary.mjs')

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')

const mkWait = (waitKind, ownerKind, subject, producer) =>
  causal.createWait({
    waitKind,
    owner: causal.owner(ownerKind, { id: ownerKind }),
    subject,
    producer,
    escapes: [causal.escape('processLifetime')],
    source: 'causal-wait-bridge.test',
  })

const readDiagnostic = (workspace) =>
  JSON.parse(fs.readFileSync(path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json'), 'utf8'))

const requireShard = (projects, shardId) => {
  const matches = [...projects.values()].filter((candidate) => candidate.shard === shardId)
  assert.equal(matches.length, 1, `${shardId} must resolve to exactly one production compile shard`)
  return matches[0]
}

const relSources = (project) => project.implementationFiles.map((p) => path.relative(ROOT, p)).sort()
const refShards = (project, projects) => project.references.map((refPath) => projects.get(refPath).shard).sort()

const mutate = (relativePath, addition) => {
  const clean = [
    {
      rel: 'Interaction/Dispatch/NewDecision.fs',
      text: '// IWaitSnapshotReader and CausalWaitHub.snapshot are lexical decoys\nlet ordinary = "IWaitSnapshotReader CausalWaitHub.snapshot"\n',
    },
  ]
  return clean.map((file) =>
    file.rel === relativePath ? { ...file, text: file.text + addition } : file,
  )
}

test('WHAT[CAUSAL-009] analyzer rejects a global observer hub in a business workflow', () => {
  const violations = boundaryGate.analyzeObservationBoundary(
    mutate('Interaction/Dispatch/NewDecision.fs', 'let observer = CausalWaitHub.observer\n'),
  )
  assert.deepEqual(violations, [
    'Interaction/Dispatch/NewDecision.fs: diagnostics read capability "CausalWaitHub" is confined to Execution/Session/Wait',
  ])
})

test('WHAT[CAUSAL-009] analyzer rejects the Node diagnostic adapter outside composition', () => {
  const violations = boundaryGate.analyzeObservationBoundary(
    mutate('Interaction/Dispatch/NewDecision.fs', 'let sink = CausalWaitBridge.target workspace\n'),
  )
  assert.deepEqual(violations, [
    'Interaction/Dispatch/NewDecision.fs: diagnostics read capability "CausalWaitBridge" is confined to Execution/Session/Wait',
  ])
})

test('WHAT[CAUSAL-009] CAUSAL_BRIDGE_first_binding_is_stable_and_refreshes_on_lifecycle', () => {
  const workspace = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-runtime-'))
  const redirect = fs.mkdtempSync(path.join(os.tmpdir(), 'causal-redirect-'))
  const registry = causal.createRegistry()
  assert.equal(causal.bindDiagnosticWorkspace(registry, workspace), true)
  assert.equal(causal.bindDiagnosticWorkspace(registry, redirect), false)
  const wait = mkWait(
    'manager-job',
    'OrchestratorWorkflow',
    { job: 'OJ7', manager: 'M4' },
    causal.workflowProducer(causal.owner('ManagerWorkflow', { session: 'M4' })),
  )
  const lease = causal.enter(registry, wait)
  try {
    const filePath = path.join(workspace, '.wanxiangshu', 'diagnostics', 'causal-waits.json')
    assert.equal(fs.existsSync(filePath), true)
    assert.ok(readDiagnostic(workspace).active.some((value) => value.waitKind === 'manager-job'))
    assert.equal(fs.existsSync(path.join(redirect, '.wanxiangshu', 'diagnostics', 'causal-waits.json')), false)
  } finally {
    causal.dispose(lease)
    assert.equal(readDiagnostic(workspace).active.length, 0)
    fs.rmSync(workspace, { recursive: true, force: true })
    fs.rmSync(redirect, { recursive: true, force: true })
  }
})

test('WHAT[CAUSAL-009] production inventory separates contract runtime adapter mailbox and proof surface', () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = subsystemInventory.projects

  const contract = requireShard(projects, 'execution-session-wait-contract')
  const runtime = requireShard(projects, 'execution-session-wait-runtime')
  const adapter = requireShard(projects, 'execution-session-wait-diagnostic-adapter')
  const mailbox = requireShard(projects, 'execution-session-wait-completion-mailbox')
  const proof = requireShard(projects, 'execution-session-wait-proof-surface')

  assert.equal(contract.subsystem, 'session-lifecycle')
  assert.equal(runtime.subsystem, 'session-lifecycle')
  assert.equal(adapter.subsystem, 'session-lifecycle')
  assert.equal(mailbox.subsystem, 'delegation')
  assert.equal(proof.subsystem, 'application-composition',
    'proof surface is a JS-side Surface; it sits in application-composition per B09/B10')

  assert.deepEqual(relSources(contract), ['src/Wanxiangshu/Execution/Session/Wait/CausalWait.fs'])
  assert.deepEqual(relSources(runtime), [
    'src/Wanxiangshu/Execution/Session/Wait/Await.fs',
    'src/Wanxiangshu/Execution/Session/Wait/Registry.fs',
  ])
  assert.deepEqual(relSources(adapter), ['src/Wanxiangshu/Execution/Session/Wait/Bridge.fs'])
  assert.deepEqual(relSources(mailbox), ['src/Wanxiangshu/Execution/Session/Wait/CompletionMailbox.fs'])
  assert.deepEqual(relSources(proof), ['src/Wanxiangshu/Execution/Session/Wait/Surface.fs'])

  assert.deepEqual(refShards(contract, projects), [])
  assert.deepEqual(refShards(adapter, projects), ['execution-session-wait-contract'])
  assert.deepEqual(refShards(runtime, projects), [
    'execution-session-wait-contract',
    'foundation-temporal-contract',
  ])

  for (const id of [
    'delegation-runtime-surface',
    'git-integrationgate',
    'opencode-tools-toolruntimescope',
  ]) {
    const composition = requireShard(projects, id)
    assert.ok(refShards(composition, projects).includes(mailbox.shard), `${id} must declare its physical mailbox provider`)
  }
  assert.equal(
    refShards(requireShard(projects, 'delegation-host-adapter'), projects).includes(mailbox.shard),
    false,
    'the Host adapter must receive a mailbox factory instead of constructing a foreign runtime',
  )
  assert.equal(
    refShards(requireShard(projects, 'delegation-fork-runtime'), projects).includes(mailbox.shard),
    false,
    'the Fork runtime must consume only the injected mailbox capability',
  )
  assert.equal([...projects.values()].some((p) => p.shard === 'execution-session-wait-causalwait'), false)
  assert.deepEqual(
    [...projects.values()].filter((p) => refShards(p, projects).includes(proof.shard)),
    [],
    'proof surface must not provide production capability',
  )
})

test('WHAT[CAUSAL-009] causal wait contract excludes registry diagnostics mailbox and proof runtime', () => {
  assertPureContract()
  assertEffectIsInjected('console')
})
