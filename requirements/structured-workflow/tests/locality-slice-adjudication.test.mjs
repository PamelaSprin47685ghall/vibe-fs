import assert from 'node:assert/strict'
import test from 'node:test'

import {
  GLOBAL_ADJUDICATION_PROOF_V1,
  buildExpectedAdjudicationSnapshotV1,
  expectedRecordProofsV1,
  validateFormalAdjudicationV1,
} from '../../../scripts/lib/locality-slice-adjudication-v1.mjs'
import { extractObservedCapabilityFactsV1 } from '../../../scripts/lib/capability-observations-v1.mjs'

const digest = (digit) => `sha256:${digit.repeat(64)}`

const world = () => ({
  schema_version: 1,
  fact_schema_version: 1,
  observed: {
    localities: [
      {
        id: 'provider', owner: 'provider-owner', kind: 'contract', project_path: 'src/Provider.fsproj',
        sources: [{
          implementation_path: 'src/Provider.fs', implementation_digest: digest('1'),
          signature_path: 'src/Provider.fsi', signature_digest: digest('2'),
        }],
      },
      {
        id: 'z-consumer', owner: 'consumer-owner', kind: 'composition', project_path: 'src/Consumer.fsproj',
        sources: [{
          implementation_path: 'src/Consumer.fs', implementation_digest: digest('6'),
          signature_path: 'src/Consumer.fsi', signature_digest: digest('7'),
        }],
      },
    ],
    project_references: [{ consumer_locality: 'z-consumer', provider_locality: 'provider' }],
    actual_source_edges: [],
    generated_artifacts: [],
    javascript_traversals: [],
    capability_extraction: extractObservedCapabilityFactsV1([]).coverage,
    capability_facts: [],
  },
  normative: {
    authorization_schema_version: 2,
    slices: [{
      id: 'provider-slice', owner: 'provider-owner', provider_locality: 'provider',
      classification: { kind: 'contract', exposure: 'shared' }, allowed_direct_consumers: ['z-consumer'],
      laws: ['PROVIDER-001'],
      semantic_evidence: [{
        path: 'requirements/provider/tests/provider.test.mjs', title: 'WHAT[PROVIDER-001] provider law',
        what_id: 'PROVIDER-001', surface_module: 'ProviderSurface.js',
      }],
    }],
    capability_relations: [],
    generated_module_relations: [],
  },
})

const fixture = () => {
  const canonicalWorld = world()
  const snapshot = buildExpectedAdjudicationSnapshotV1({
    world: canonicalWorld,
    cutover_input_index_digest: digest('5'),
    reason_for: () => 'Owner adjudicated the complete signed surface.',
    migration_path_for: () => 'Keep the provider as one shared contract slice.',
  })
  return {
    snapshot,
    world: canonicalWorld,
    cutover_input_index_digest: digest('5'),
    valid_proof_ids: expectedRecordProofsV1(canonicalWorld, 'provider').map(({ what_id: whatId, path, title }) => `${whatId}\0${path}\0${title}`),
  }
}

const code = (value) => validateFormalAdjudicationV1(value)[0]

test('WHAT[STRUCTURED-WORKFLOW-011] adjudication validator binds a decision to terminal manifest claims', () => {
  const legal = fixture()
  assert.deepEqual(validateFormalAdjudicationV1(legal), [])
  assert.ok(legal.snapshot.records[0].decision.what_ids.includes('PROVIDER-001'))
  assert.ok(legal.snapshot.records[0].decision.what_ids.includes('STRUCTURED-WORKFLOW-011'))
  assert.ok(legal.snapshot.records[0].decision.proofs.some((proof) => proof.title === GLOBAL_ADJUDICATION_PROOF_V1.title))
})

test('WHAT[STRUCTURED-WORKFLOW-016] formal adjudication rejects every stale world claim and proof with an exact code', () => {
  const cases = [
    ['adjudication-record-missing', (value) => { value.snapshot.records = [] }, { locality_id: 'provider' }],
    ['adjudication-record-unexpected', (value) => { value.snapshot.records.push({ ...structuredClone(value.snapshot.records[0]), locality_id: 'unexpected' }) }, { locality_id: 'unexpected' }],
    ['adjudication-record-duplicate', (value) => { value.snapshot.records.push(structuredClone(value.snapshot.records[0])) }, { locality_id: 'provider' }],
    ['adjudication-record-locality-mismatch', (value) => { value.snapshot.records[0].locality_id = 'other' }, { locality_id: 'other' }],
    ['adjudication-record-locality-mismatch', (value) => { value.snapshot.records[0].decision.reason = '\n' }, { locality_id: 'provider' }],
    ['adjudication-fact-schema-mismatch', (value) => { value.snapshot.fact_schema_version = 2 }, {}],
    ['adjudication-world-digest-mismatch', (value) => { value.snapshot.canonical_world_digest = digest('a') }, {}],
    ['adjudication-index-digest-mismatch', (value) => { value.snapshot.cutover_input_index_digest = digest('b') }, {}],
    ['adjudication-query-digest-mismatch', (value) => { value.snapshot.records[0].queries.audience.query_digest = digest('c') }, { locality_id: 'provider', query_kind: 'audience' }],
    ['adjudication-target-mismatch', (value) => { value.snapshot.records[0].decision.target_classification = { case: 'private', payload: {} } }, { locality_id: 'provider' }],
    ['adjudication-manifest-claim-missing', (value) => { value.snapshot.records[0].decision.manifest_claim_ids.provider_slice_id = null }, { locality_id: 'provider', claim_id: 'provider-slice' }],
    ['adjudication-manifest-claim-stale', (value) => { value.snapshot.records[0].decision.manifest_claim_ids.consumer_capability_relation_ids.push('stale') }, { locality_id: 'provider', claim_id: 'stale' }],
    ['adjudication-proof-missing', (value) => { value.snapshot.records[0].decision.proofs = value.snapshot.records[0].decision.proofs.filter(({ what_id: whatId }) => whatId !== 'PROVIDER-001') }, { locality_id: 'provider', what_id: 'PROVIDER-001' }],
    ['adjudication-proof-orphan', (value) => { value.snapshot.records[0].decision.proofs.push({ what_id: 'ORPHAN-001', path: 'requirements/orphan/tests/orphan.test.mjs', title: 'WHAT[ORPHAN-001] orphan' }) }, { locality_id: 'provider', what_id: 'ORPHAN-001' }],
    ['adjudication-proof-invalid', (value) => { value.valid_proof_ids = value.valid_proof_ids.filter((proofId) => !proofId.startsWith('PROVIDER-001\0')) }, { locality_id: 'provider', what_id: 'PROVIDER-001' }],
    ['adjudication-proof-invalid', (value) => { value.snapshot.records[0].decision.proofs.push(structuredClone(value.snapshot.records[0].decision.proofs[0])) }, { locality_id: 'provider', what_id: '<ordering-or-duplicate>' }],
  ]

  for (const [expectedCode, mutate, coordinates] of cases) {
    const value = fixture()
    mutate(value)
    assert.deepEqual(code(value), { code: expectedCode, ...coordinates })
  }
})
