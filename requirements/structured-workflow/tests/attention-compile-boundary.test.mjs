import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'

const closure = (inventory, root) => {
  const visited = new Set()
  const visit = (path) => {
    if (visited.has(path)) return
    visited.add(path)
    for (const reference of inventory.projects.get(path).references) visit(reference)
  }
  visit(root.projectPath)
  return [...visited].flatMap((path) => inventory.projects.get(path).implementationFiles)
}

test('WHAT[STRUCTURED-WORKFLOW-013] attention tools consume their own port without durable aggregate or runtime containers', () => {
  const inventory = buildSubsystemInventory()
  assert.ok(inventory.ok, inventory.violations.join('\n'))
  const tools = [...inventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('/OpenCode/Tools/AttentionTools.fs')))
  assert.ok(tools, 'the real AttentionTools consumer must have a compile shard')
  assert.equal(tools.subsystem, 'interaction')
  for (const file of closure(inventory, tools)) {
    assert.doesNotMatch(file, /\/(?:Composition\/Durable|Persistence\/Journal)\//,
      `attention tool closure must not acquire aggregate persistence: ${file}`)
    assert.doesNotMatch(file, /\/(?:PluginRuntimeScope|ToolRuntimeScope)\.fs$/,
      `attention tool closure must not acquire an application runtime container: ${file}`)
  }
  for (const file of [...tools.implementationFiles, ...tools.signatureFiles]) {
    assert.doesNotMatch(readFileSync(file, 'utf8'), /\b(?:AgentJournal|AgentFact|ProjectionSet|AgentProjectionSet)\b/,
      `attention tool boundary must not expose a foreign aggregate: ${file}`)
  }
  const port = [...inventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('/Interaction/Attention/JournalPort.fs')))
  assert.equal(port?.subsystem, 'interaction', 'the port vocabulary belongs to the consumer domain')
  const adapter = [...inventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('/Composition/Durable/AgentJournalPortAdapter.fs')))
  assert.equal(adapter?.subsystem, 'durable-composition', 'outer routing belongs to durable composition')
  const registry = [...inventory.projects.values()].find((project) =>
    project.implementationFiles.some((file) => file.endsWith('/OpenCode/Tools/ToolRegistry.fs')))
  assert.ok(registry.references.includes(adapter.projectPath), 'the real registry must wire the durable adapter')
  const registryFile = registry.implementationFiles.find((file) => file.endsWith('/OpenCode/Tools/ToolRegistry.fs'))
  assert.doesNotMatch(readFileSync(registryFile, 'utf8'), /AgentFact\.Attention\b/,
    'the registry must not take ownership of durable routing')
})
