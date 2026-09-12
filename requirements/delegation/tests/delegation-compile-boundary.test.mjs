import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { planOwnerCompile } from '../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')

const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
const projects = [...subsystemInventory.projects.values()]

const requireShard = (locality) => {
  const matches = projects.filter((project) => project.shard === locality)
  assert.equal(matches.length, 1, `${locality} must resolve to exactly one compile shard`)
  assert.equal(matches[0].subsystem, 'delegation', `${locality} belongs to the delegation subsystem`)
  return matches[0]
}

const inspectShard = (locality) => {
  const project = requireShard(locality)
  const plan = planOwnerCompile({ projectPath: project.projectPath, aggregatePath: AGGREGATE })
  const sources = plan.compileItems
    .filter((path) => path.endsWith('.fs'))
    .map((path) => path.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/'))
  return { project, sources }
}

// DELEG-028/029 adjudicate real source reachability, never project-level kind labels.
// A delegation vocabulary (contract / pure projection) shard may transitively carry
// Foundation identity/roles primitives and delegation-owned contract sources; any
// durable composition, physical adapter, tool, PTY or delegation-internal runtime
// source in the closure marks the shard as an implementation instead.
const rel = (file) => file.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/')

const IMPLEMENTATION_SOURCES = [
  /(?:^|\/)(?:Composition|Persistence|Process|OpenCode|Plugin|Sphinx|Host)\//,
  /Execution\/Delegation\/Fork\/Host\//,
  /Execution\/Delegation\/SyncDelegate\/(?:Wait|Store|Prompt|Workflow|Runtime)\.f[si]$/,
  /Execution\/Delegation\/Handle\/(?:Controller|JoinInterruptRegistry|CompletionCodec|JoinDrain)\.f[si]$/,
  /Execution\/Delegation\/ChildRecoveryWorkflow\.f[si]$/,
  /Execution\/Delegation\/.*\/OpenCode\//,
  /Execution\/Agent\/(?:Program|Errors)\.f[si]$/,
]

const visitClosure = (inventory, project, perProject) => {
  const seen = new Set()
  const visit = (path) => {
    if (seen.has(path)) return
    seen.add(path)
    const provider = inventory.projects.get(path)
    assert.ok(provider, `referenced project must exist in inventory: ${path}`)
    perProject(provider)
    for (const reference of provider.references) visit(reference)
  }
  visit(project.projectPath)
}

const closureSources = (inventory, project) => {
  const sources = []
  visitClosure(inventory, project, (provider) => sources.push(...provider.implementationFiles.map(rel)))
  return sources
}

const assertVocabularyClosure = (inventory, project, label) =>
  visitClosure(inventory, project, (provider) => {
    for (const file of provider.implementationFiles) {
      const relative = rel(file)
      for (const forbidden of IMPLEMENTATION_SOURCES) {
        assert.ok(!forbidden.test(relative), `${label} must not transitively compile implementation source ${relative}`)
      }
    }
  })

const SOURCE_BUDGETS = new Map([
  ['delegation-contract', 100],
  ['delegation-linkage-projection', 100],
  ['delegation-journal-port', 100],
  ['delegation-sync-contract', 100],
  ['delegation-fold', 185],
  ['delegation-sync-runtime', 185],
  ['delegation-fork-runtime', 185],
])

// WHAT[DELEG-028] budget adjudication (see WHY.md): contract ≤100 hard; fold/runtime target
// ≤185 hard; adapters carry the shared durable spine by charter — hard ceiling is the 60%
// full-fallback ratio with the measured baseline as a growth ratchet; composition exempt.
// Measured 2026-09-12 over the owner-compile production `.fs` closure: host 289, pty 290,
// recovery 45 (post Wave-6/Wire + Wave-7/OrchestratorPort + Wave-9 terminal sibling
// ports: TurnObservation, TerminalPolicy, HostJoinGuard, TerminalTrace, InteractionRepair,
// ProviderRecovery; 实测 2026-09-12 收口批后：orchestrator adapter 拆分为独立分片 `change-orchestrator-port-adapter` 回收 `Change/Fold`+`OrchestratorPort`+`Types` 三源、`ProviderRecoveryPort` 死文件删除；其余增长来自本轮新增的窄 contract 端口文件). The ratchet is held at the measured value, so any growth (including
// one new source file in a shared upstream shard) fails here and must be adjudicated
// against WHAT[DELEG-028] instead of passing silently.
const ADAPTER_RATCHET = new Map([
  ['delegation-host-adapter', 289],
  ['delegation-pty-adapter', 290],
  ['delegation-recovery-runtime', 45],
])

test('WHAT[DELEG-028] Delegation contract excludes workflow Host PTY and recovery sources', () => {
  const { sources } = inspectShard('delegation-contract')

  const forbidden = [
    /Execution\/Delegation\/SyncDelegate\/(?:Wait|Store|Prompt|Workflow|Runtime)\.fs$/,
    /Execution\/Delegation\/Fork\/Host\//,
    /Execution\/Delegation\/Handle\/(?:Controller|JoinInterruptRegistry|CompletionCodec|JoinDrain)\.fs$/,
    /Execution\/Delegation\/ChildRecoveryWorkflow\.fs$/,
    /Execution\/Delegation\/.*\/OpenCode\//,
    /Execution\/Agent\/(?:Program|Errors)\.fs$/,
    /Process\//,
  ]

  for (const pattern of forbidden) {
    assert.ok(!sources.some((source) => pattern.test(source)), `delegation contract leaks ${pattern}`)
  }
})

test('WHAT[DELEG-028] Delegation focused localities stay within compile budgets', () => {
  const aggregateSources = readFileSync(AGGREGATE, 'utf8')
    .match(/<Compile\s+Include="([^"]+\.fs)"/g)
    .map((m) => m.replace(/<Compile\s+Include="/, '').replace('"', ''))
    .filter((include) => include.endsWith('.fs')).length
  assert.ok(aggregateSources > 0, 'aggregate must declare production sources')
  const fullFallbackCeiling = Math.floor(aggregateSources * 0.6)

  for (const [locality, budget] of SOURCE_BUDGETS) {
    const inspected = inspectShard(locality)
    assert.ok(inspected.sources.length <= budget, `${locality} exceeds its production source budget ${budget}`)
  }
  for (const [locality, ratchet] of ADAPTER_RATCHET) {
    const inspected = inspectShard(locality)
    assert.ok(
      inspected.sources.length <= fullFallbackCeiling,
      `${locality} exceeds the 60% full-fallback ceiling (${inspected.sources.length} > ${fullFallbackCeiling})`,
    )
    assert.ok(
      inspected.sources.length <= ratchet,
      `${locality} grew beyond its recorded ratchet ${ratchet} — revise WHAT[DELEG-028] or shrink the closure`,
    )
  }

  const foldSources = inspectShard('delegation-fold').sources
  assert.ok(foldSources.includes('Execution/Delegation/DelegationFactFold.fs'))
  assert.ok(!foldSources.includes('Execution/Delegation/HandoffLedger.fs'))
  assert.ok(inspectShard('delegation-ledger').sources.includes('Execution/Delegation/HandoffLedger.fs'))
  const linkageOwner = projects.find(
    (project) => project.implementationFiles.includes(join(SOURCE_ROOT, 'Execution/Delegation/LinkageProjection.fs')),
  )
  assert.equal(linkageOwner?.subsystem, 'delegation', 'delegation subsystem must own LinkageProjection')
  assert.equal(linkageOwner?.shard, 'delegation-linkage-projection', 'delegation-linkage-projection shard must own LinkageProjection')
  // The vocabulary role is proven by transitive source membership, not by a declared
  // kind: an implementation source anywhere in this shard's closure is forbidden.
  assertVocabularyClosure(subsystemInventory, linkageOwner, 'delegation-linkage-projection')
  const spineProject = projects.find((project) => project.shard === 'composition-durable-projection')
  assert.ok(spineProject, 'shard composition-durable-projection must exist')
  assert.ok(
    linkageOwner && spineProject.references.includes(linkageOwner.projectPath),
    'composition-durable-projection must declare a ProjectReference to delegation-linkage-projection',
  )
  assert.ok(inspectShard('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Wait.fs'))
  assert.ok(inspectShard('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Store.fs'))
  assert.ok(inspectShard('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Prompt.fs'))
  assert.ok(inspectShard('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Workflow.fs'))
  assert.ok(!inspectShard('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Runtime.fs'))
  assert.ok(inspectShard('delegation-host-adapter').sources.includes('Execution/Delegation/SyncDelegate/Runtime.fs'))
  assert.ok(inspectShard('delegation-fork-runtime').sources.includes('Execution/Delegation/Fork/Runtime.fs'))
  assert.ok(inspectShard('delegation-host-adapter').sources.includes('Execution/Delegation/Fork/Host/Runtime.fs'))
  assert.ok(inspectShard('delegation-pty-adapter').sources.includes('Execution/Delegation/Fork/Host/Pty.fs'))
  assert.ok(inspectShard('delegation-recovery-runtime').sources.includes('Execution/Delegation/ChildRecoveryWorkflow.fs'))

  // DELEG-029 / DURABLE-EVENTS-023: the delegation fold owns linkage/estimate decisions and
  // emits pure changes; durable composition is the only place that assembles them back into
  // the aggregate projection. A runtime fold therefore must not name the aggregate, must not
  // close authority runs itself, and may only declare contract references.
  const foldProject = requireShard('delegation-fold')
  for (const source of foldProject.implementationFiles) {
    const text = readFileSync(source, 'utf8')
    assert.doesNotMatch(text, /\bAgentProjectionSet\b/, `${source} must not name the aggregate projection`)
    assert.doesNotMatch(text, /PromptAuthority/, `${source} must not close authority runs itself`)
    assert.doesNotMatch(text, /\bFoldRejection\b/, `${source} must not speak the durable fail-closed report`)
  }
  for (const reference of foldProject.references) {
    const provider = projects.find((project) => project.projectPath === reference)
    assert.ok(provider, `referenced project must exist in inventory: ${reference}`)
    if (provider.subsystem === 'delegation') continue
    assertVocabularyClosure(
      subsystemInventory,
      provider,
      `delegation fold foreign provider ${provider.shardKey}`,
    )
  }
  for (const spineSource of [
    'Composition/Durable/Projection.fs',
    'Composition/Durable/FoldRejection.fs',
    'Composition/Durable/Fold.fs',
  ]) {
    assert.ok(
      !foldSources.includes(spineSource),
      `delegation fold closure must not pull ${spineSource} out of the durable spine`,
    )
  }

  // The fold vocabulary is delegation-owned; applying it to the aggregate is composition's
  // single assembly point.
  const delegationProjectionOwner = projects.find((project) =>
    project.implementationFiles.includes(join(SOURCE_ROOT, 'Execution/Delegation/DelegationProjection.fs')),
  )
  assert.equal(delegationProjectionOwner?.subsystem, 'delegation', 'delegation must own its fold state vocabulary')
  assert.equal(
    delegationProjectionOwner?.shard,
    'delegation-linkage-projection',
    'delegation-linkage-projection must compile the delegation fold state vocabulary',
  )
  assertVocabularyClosure(subsystemInventory, delegationProjectionOwner, 'the delegation fold state vocabulary')
  const bridgeOwner = projects.find((project) =>
    project.implementationFiles.includes(join(SOURCE_ROOT, 'Composition/Durable/DelegationProjectionBridge.fs')),
  )
  assert.equal(bridgeOwner?.shard, 'composition-durable-fold', 'composition-durable-fold must compile the delegation bridge')
  // The bridge is the assembly point only if its shard really closes the aggregate
  // projection over the delegation fold vocabulary: its own source must name the
  // aggregate, and its closure must reach Composition/Durable/Projection.fs.
  const bridgeText = readFileSync(join(SOURCE_ROOT, 'Composition/Durable/DelegationProjectionBridge.fs'), 'utf8')
  assert.match(bridgeText, /\bAgentProjectionSet\b/, 'the delegation bridge must consume the aggregate projection')
  assert.ok(
    closureSources(subsystemInventory, bridgeOwner).includes('Composition/Durable/Projection.fs'),
    'composition-durable-fold must transitively compile the aggregate projection',
  )
})

test('WHAT[DELEG-028] DELEG-029 boundary ignores locality kind labels entirely', () => {
  // Positive: erasing every kind label keeps the boundary intact — it is decided by
  // transitive source membership alone.
  const kindless = new Map(projects.map((project) => [project.projectPath, { ...project, legacyKind: '' }]))
  const kindlessInventory = { ...subsystemInventory, projects: kindless }
  assertVocabularyClosure(
    kindlessInventory,
    requireShard('delegation-linkage-projection'),
    'delegation-linkage-projection',
  )
  for (const reference of requireShard('delegation-fold').references) {
    const provider = kindless.get(reference)
    if (provider.subsystem === 'delegation') continue
    assertVocabularyClosure(kindlessInventory, provider, `delegation fold foreign provider ${provider.shardKey}`)
  }

  // Negative: a real implementation shard labelled 'contract' inside the fold's
  // reference list is still adjudicated by source membership and rejected.
  for (const masquerading of ['persistence-journal-agentjournal', 'composition-durable-projection']) {
    const foreign = projects.find((project) => project.shard === masquerading)
    assert.ok(foreign, `real implementation provider must exist: ${masquerading}`)
    const mutated = new Map(kindless)
    mutated.set(foreign.projectPath, { ...foreign, legacyKind: 'contract' })
    const fold = requireShard('delegation-fold')
    mutated.set(fold.projectPath, { ...fold, references: [...fold.references, foreign.projectPath] })
    const mutatedInventory = { ...subsystemInventory, projects: mutated }
    assert.throws(
      () => assertVocabularyClosure(mutatedInventory, mutated.get(fold.projectPath), 'delegation-fold'),
      /must not transitively compile implementation source/,
      `a ${masquerading} shard masquerading as a contract must be rejected inside the fold closure`,
    )
  }

  // Indirect masquerade: an implementation shard hiding behind a same-subsystem
  // provider is equally rejected — the check walks transitive references.
  const journal = projects.find((project) => project.shard === 'persistence-journal-agentjournal')
  const intermediary = projects.find((project) => project.shard === 'delegation-journal-port')
  assert.ok(journal && intermediary, 'journal provider and delegation intermediary must exist')
  const throughDelegation = new Map(kindless)
  throughDelegation.set(intermediary.projectPath, {
    ...intermediary,
    references: [...intermediary.references, journal.projectPath],
  })
  assert.throws(
    () => assertVocabularyClosure({ ...subsystemInventory, projects: throughDelegation }, intermediary, 'delegation-journal-port'),
    /must not transitively compile implementation source Persistence\/Journal\//,
  )
})
