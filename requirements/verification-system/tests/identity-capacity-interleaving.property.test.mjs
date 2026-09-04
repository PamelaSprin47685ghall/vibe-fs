import assert from 'node:assert/strict'
import test from 'node:test'

import { runInterleaving, validPermutations } from './support/identity-capacity-interleaving.mjs'

const parentAgents = ['manager', 'devops']
const childAgents = ['coder', 'inspector']
const capacities = [2, 3]
const replayModes = [false, true]
const duplicateDeliveryModes = [false, true]
const restartBoundaries = [null, 'child dispatched', 'parent new prompt']

const families = parentAgents.flatMap((parentAgent) =>
  childAgents.flatMap((childAgent) =>
    capacities.flatMap((capacity) =>
      replayModes.flatMap((replay) =>
        duplicateDeliveryModes.map((duplicateDelivery) => ({
          parentAgent,
          childAgent,
          capacity,
          replay,
          duplicateDelivery,
        })),
      ),
    ),
  ),
).map((family, index) => Object.freeze({
  ...family,
  name: `family-${index}`,
  restartAfter: restartBoundaries[index % restartBoundaries.length],
  schedule: validPermutations[index % validPermutations.length],
}))

test('WHAT[VERIFICATION-SYSTEM-007] deterministic families preserve replay, restart, identity, and fence laws', async () => {
  assert.equal(families.length, 32)
  assert.deepEqual(new Set(families.map(({ parentAgent }) => parentAgent)), new Set(parentAgents))
  assert.deepEqual(new Set(families.map(({ childAgent }) => childAgent)), new Set(childAgents))
  assert.deepEqual(new Set(families.map(({ capacity }) => capacity)), new Set(capacities))
  assert.deepEqual(new Set(families.map(({ replay }) => replay)), new Set(replayModes))
  assert.deepEqual(
    new Set(families.map(({ duplicateDelivery }) => duplicateDelivery)),
    new Set(duplicateDeliveryModes),
  )
  assert.deepEqual(new Set(families.map(({ restartAfter }) => restartAfter)), new Set(restartBoundaries))

  const results = []
  for (const { schedule, ...family } of families) {
    results.push(await runInterleaving(schedule, family))
  }

  assert.equal(results.length, families.length)
  assert.ok(results.every(({ providerDispatches }) => providerDispatches === 1))
  assert.ok(results.every(({ parent, child }) => parent.session !== child.session))
  assert.ok(
    results.every(
      ({ parent, child }) =>
        parent.participantIdentity.selectedAgent !== child.participantIdentity.selectedAgent,
    ),
  )
})
