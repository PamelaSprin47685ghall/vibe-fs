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

test('WHAT[delegation-030] delegation invariant fatal preserves settlement and one injected fuse', () => {
  assertFatalBoundary('delegation')
})
