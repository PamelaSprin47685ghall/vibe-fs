import {
  buildCanonicalWorldV1,
  canonicalWorldDigestV1,
  classifyTerminalV1,
  deriveAdjudicationCandidates,
  projectManifestClaimsV1,
  queryCanonicalLocalityV1,
  queryDigestV1,
} from './locality-slice-world-v1.mjs'
import { compareCanonicalTextV1, encodeCanonicalJsonV1 } from './canonical-json-v1.mjs'

export const GLOBAL_ADJUDICATION_PROOF_V1 = Object.freeze({
  what_id: 'STRUCTURED-WORKFLOW-011',
  path: 'requirements/structured-workflow/tests/locality-slice-adjudication.test.mjs',
  title: 'WHAT[STRUCTURED-WORKFLOW-011] adjudication validator binds a decision to terminal manifest claims',
})

const proofIdentity = ({ what_id: whatId, path, title }) => `${whatId}\0${path}\0${title}`
const uniqueTexts = (values) => [...new Set(values)].sort(compareCanonicalTextV1)
const uniqueProofs = (values) => [...new Map(values.map((proof) => [proofIdentity(proof), proof])).values()]
  .sort((left, right) => compareCanonicalTextV1(proofIdentity(left), proofIdentity(right)))

const claimedRows = (world, localityId) => {
  const claims = projectManifestClaimsV1(world, localityId)
  const slices = claims.provider_slice_id === null ? [] : world.normative.slices.filter(({ id }) => id === claims.provider_slice_id)
  const capabilityRelations = world.normative.capability_relations.filter(({ id }) => claims.consumer_capability_relation_ids.includes(id))
  const generatedRelations = world.normative.generated_module_relations.filter(({ id }) => claims.consumer_generated_module_relation_ids.includes(id))
  return { claims, slices, capabilityRelations, generatedRelations }
}

export const expectedRecordProofsV1 = (worldInput, localityId) => {
  const world = buildCanonicalWorldV1(worldInput)
  const { slices, capabilityRelations, generatedRelations } = claimedRows(world, localityId)
  return uniqueProofs([
    ...slices.flatMap(({ semantic_evidence: evidence }) => evidence.map(({ what_id: whatId, path, title }) => ({ what_id: whatId, path, title }))),
    ...capabilityRelations.flatMap(({ semantic_evidence: evidence }) => evidence.map(({ what_id: whatId, path, title }) => ({ what_id: whatId, path, title }))),
    ...generatedRelations.map(({ determinism_proof: proof }) => ({ ...proof })),
    GLOBAL_ADJUDICATION_PROOF_V1,
  ])
}

const expectedWhatIds = (world, localityId) => {
  const { slices, capabilityRelations, generatedRelations } = claimedRows(world, localityId)
  return uniqueTexts([
    'STRUCTURED-WORKFLOW-011',
    ...slices.flatMap(({ laws }) => laws),
    ...capabilityRelations.flatMap(({ laws }) => laws),
    ...generatedRelations.flatMap(({ laws }) => laws),
  ])
}

const queriesFor = (world, localityId) => {
  const results = queryCanonicalLocalityV1(world, localityId)
  return Object.fromEntries(['surface', 'audience', 'capability'].map((kind) => {
    const queryId = `${kind}/v1:${localityId}`
    return [kind, { query_id: queryId, query_digest: queryDigestV1(queryId, results[kind]) }]
  }))
}

export const buildExpectedAdjudicationSnapshotV1 = ({
  world: worldInput,
  cutover_input_index_digest: cutoverInputIndexDigest,
  reason_for: reasonFor,
  migration_path_for: migrationPathFor,
}) => {
  const world = buildCanonicalWorldV1(worldInput)
  return {
    schema_version: 1,
    snapshot_kind: 'm6.4-cutover',
    fact_schema_version: world.fact_schema_version,
    canonical_world_digest: canonicalWorldDigestV1(world),
    cutover_input_index_digest: cutoverInputIndexDigest,
    records: deriveAdjudicationCandidates(world).map(({ locality_id: localityId }) => ({
      locality_id: localityId,
      queries: queriesFor(world, localityId),
      decision: {
        reason: reasonFor(localityId),
        target_classification: classifyTerminalV1(world, localityId),
        manifest_claim_ids: projectManifestClaimsV1(world, localityId),
        migration_path: migrationPathFor(localityId),
        what_ids: expectedWhatIds(world, localityId),
        proofs: expectedRecordProofsV1(world, localityId),
      },
    })),
  }
}

