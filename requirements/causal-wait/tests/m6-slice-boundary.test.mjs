import assert from 'node:assert/strict'
import test from 'node:test'
import { readOwnerProjectInventoryV1 } from '../../../scripts/checks/owner-projects.mjs'
import { assertEffectIsInjected, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const locality = (inventory, id) => {
  const matches = inventory.localities.filter((candidate) => candidate.id === id)
  assert.equal(matches.length, 1, `${id} must resolve to one production locality`)
  return matches[0]
}

const sourcePaths = (entry) => entry.sources.map(({ implementationPath }) => implementationPath)

test('WHAT[CAUSAL-009] production inventory separates contract runtime adapter mailbox and proof surface', () => {
  const inventory = readOwnerProjectInventoryV1()
  const contract = locality(inventory, 'execution-session-wait-contract')
  const runtime = locality(inventory, 'execution-session-wait-runtime')
  const adapter = locality(inventory, 'execution-session-wait-diagnostic-adapter')
  const mailbox = locality(inventory, 'execution-session-wait-completion-mailbox')
  const proof = locality(inventory, 'execution-session-wait-proof-surface')

  assert.equal(contract.kind, 'contract')
  assert.equal(runtime.kind, 'runtime')
  assert.equal(adapter.kind, 'adapter')
  assert.equal(mailbox.kind, 'runtime')
  assert.equal(proof.kind, 'composition')
  assert.deepEqual(sourcePaths(contract), ['src/Wanxiangshu/Execution/Session/Wait/CausalWait.fs'])
  assert.deepEqual(sourcePaths(runtime), [
    'src/Wanxiangshu/Execution/Session/Wait/Await.fs',
    'src/Wanxiangshu/Execution/Session/Wait/Registry.fs',
  ])
  assert.deepEqual(sourcePaths(adapter), ['src/Wanxiangshu/Execution/Session/Wait/Bridge.fs'])
  assert.deepEqual(sourcePaths(mailbox), ['src/Wanxiangshu/Execution/Session/Wait/CompletionMailbox.fs'])
  assert.deepEqual(sourcePaths(proof), ['src/Wanxiangshu/Execution/Session/Wait/Surface.fs'])
  assert.deepEqual(contract.references, [])
  assert.deepEqual(adapter.references, ['execution-session-wait-contract'])
  assert.deepEqual(runtime.references, [
    'execution-session-wait-contract',
    'foundation-temporal-contract',
  ])
  for (const consumer of inventory.localities.filter(({ references }) => references.includes(mailbox.id))) {
    assert.equal(consumer.kind, 'composition', `${consumer.id} must inject the physical mailbox from composition`)
  }
  for (const id of [
    'delegation-runtime-surface',
    'git-integrationgate',
    'opencode-host-pluginruntimescope',
  ]) {
    const composition = locality(inventory, id)
    assert.equal(composition.kind, 'composition')
    assert.ok(composition.references.includes(mailbox.id), `${id} must declare its physical mailbox provider`)
  }
  assert.equal(
    locality(inventory, 'delegation-host-adapter').references.includes(mailbox.id),
    false,
    'the Host adapter must receive a mailbox factory instead of constructing a foreign runtime',
  )
  assert.equal(
    locality(inventory, 'delegation-fork-runtime').references.includes(mailbox.id),
    false,
    'the Fork runtime must consume only the injected mailbox capability',
  )
  assert.equal(inventory.localities.some(({ id }) => id === 'execution-session-wait-causalwait'), false)
  assert.deepEqual(
    inventory.localities.filter(({ references }) => references.includes(proof.id)),
    [],
    'proof surface must not provide production capability',
  )
})

test('WHAT[CAUSAL-009] causal wait contract excludes registry diagnostics mailbox and proof runtime', () => {
  assertPureContract('capability-type-only')
  assertEffectIsInjected('console')
})
