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
// Measured 2026-09-12 over the owner-compile production `.fs` closure: host 294, pty 295,
// recovery 45 (post Wave-6/Wire + Wave-7/OrchestratorPort + Wave-9 terminal sibling
// ports: TurnObservation, TerminalPolicy, HostJoinGuard, TerminalTrace, InteractionRepair,
// ProviderRecovery). The ratchet is held at the measured value, so any growth (including
// one new source file in a shared upstream shard) fails here and must be adjudicated
// against WHAT[DELEG-028] instead of passing silently.
const ADAPTER_RATCHET = new Map([
  ['delegation-host-adapter', 294],
  ['delegation-pty-adapter', 295],
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
  assert.equal(linkageOwner?.legacyKind, 'contract', 'delegation-linkage-projection must be a contract shard')
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
    assert.ok(
      !['composition', 'adapter', 'runtime'].includes(provider.legacyKind),
      `delegation fold must not depend on foreign ${provider.legacyKind} shard ${provider.shard}`,
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
  assert.equal(delegationProjectionOwner?.legacyKind, 'contract', 'the fold state vocabulary must be a contract shard')
  const bridgeOwner = projects.find((project) =>
    project.implementationFiles.includes(join(SOURCE_ROOT, 'Composition/Durable/DelegationProjectionBridge.fs')),
  )
  assert.equal(bridgeOwner?.shard, 'composition-durable-fold', 'composition-durable-fold must compile the delegation bridge')
  assert.equal(bridgeOwner?.legacyKind, 'composition', 'the delegation bridge must be a composition shard')
})