const exactKeys = (value, keys) => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return false
  const actual = Object.keys(value).sort(compareCanonicalTextV1)
  const expected = [...keys].sort(compareCanonicalTextV1)
  return actual.length === expected.length && actual.every((key, index) => key === expected[index])
}

const reviewTextIsValid = (value) => typeof value === 'string'
  && value.trim().length > 0
  && !/[\u0000-\u001f\u007f]/.test(value)

const terminalClassificationIsValid = (value) => exactKeys(value, ['case', 'payload'])
  && ['private', 'contract-shared', 'contract-bounded', 'runtime-effect', 'adapter-effect', 'composition-terminal'].includes(value.case)
  && exactKeys(value.payload, [])

const textArrayIsCanonical = (value) => Array.isArray(value)
  && value.every((item) => typeof item === 'string' && item.length > 0)
  && value.every((item, index) => index === 0 || compareCanonicalTextV1(value[index - 1], item) < 0)

const proofIsValid = (proof) => exactKeys(proof, ['what_id', 'path', 'title'])
  && [proof.what_id, proof.path, proof.title].every((item) => typeof item === 'string' && item.length > 0)

const firstDifference = (expected, actual) => {
  const expectedSet = new Set(expected)
  const actualSet = new Set(actual)
  return {
    missing: expected.find((value) => !actualSet.has(value)) ?? null,
    stale: actual.find((value) => !expectedSet.has(value)) ?? null,
  }
}

const finding = (code, coordinates = {}) => [{ code, ...coordinates }]

const claimsFlattened = (claims) => [
  ...(claims.provider_slice_id === null ? [] : [claims.provider_slice_id]),
  ...claims.consumer_capability_relation_ids,
  ...claims.consumer_generated_module_relation_ids,
]

