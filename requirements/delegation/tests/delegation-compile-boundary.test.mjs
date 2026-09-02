import assert from 'node:assert/strict'
import { readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import test from 'node:test'
import { planOwnerCompile } from '../../../scripts/lib/owner-compile.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')
const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')
const AGGREGATE = join(SOURCE_ROOT, 'Wanxiangshu.fsproj')

const projects = readdirSync(SOURCE_ROOT)
  .filter((name) => /^Wanxiangshu\.Owner\..+\.fsproj$/.test(name))
  .map((name) => {
    const path = join(SOURCE_ROOT, name)
    const xml = readFileSync(path, 'utf8')
    return {
      path,
      locality: xml.match(/<WanxiangshuOwnerLocality>([^<]+)<\/WanxiangshuOwnerLocality>/)?.[1],
      kind: xml.match(/<WanxiangshuOwnerLocalityKind>([^<]+)<\/WanxiangshuOwnerLocalityKind>/)?.[1],
    }
  })

const requireLocality = (locality) => {
  const matches = projects.filter((project) => project.locality === locality)
  assert.equal(matches.length, 1, `${locality} must resolve to exactly one owner project`)
  return matches[0]
}

const inspectLocality = (locality) => {
  const project = requireLocality(locality)
  const plan = planOwnerCompile({ projectPath: project.path, aggregatePath: AGGREGATE })
  const sources = plan.compileItems
    .filter((path) => path.endsWith('.fs'))
    .map((path) => path.slice(SOURCE_ROOT.length + 1).replaceAll('\\', '/'))
  return { project, sources }
}

const EXPECTED_KINDS = new Map([
  ['delegation-contract', 'contract'],
  ['delegation-sync-contract', 'contract'],
  ['delegation-fold', 'runtime'],
  ['delegation-ledger', 'composition'],
  ['delegation-sync-runtime', 'runtime'],
  ['delegation-fork-runtime', 'runtime'],
  ['delegation-host-adapter', 'adapter'],
  ['delegation-pty-adapter', 'adapter'],
  ['delegation-recovery-runtime', 'runtime'],
])

// WHAT[DELEG-028] budget adjudication (see WHY.md): contract ≤100 hard; fold/runtime target
// ≤185 hard; adapters carry the shared durable spine by charter — hard ceiling is the 60%
// full-fallback ratio with the measured baseline as a growth ratchet; composition exempt.
const ADAPTER_RATCHET = new Map([
  ['delegation-host-adapter', 315],
  ['delegation-pty-adapter', 316],
  ['delegation-recovery-runtime', 193],
])

test('WHAT[DELEG-028] Delegation contract excludes workflow Host PTY and recovery sources', () => {
  const { project, sources } = inspectLocality('delegation-contract')
  assert.equal(project.kind, 'contract')

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

  for (const [locality, kind] of EXPECTED_KINDS) {
    const inspected = inspectLocality(locality)
    assert.equal(inspected.project.kind, kind, `${locality} must declare ${kind} kind`)
    if (kind === 'composition') {
      continue
    }
    if (kind === 'adapter' || ADAPTER_RATCHET.has(locality)) {
      assert.ok(
        inspected.sources.length <= fullFallbackCeiling,
        `${locality} exceeds the 60% full-fallback ceiling (${inspected.sources.length} > ${fullFallbackCeiling})`,
      )
      const ratchet = ADAPTER_RATCHET.get(locality)
      assert.ok(ratchet, `${locality} must declare a measured ratchet in WHAT[DELEG-028]`)
      assert.ok(
        inspected.sources.length <= ratchet,
        `${locality} grew beyond its recorded ratchet ${ratchet} — revise WHAT[DELEG-028] or shrink the closure`,
      )
      continue
    }
    const budget = kind === 'contract' ? 100 : 185
    assert.ok(inspected.sources.length <= budget, `${locality} exceeds its production source budget ${budget}`)
  }

  const foldSources = inspectLocality('delegation-fold').sources
  assert.ok(foldSources.includes('Execution/Delegation/DelegationFactFold.fs'))
  assert.ok(!foldSources.includes('Execution/Delegation/HandoffLedger.fs'))
  assert.ok(inspectLocality('delegation-ledger').sources.includes('Execution/Delegation/HandoffLedger.fs'))
  // LinkageProjection/DelegatedToolEstimateProjection are owned by the durable projection spine
  // (Composition/Durable/Projection.fsi owns Handles/DelegatedToolEstimate fields); fold consumes
  // them through that reference — the spine's owner project is the only compiling locality.
  const spineOwner = projects.find(
    (p) => p.kind === 'composition' && readFileSync(p.path, 'utf8').includes('Execution/Delegation/LinkageProjection.fs'),
  )
  assert.ok(spineOwner, 'durable projection spine must own LinkageProjection')
  assert.ok(inspectLocality('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Wait.fs'))
  assert.ok(inspectLocality('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Store.fs'))
  assert.ok(inspectLocality('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Prompt.fs'))
  assert.ok(inspectLocality('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Workflow.fs'))
  assert.ok(!inspectLocality('delegation-sync-runtime').sources.includes('Execution/Delegation/SyncDelegate/Runtime.fs'))
  assert.ok(inspectLocality('delegation-host-adapter').sources.includes('Execution/Delegation/SyncDelegate/Runtime.fs'))
  assert.ok(inspectLocality('delegation-fork-runtime').sources.includes('Execution/Delegation/Fork/Runtime.fs'))
  assert.ok(inspectLocality('delegation-host-adapter').sources.includes('Execution/Delegation/Fork/Host/Runtime.fs'))
  assert.ok(inspectLocality('delegation-pty-adapter').sources.includes('Execution/Delegation/Fork/Host/Pty.fs'))
  assert.ok(inspectLocality('delegation-recovery-runtime').sources.includes('Execution/Delegation/ChildRecoveryWorkflow.fs'))
})
