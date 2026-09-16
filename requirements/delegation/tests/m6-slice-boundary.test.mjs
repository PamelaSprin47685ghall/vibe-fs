import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { assertFatalBoundary } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')

const assertRecoveryClosure = (inventory, root) => {
  const visited = new Set()
  const visit = (path) => {
    if (visited.has(path)) return
    visited.add(path)
    const project = inventory.projects.get(path)
    assert.ok(project, `referenced project must exist: ${path}`)
    for (const file of project.implementationFiles) {
      assert.doesNotMatch(file, /\/(?:Persistence\/(?:Journal|EventStore)|Composition\/Durable|Process|OpenCode\/Tools)\//,
        `recovery must not acquire durable composition or physical implementations: ${file}`)
      assert.doesNotMatch(file, /\/(?:PluginRuntimeScope|ToolRuntimeScope|HostSignalBootstrap|SharedTerminalBus)\.fs$/,
        `recovery must not acquire a concrete host runtime: ${file}`)
    }
    for (const reference of project.references) visit(reference)
  }
  visit(root.projectPath)
}

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
  //    only its port. Check the actual transitive inputs, not retired locality labels.
  const durableCompositionOnly = new Set([
    'persistence-journal-agentjournal',
    'persistence-journal-promptfactcodec',
    'persistence-journal-eventstorewriter',
    'composition-durable-fact',
  ])
  assert.ok(recoveryRuntime.references.length > 0, 'delegation-recovery-runtime must have references')
  for (const reference of recoveryRuntime.references) {
    const provider = subsystemInventory.projects.get(reference)
    assert.ok(provider, `referenced project must exist in inventory: ${reference}`)
    if (provider.subsystem === recoveryRuntime.subsystem) continue
    assert.ok(
      !durableCompositionOnly.has(provider.shard),
      `${provider.shard} must not be consumed by the delegation recovery runtime (DELEG-029)`,
    )
  }
  assertRecoveryClosure(subsystemInventory, recoveryRuntime)

  const withoutLegacyKinds = {
    ...subsystemInventory,
    projects: new Map([...subsystemInventory.projects].map(([path, project]) =>
      [path, { ...project, legacyKind: '' }])),
  }
  assertRecoveryClosure(withoutLegacyKinds, recoveryRuntime)

  // Both a directly injected journal and a physical timing implementation must
  // be rejected even when falsely labelled as contracts.
  for (const shard of ['persistence-journal-agentjournal', 'process-node-timing-adapter']) {
    const foreign = [...subsystemInventory.projects.values()].find((project) => project.shard === shard)
    assert.ok(foreign, `real negative provider must exist: ${shard}`)
    const projects = new Map(withoutLegacyKinds.projects)
    projects.set(foreign.projectPath, { ...foreign, legacyKind: 'contract' })
    projects.set(recoveryRuntime.projectPath, {
      ...recoveryRuntime,
      references: [...recoveryRuntime.references, foreign.projectPath],
    })
    assert.throws(() => assertRecoveryClosure({ ...subsystemInventory, projects }, recoveryRuntime),
      /recovery must not acquire/)
  }

  // A same-subsystem intermediary must not hide that dependency either.
  const journal = [...subsystemInventory.projects.values()].find((project) => project.shard === 'persistence-journal-agentjournal')
  const portProject = [...subsystemInventory.projects.values()].find((project) => project.shard === 'delegation-journal-port')
  assert.ok(portProject)
  const indirect = new Map(withoutLegacyKinds.projects)
  indirect.set(portProject.projectPath, { ...portProject, references: [...portProject.references, journal.projectPath] })
  assert.throws(() => assertRecoveryClosure({ ...subsystemInventory, projects: indirect }, recoveryRuntime),
    /recovery must not acquire/)

  // Execution/Delegation/LinkageProjection.fs must be provided by a delegation contract shard
  const linkageProject = [...subsystemInventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('Execution/Delegation/LinkageProjection.fs')),
  )
  assert.ok(linkageProject, 'a project must compile Execution/Delegation/LinkageProjection.fs')
  assert.equal(linkageProject.subsystem, 'delegation', 'LinkageProjection project must belong to delegation subsystem')
  assertRecoveryClosure(subsystemInventory, linkageProject)

  // 3. The capability type is owned by delegation and does not pull the implementation back in.
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
  assertRecoveryClosure(subsystemInventory, declaringProject)

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