export const validateFormalAdjudicationV1 = ({
  snapshot,
  world: worldInput,
  cutover_input_index_digest: cutoverInputIndexDigest,
  valid_proof_ids: validProofIds,
}) => {
  const world = buildCanonicalWorldV1(worldInput)
  if (!exactKeys(snapshot, ['schema_version', 'snapshot_kind', 'fact_schema_version', 'canonical_world_digest', 'cutover_input_index_digest', 'records'])
    || snapshot.schema_version !== 1 || snapshot.snapshot_kind !== 'm6.4-cutover' || snapshot.fact_schema_version !== world.fact_schema_version) {
    return finding('adjudication-fact-schema-mismatch')
  }
  if (!Array.isArray(snapshot.records)) return finding('adjudication-record-missing', { locality_id: world.observed.localities[0]?.id })
  const expectedIds = world.observed.localities.map(({ id }) => id)
  const malformedRecord = snapshot.records.find((record) => record === null || typeof record !== 'object' || typeof record.locality_id !== 'string')
  if (malformedRecord !== undefined) return finding('adjudication-record-locality-mismatch', { locality_id: '<invalid-shape>' })
  const actualIds = snapshot.records.map(({ locality_id: localityId }) => localityId)
  const duplicate = actualIds.find((value, index) => actualIds.indexOf(value) !== index)
  if (duplicate !== undefined) return finding('adjudication-record-duplicate', { locality_id: duplicate })
  if (actualIds.length < expectedIds.length) {
    const missing = expectedIds.find((id) => !actualIds.includes(id))
    return finding('adjudication-record-missing', { locality_id: missing })
  }
  if (actualIds.length > expectedIds.length) {
    const unexpected = actualIds.find((id) => !expectedIds.includes(id))
    return finding('adjudication-record-unexpected', { locality_id: unexpected })
  }
  if (actualIds.some((id, index) => id !== expectedIds[index])) {
    const mismatch = actualIds.find((id, index) => id !== expectedIds[index])
    return finding('adjudication-record-locality-mismatch', { locality_id: mismatch })
  }
  if (snapshot.canonical_world_digest !== canonicalWorldDigestV1(world)) return finding('adjudication-world-digest-mismatch')
  if (snapshot.cutover_input_index_digest !== cutoverInputIndexDigest) return finding('adjudication-index-digest-mismatch')

  const validProofSet = new Set(validProofIds ?? [])
  for (const record of snapshot.records) {
    const localityId = record.locality_id
    if (!exactKeys(record, ['locality_id', 'queries', 'decision'])
      || !exactKeys(record.queries, ['surface', 'audience', 'capability'])
      || !exactKeys(record.decision, ['reason', 'target_classification', 'manifest_claim_ids', 'migration_path', 'what_ids', 'proofs'])
      || !exactKeys(record.decision.manifest_claim_ids, ['provider_slice_id', 'consumer_capability_relation_ids', 'consumer_generated_module_relation_ids'])
      || !reviewTextIsValid(record.decision.reason)
      || !reviewTextIsValid(record.decision.migration_path)) {
      return finding('adjudication-record-locality-mismatch', { locality_id: localityId })
    }
    const expectedQueries = queriesFor(world, localityId)
    for (const kind of ['surface', 'audience', 'capability']) {
      if (!exactKeys(record.queries[kind], ['query_id', 'query_digest'])
        || encodeCanonicalJsonV1(record.queries[kind]) !== encodeCanonicalJsonV1(expectedQueries[kind])) {
        return finding('adjudication-query-digest-mismatch', { locality_id: localityId, query_kind: kind })
      }
    }
    if (!terminalClassificationIsValid(record.decision.target_classification)
      || encodeCanonicalJsonV1(record.decision.target_classification) !== encodeCanonicalJsonV1(classifyTerminalV1(world, localityId))) {
      return finding('adjudication-target-mismatch', { locality_id: localityId })
    }
    const expectedClaims = projectManifestClaimsV1(world, localityId)
    const expectedClaimIds = claimsFlattened(expectedClaims)
    if (!(record.decision.manifest_claim_ids.provider_slice_id === null
      || typeof record.decision.manifest_claim_ids.provider_slice_id === 'string')
      || !textArrayIsCanonical(record.decision.manifest_claim_ids.consumer_capability_relation_ids)
      || !textArrayIsCanonical(record.decision.manifest_claim_ids.consumer_generated_module_relation_ids)) {
      return finding('adjudication-manifest-claim-stale', { locality_id: localityId, claim_id: '<invalid-shape>' })
    }
    const actualClaimIds = claimsFlattened(record.decision.manifest_claim_ids)
    const claimDifference = firstDifference(expectedClaimIds, actualClaimIds)
    if (claimDifference.missing !== null) return finding('adjudication-manifest-claim-missing', { locality_id: localityId, claim_id: claimDifference.missing })
    if (claimDifference.stale !== null) return finding('adjudication-manifest-claim-stale', { locality_id: localityId, claim_id: claimDifference.stale })
    if (encodeCanonicalJsonV1(record.decision.manifest_claim_ids) !== encodeCanonicalJsonV1(expectedClaims)) {
      return finding('adjudication-manifest-claim-stale', { locality_id: localityId, claim_id: actualClaimIds[0] ?? '<ordering>' })
    }
    const expectedIdsForRecord = expectedWhatIds(world, localityId)
    if (!textArrayIsCanonical(record.decision.what_ids)) {
      return finding('adjudication-proof-invalid', { locality_id: localityId, what_id: '<invalid-shape>' })
    }
    if (encodeCanonicalJsonV1(record.decision.what_ids) !== encodeCanonicalJsonV1(expectedIdsForRecord)) {
      return finding('adjudication-proof-invalid', { locality_id: localityId, what_id: firstDifference(expectedIdsForRecord, record.decision.what_ids).missing ?? record.decision.what_ids[0] })
    }
    if (!Array.isArray(record.decision.proofs) || !record.decision.proofs.every(proofIsValid)) {
      return finding('adjudication-proof-invalid', { locality_id: localityId, what_id: '<invalid-shape>' })
    }
    const expectedProofs = expectedRecordProofsV1(world, localityId)
    const expectedProofIds = expectedProofs.map(proofIdentity)
    const actualProofIds = record.decision.proofs.map(proofIdentity)
    const proofDifference = firstDifference(expectedProofIds, actualProofIds)
    if (proofDifference.missing !== null) return finding('adjudication-proof-missing', { locality_id: localityId, what_id: proofDifference.missing.split('\0')[0] })
    if (proofDifference.stale !== null) return finding('adjudication-proof-orphan', { locality_id: localityId, what_id: proofDifference.stale.split('\0')[0] })
    if (encodeCanonicalJsonV1(record.decision.proofs) !== encodeCanonicalJsonV1(expectedProofs)) {
      return finding('adjudication-proof-invalid', { locality_id: localityId, what_id: '<ordering-or-duplicate>' })
    }
    for (const proof of record.decision.proofs) {
      if (!validProofSet.has(proofIdentity(proof))) return finding('adjudication-proof-invalid', { locality_id: localityId, what_id: proof.what_id })
    }
  }
  return []
}
