import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { integrationTest } from '../../../verification-system/tests/support/tier-gate.mjs'

integrationTest('WHAT[requirement-grounding-007] compiled production owners preserve original material through the pinned Host actual provider serialization', () => {
  const env = { ...process.env }
  delete env.NODE_TEST_CONTEXT
  const child = spawnSync(process.execPath, [new URL('./run-grounding-provider-canary.mjs', import.meta.url).pathname], {
    encoding: 'utf8', env, timeout: 45000,
  })
  assert.equal(child.error, undefined, child.stderr)
  assert.equal(child.signal, null, child.stderr)
  assert.equal(child.status, 0, child.stderr)
  const line = child.stdout.split('\n').find(value => value.startsWith('grounding-provider-canary: '))
  assert.ok(line, child.stdout)
  const receipt = JSON.parse(line.slice('grounding-provider-canary: '.length))
  assert.equal(receipt.version, '1.18.29')
  assert.equal(receipt.providerRequests, 3)
  assert.equal(receipt.nativeReads, 2)
  for (const field of ['compiledPairAndGroundingOwners', 'independentWholePayloadOracle', 'afterHookMatchesSDK', 'nextProviderMatchesProjection', 'originalMaterialBytesPreserved', 'frozenPriorResultPreserved']) {
    assert.equal(receipt[field], true, field)
  }
  assert.equal(receipt.nativeByteCoverage, 'PartialFile')
  assert.equal(receipt.scope, 'thin-owner-hooks-and-real-host-serialization')
})
