import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { default: path } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { readCompileShardInventory } = await import("../../../scripts/lib/compile-shards.mjs");
const { buildSubsystemInventory } = await import("../../../scripts/checks/subsystems.mjs");
const HostSignalSurface = await import("../../../dist/OpenCode/Host/HostSignalSurface.js");
const { assertEffectIsInjected, assertFatalBoundary, assertOptionalObservationNoninterference, assertPureContract } = await import("../../structured-workflow/tests/support/m6-boundary-proof.mjs");

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const requireShard = (projects, shardId) => {
  const matches = [...projects.values()].filter((candidate) => candidate.shard === shardId)
  assert.equal(matches.length, 1, `${shardId} must resolve to exactly one production compile shard`)
  return matches[0]
}
const relSources = (project) => project.implementationFiles.map((p) => path.relative(ROOT, p)).sort()
const refShards = (project, projects) => project.references.map((refPath) => projects.get(refPath).shard).sort()
const closureSources = (root, projects) => {
  const closure = new Set()
  const pending = [root]
  while (pending.length > 0) {
    const project = pending.pop()
    if (closure.has(project)) continue
    closure.add(project)
    for (const refPath of project.references) {
      pending.push(projects.get(refPath))
    }
  }
  return new Set([...closure].flatMap(relSources))
}

test('WHAT[HOST-BOUNDARY-031] RootWorkspace runtime is private and every observer consumes only the typed contract', () => {
  const shardInventory = readCompileShardInventory({ repositoryRoot: ROOT })
  const subsystemInventory = buildSubsystemInventory({ compileInventory: shardInventory })
  assert.ok(subsystemInventory.ok, subsystemInventory.violations.join('\n'))
  const projects = subsystemInventory.projects

  const contract = requireShard(projects, 'host-root-workspace-contract')
  const runtime = requireShard(projects, 'host-root-workspace-runtime')

  assert.equal(contract.subsystem, 'host')
  assert.equal(runtime.subsystem, 'host')
  assert.deepEqual(relSources(contract), ['src/Wanxiangshu/OpenCode/Host/RootWorkspace.fs'])
  assert.deepEqual(refShards(contract, projects), [])
  assert.deepEqual(relSources(runtime), ['src/Wanxiangshu/OpenCode/Host/RootWorkspaceRuntime.fs'])
  assert.deepEqual(refShards(runtime, projects), ['host-root-workspace-contract'])

  const runtimeConsumers = [...projects.values()]
    .filter((candidate) => refShards(candidate, projects).includes('host-root-workspace-runtime'))
    .map((candidate) => candidate.shard)
    .sort()
  assert.deepEqual(runtimeConsumers, ['opencode-host-sharedstatesurface', 'plugin-composition'])

  assert.ok(refShards(requireShard(projects, 'opencode-host-hostsignalbootstrap'), projects).includes('host-root-workspace-contract'))
  assert.ok(!refShards(requireShard(projects, 'opencode-host-hostsignalbootstrap'), projects).includes('host-root-workspace-runtime'), 'opencode-host-hostsignalbootstrap must consume only typed contract, not process-local runtime')

  for (const id of [
    'execution-delegation-hostturnobservedsurface',
    'git-integrationgate',
    'interaction-repair-interactionrepair',
    'opencode-host-pluginruntimescope',
    'participant-provider-attempt-fallback-ledger',
  ])
    assert.ok(!refShards(requireShard(projects, id), projects).includes('host-root-workspace-runtime'), `${id} must not acquire the process-local runtime`)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { putSessionParent, getSessionParent, clearSessionParents, tryBindRootWorkspace, tryGetRootWorkspace, firstBoundRootWorkspace, selectContinuationDirectory } = await import("../../../dist/OpenCode/Host/SharedStateSurface.js");
const sharedStateSurface = await import("../../../dist/OpenCode/Host/SharedStateSurface.js");


test('WHAT[HOST-BOUNDARY-031] SHARED_root_workspace_is_first_bound_behind_typed_capabilities', async () => {
  assert.equal(tryGetRootWorkspace(), null)
  assert.equal(tryBindRootWorkspace(null), false, 'None must not occupy the first-bind slot')
  assert.equal(tryBindRootWorkspace(''), false, 'blank must not occupy the first-bind slot')
  assert.equal(tryBindRootWorkspace('  '), false, 'whitespace must not occupy the first-bind slot')
  assert.equal(tryGetRootWorkspace(), null)

  const attempts = await Promise.all(
    ['/tmp/first-root-workspace', '/tmp/concurrent-root-workspace']
      .map(candidate => Promise.resolve().then(() => ({ candidate, bound: tryBindRootWorkspace(candidate) }))),
  )
  const winners = attempts.filter(attempt => attempt.bound)
  assert.equal(winners.length, 1, 'concurrent contenders must produce one winner')
  assert.equal(tryGetRootWorkspace(), winners[0].candidate)

  assert.equal(tryBindRootWorkspace('/tmp/second-root-workspace'), false)
  assert.equal(tryGetRootWorkspace(), winners[0].candidate, 'a later plugin cannot overwrite the root')

  assert.equal(firstBoundRootWorkspace([null, '', '  ', '/tmp/later']), '/tmp/later')
  assert.equal(selectContinuationDirectory('/tmp/live', true, '/tmp/root'), '/tmp/live')
  assert.equal(selectContinuationDirectory('/tmp/deleted', false, '/tmp/root'), '/tmp/root')
  assert.equal(selectContinuationDirectory('/tmp/deleted', false, null), null)

  assert.equal('setRootWorkspace' in sharedStateSurface, false)
  assert.equal('clearRootWorkspace' in sharedStateSurface, false)
})
}
