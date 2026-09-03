import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import {
  buildCanonicalWorldV1,
  canonicalWorldDigestV1,
  deriveAdjudicationCandidates,
  serializeCanonicalWorldV1,
} from '../../../scripts/lib/locality-slice-world-v1.mjs'
import { extractObservedCapabilityFactsV1 } from '../../../scripts/lib/capability-observations-v1.mjs'

const SEED = 0x43414e4f
const digest = (value) => `sha256:${value.toString(16).padStart(64, '0')}`

const worldFor = (ids) => {
  const rank = new Map([...ids].sort().map((id, index) => [id, index]))
  return ({
  schema_version: 1,
  fact_schema_version: 1,
  observed: {
    localities: ids.map((id) => ({
      id,
      owner: `owner-${id}`,
      kind: 'contract',
      project_path: `src/${id}.fsproj`,
      sources: [{
        implementation_path: `src/${id}.fs`,
        implementation_digest: digest(rank.get(id) + 1),
        signature_path: `src/${id}.fsi`,
        signature_digest: digest(rank.get(id) + 101),
      }],
    })),
    project_references: [],
    actual_source_edges: [],
    generated_artifacts: [],
    javascript_traversals: [],
    capability_extraction: extractObservedCapabilityFactsV1([]).coverage,
    capability_facts: [],
  },
  normative: {
    authorization_schema_version: 2,
    slices: [],
    capability_relations: [],
    generated_module_relations: [],
  },
  })
}

test('WHAT[STRUCTURED-WORKFLOW-013] canonical world is permutation invariant and every live locality remains a candidate', () => {
  fc.assert(fc.property(
    fc.uniqueArray(fc.stringMatching(/^[a-z][a-z0-9-]{0,10}$/), { minLength: 1, maxLength: 12 }),
    fc.integer(),
    (ids, shuffleSeed) => {
      const canonical = buildCanonicalWorldV1(worldFor(ids))
      const permutedIds = fc.sample(fc.shuffledSubarray(ids, { minLength: ids.length, maxLength: ids.length }), { seed: shuffleSeed, numRuns: 1 })[0]
      const permuted = buildCanonicalWorldV1(worldFor(permutedIds))
      assert.equal(serializeCanonicalWorldV1(permuted), serializeCanonicalWorldV1(canonical))
      assert.equal(canonicalWorldDigestV1(permuted), canonicalWorldDigestV1(canonical))
      assert.deepEqual(
        deriveAdjudicationCandidates(permuted).map(({ locality_id: localityId }) => localityId),
        [...ids].sort(),
      )
    },
  ), { seed: SEED, numRuns: 100 })
})
