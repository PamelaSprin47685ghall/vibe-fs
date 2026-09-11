import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')

test('WHAT[DELEG-029] delegation runtime consumes only the delegation-owned journal port', () => {
  const compileInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))

  const recoveryRuntime = [...subsystemInventory.projects.values()].find(
    (project) => project.shard === 'delegation-recovery-runtime',
  )
  assert.ok(recoveryRuntime, 'shard delegation-recovery-runtime must exist')

  // 1. Every source of shard delegation-recovery-runtime is free of the foreign durable handle and the outer routing union
  const recoverySources = [...recoveryRuntime.implementationFiles, ...recoveryRuntime.signatureFiles]
  assert.ok(recoverySources.length > 0, 'delegation-recovery-runtime must have sources')
  for (const sourcePath of recoverySources) {
    const text = readFileSync(sourcePath, 'utf8')
    assert.doesNotMatch(text, /\bAgentJournal(?!Port)\b/, `${sourcePath} must not reference foreign AgentJournal`)
    assert.doesNotMatch(text, /\bAgentFact\b/, `${sourcePath} must not reference outer routing union AgentFact`)
    assert.doesNotMatch(text, /Wanxiangshu\.Persistence/, `${sourcePath} must not reference Wanxiangshu.Persistence`)
    assert.doesNotMatch(text, /Composition\.Durable/, `${sourcePath} must not reference Composition.Durable`)
    assert.doesNotMatch(text, /\.Writer\./, `${sourcePath} must not reach into .Writer.`)
  }

  // 2. The recovery runtime never reaches the durable handle, the outer routing union or the
  //    journal codec: DELEG-029 keeps those at durable composition, and the runtime consumes
  //    only the delegation-owned port. The single composition-kind provider left is the
  //    durable projection spine, which carries the delegation-owned linkage projection type
  //    and its pure transition functions (compiled there to break a fold circularity — see
  //    2ee76f2dc "move LinkageProjection/FoldRejection to composition-durable-projection").
  const durableCompositionOnly = new Set([
    'persistence-journal-agentjournal',
    'persistence-journal-promptfactcodec',
    'persistence-journal-eventstorewriter',
    'composition-durable-fact',
  ])
  const pureProjectionSpine = new Set(['composition-durable-projection'])
  assert.ok(recoveryRuntime.references.length > 0, 'delegation-recovery-runtime must have references')
  for (const reference of recoveryRuntime.references) {
    const provider = subsystemInventory.projects.get(reference)
    assert.ok(provider, `referenced project must exist in inventory: ${reference}`)
    if (provider.subsystem === recoveryRuntime.subsystem) continue
    assert.ok(
      !durableCompositionOnly.has(provider.shard),
      `${provider.shard} must not be consumed by the delegation recovery runtime (DELEG-029)`,
    )
    assert.ok(
      provider.legacyKind === 'contract' || pureProjectionSpine.has(provider.shard),
      `cross-subsystem reference ${provider.shard} (${provider.legacyKind}) is not a contract or the pure projection spine`,
    )
  }

  // 3. The capability type is delegation-owned: the file declaring type AgentJournalPort is compiled by a project whose subsystem === 'delegation' and legacyKind === 'contract'
  let declaringProject = null
  for (const project of subsystemInventory.projects.values()) {
    const allFiles = [...project.implementationFiles, ...project.signatureFiles]
    for (const file of allFiles) {
      const text = readFileSync(file, 'utf8')
      if (/\btype\s+AgentJournalPort\b/.test(text)) {
        declaringProject = project
        break
      }
    }
    if (declaringProject) break
  }
  assert.ok(declaringProject, "a source file must declare 'type AgentJournalPort'")
  assert.equal(declaringProject.subsystem, 'delegation', 'AgentJournalPort declaring project must belong to delegation subsystem')
  assert.equal(declaringProject.legacyKind, 'contract', "AgentJournalPort declaring project must have legacyKind 'contract'")

  // 4. The shard's implementation sources consume the port and never call foreign module functions
  let consumesPort = false
  for (const file of recoveryRuntime.implementationFiles) {
    const text = readFileSync(file, 'utf8')
    if (/AgentJournalPort/.test(text)) consumesPort = true
    assert.doesNotMatch(
      text,
      /AgentJournal\.(?:appendAgent|handleProjection)/,
      `${file} must never call foreign AgentJournal module functions`,
    )
  }
  assert.ok(consumesPort, 'delegation-recovery-runtime implementation must consume AgentJournalPort')
})

test('WHAT[DELEG-030] delegation invariant fatal preserves settlement and one injected fuse', () => {
  assertFatalBoundary('delegation')
})
