import {
  assertRepositoryPathV1,
  canonicalDigestV1,
  compareCanonicalTextV1,
  encodeCanonicalJsonV1,
} from './canonical-json-v1.mjs'
import {
  capabilityDispositionViolatesContractV1,
  javascriptSourceIdV1,
  javascriptTraversalIdV1,
  validateCanonicalCapabilityFactV1,
  validateCapabilityPartitionV1,
} from './capability-observations-v1.mjs'
import { generatedArtifactIdV1 } from './generated-artifact-v1.mjs'

export class CanonicalWorldV1Error extends TypeError {
  constructor(code, path, message) {
    super(`${path}: ${message}`)
    this.name = 'CanonicalWorldV1Error'
    this.code = code
    this.path = path
  }
}

const fail = (code, path, message) => {
  throw new CanonicalWorldV1Error(code, path, message)
}

const plainObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value)

const exactObject = (value, keys, path) => {
  if (!plainObject(value)) fail('canonical-world-schema', path, 'expected object')
  const actual = Object.keys(value).sort(compareCanonicalTextV1)
  const expected = [...keys].sort(compareCanonicalTextV1)
  if (actual.length !== expected.length || actual.some((key, index) => key !== expected[index])) {
    fail('canonical-world-schema', path, `expected exact keys ${expected.join(',')}`)
  }
  return value
}

const array = (value, path) => {
  if (!Array.isArray(value)) fail('canonical-world-schema', path, 'expected array')
  return value
}

const text = (value, path) => {
  if (typeof value !== 'string' || value.length === 0) fail('canonical-world-schema', path, 'expected non-empty string')
  return value
}

const integer = (value, path) => {
  if (!Number.isSafeInteger(value) || value < 0) fail('canonical-world-schema', path, 'expected non-negative safe integer')
  return value
}

const digest = (value, path) => {
  if (typeof value !== 'string' || !/^sha256:[0-9a-f]{64}$/.test(value)) fail('canonical-world-schema', path, 'expected sha256 digest')
  return value
}

const repositoryPath = (value, path) => {
  try {
    return assertRepositoryPathV1(value, path)
  } catch (error) {
    fail('canonical-world-schema', path, error.message)
  }
}

const sortedUnique = (values, identity, path, { allowIdentical = false } = {}) => {
  const sorted = [...values].sort((left, right) => compareCanonicalTextV1(identity(left), identity(right)))
  const result = []
  for (const value of sorted) {
    const previous = result.at(-1)
    if (previous && identity(previous) === identity(value)) {
      if (allowIdentical && encodeCanonicalJsonV1(previous) === encodeCanonicalJsonV1(value)) continue
      fail('canonical-world-duplicate-identity', path, `duplicate identity ${identity(value)}`)
    }
    result.push(value)
  }
  return result
}

const sourcePair = (value, path) => {
  exactObject(value, ['implementation_path', 'implementation_digest', 'signature_path', 'signature_digest'], path)
  const implementationPath = repositoryPath(value.implementation_path, `${path}.implementation_path`)
  const signaturePath = repositoryPath(value.signature_path, `${path}.signature_path`)
  if (!implementationPath.endsWith('.fs') || implementationPath.endsWith('.fsi')
    || signaturePath !== `${implementationPath.slice(0, -3)}.fsi`) {
    fail('canonical-world-schema', path, 'implementation and sibling signature paths do not match')
  }
  return {
    implementation_path: implementationPath,
    implementation_digest: digest(value.implementation_digest, `${path}.implementation_digest`),
    signature_path: signaturePath,
    signature_digest: digest(value.signature_digest, `${path}.signature_digest`),
  }
}

const LOCALITY_KINDS = new Set(['contract', 'runtime', 'adapter', 'composition'])

const locality = (value, path) => {
  exactObject(value, ['id', 'owner', 'kind', 'project_path', 'sources'], path)
  if (!LOCALITY_KINDS.has(value.kind)) fail('canonical-world-schema', `${path}.kind`, 'unknown locality kind')
  const sources = sortedUnique(
    array(value.sources, `${path}.sources`).map((row, index) => sourcePair(row, `${path}.sources[${index}]`)),
    (row) => row.implementation_path,
    `${path}.sources`,
  )
  if (sources.length === 0) fail('canonical-world-schema', `${path}.sources`, 'locality must have at least one source pair')
  return {
    id: text(value.id, `${path}.id`),
    owner: text(value.owner, `${path}.owner`),
    kind: value.kind,
    project_path: repositoryPath(value.project_path, `${path}.project_path`),
    sources,
  }
}

const projectReference = (value, path) => {
  exactObject(value, ['consumer_locality', 'provider_locality'], path)
  return {
    consumer_locality: text(value.consumer_locality, `${path}.consumer_locality`),
    provider_locality: text(value.provider_locality, `${path}.provider_locality`),
  }
}

const sourceEdge = (value, path) => {
  exactObject(value, ['consumer_locality', 'consumer_source', 'provider_locality', 'provider_source'], path)
  return {
    consumer_locality: text(value.consumer_locality, `${path}.consumer_locality`),
    consumer_source: repositoryPath(value.consumer_source, `${path}.consumer_source`),
    provider_locality: text(value.provider_locality, `${path}.provider_locality`),
    provider_source: repositoryPath(value.provider_source, `${path}.provider_source`),
  }
}

const linkage = (value, path) => {
  exactObject(value, ['import_specifier', 'package_import_target', 'generator_path', 'generator_entry', 'input_selector_path', 'input_selector_entry', 'build_path', 'build_entry'], path)
  return {
    import_specifier: text(value.import_specifier, `${path}.import_specifier`),
    package_import_target: text(value.package_import_target, `${path}.package_import_target`),
    generator_path: repositoryPath(value.generator_path, `${path}.generator_path`),
    generator_entry: text(value.generator_entry, `${path}.generator_entry`),
    input_selector_path: repositoryPath(value.input_selector_path, `${path}.input_selector_path`),
    input_selector_entry: text(value.input_selector_entry, `${path}.input_selector_entry`),
    build_path: repositoryPath(value.build_path, `${path}.build_path`),
    build_entry: text(value.build_entry, `${path}.build_entry`),
  }
}

const generatedArtifact = (value, path) => {
  exactObject(value, ['id', 'artifact_path', 'artifact_digest', 'selected_inputs_digest', 'linkage', 'javascript_traversal_id'], path)
  const result = {
    id: text(value.id, `${path}.id`),
    artifact_path: repositoryPath(value.artifact_path, `${path}.artifact_path`),
    artifact_digest: digest(value.artifact_digest, `${path}.artifact_digest`),
    selected_inputs_digest: digest(value.selected_inputs_digest, `${path}.selected_inputs_digest`),
    linkage: linkage(value.linkage, `${path}.linkage`),
    javascript_traversal_id: text(value.javascript_traversal_id, `${path}.javascript_traversal_id`),
  }
  if (result.id !== generatedArtifactIdV1(result)) fail('canonical-world-schema', `${path}.id`, 'generated artifact identity does not match path and linkage')
  if (result.javascript_traversal_id !== javascriptTraversalIdV1('generated-artifact', result.id)) {
    fail('canonical-world-schema', `${path}.javascript_traversal_id`, 'generated artifact traversal identity does not match artifact identity')
  }
  return result
}

const javascriptTraversal = (value, path) => {
  exactObject(value, ['id', 'source_kind', 'source_id', 'ast_node_count', 'visited_node_count', 'no_capability_node_count', 'capability_emitting_node_count', 'unknown_node_count', 'ast_node_set_digest', 'visit_partition_digest'], path)
  if (!['fable-emit', 'emit-js-expr', 'generated-artifact'].includes(value.source_kind)) fail('canonical-world-schema', `${path}.source_kind`, 'unknown JavaScript source kind')
  const result = {
    id: text(value.id, `${path}.id`),
    source_kind: value.source_kind,
    source_id: text(value.source_id, `${path}.source_id`),
    ast_node_count: integer(value.ast_node_count, `${path}.ast_node_count`),
    visited_node_count: integer(value.visited_node_count, `${path}.visited_node_count`),
    no_capability_node_count: integer(value.no_capability_node_count, `${path}.no_capability_node_count`),
    capability_emitting_node_count: integer(value.capability_emitting_node_count, `${path}.capability_emitting_node_count`),
    unknown_node_count: integer(value.unknown_node_count, `${path}.unknown_node_count`),
    ast_node_set_digest: digest(value.ast_node_set_digest, `${path}.ast_node_set_digest`),
    visit_partition_digest: digest(value.visit_partition_digest, `${path}.visit_partition_digest`),
  }
  if (result.id !== javascriptTraversalIdV1(result.source_kind, result.source_id)) {
    fail('canonical-world-schema', `${path}.id`, 'JavaScript traversal identity does not match its source')
  }
  if (result.ast_node_count === 0) {
    fail('canonical-world-schema', `${path}.ast_node_count`, 'JavaScript traversal must cover a non-empty AST')
  }
  if (result.visited_node_count !== result.ast_node_count
    || result.visited_node_count !== result.no_capability_node_count + result.capability_emitting_node_count + result.unknown_node_count) {
    fail('canonical-world-schema', path, 'JavaScript traversal counts do not form one complete partition')
  }
  return result
}

const extractionCoverage = (value, path) => {
  exactObject(value, ['capability_observation_count', 'irrelevant_count', 'classified_count', 'unknown_count', 'capability_observation_digest', 'disposition_digest'], path)
  const result = {
    capability_observation_count: integer(value.capability_observation_count, `${path}.capability_observation_count`),
    irrelevant_count: integer(value.irrelevant_count, `${path}.irrelevant_count`),
    classified_count: integer(value.classified_count, `${path}.classified_count`),
    unknown_count: integer(value.unknown_count, `${path}.unknown_count`),
    capability_observation_digest: digest(value.capability_observation_digest, `${path}.capability_observation_digest`),
    disposition_digest: digest(value.disposition_digest, `${path}.disposition_digest`),
  }
  if (result.irrelevant_count + result.classified_count + result.unknown_count !== result.capability_observation_count) {
    fail('canonical-world-schema', path, 'capability extraction counts do not partition observations')
  }
  return result
}

const semanticEvidence = (value, path) => {
  exactObject(value, ['path', 'title', 'what_id', 'surface_module'], path)
  return {
    path: repositoryPath(value.path, `${path}.path`),
    title: text(value.title, `${path}.title`),
    what_id: text(value.what_id, `${path}.what_id`),
    surface_module: text(value.surface_module, `${path}.surface_module`),
  }
}

const uniqueTexts = (value, path) => sortedUnique(
  array(value, path).map((item, index) => text(item, `${path}[${index}]`)),
  (item) => item,
  path,
)

const evidenceRows = (value, path) => sortedUnique(
  array(value, path).map((row, index) => semanticEvidence(row, `${path}[${index}]`)),
  (row) => `${row.what_id}\0${row.path}\0${row.title}\0${row.surface_module}`,
  path,
)

const validateLawEvidence = (laws, evidence, path) => {
  if (laws.length === 0 || evidence.length === 0) fail('canonical-world-schema', path, 'law and semantic evidence collections must be non-empty')
  const evidenceLaws = new Set(evidence.map(({ what_id: whatId }) => whatId))
  if (laws.some((law) => !evidenceLaws.has(law)) || evidence.some(({ what_id: whatId }) => !laws.includes(whatId))) {
    fail('canonical-world-schema', path, 'laws and semantic evidence must cover each other')
  }
}

const slice = (value, path) => {
  if (!plainObject(value) || !plainObject(value.classification)) fail('canonical-world-schema', path, 'slice and classification must be objects')
  const bounded = value.classification.kind === 'contract' && value.classification.exposure === 'bounded'
  exactObject(value, bounded
    ? ['id', 'owner', 'provider_locality', 'classification', 'allowed_direct_consumers', 'allowed_effective_consumers', 'laws', 'semantic_evidence']
    : ['id', 'owner', 'provider_locality', 'classification', 'allowed_direct_consumers', 'laws', 'semantic_evidence'], path)
  const classificationKeys = value.classification.kind === 'composition' ? ['kind'] : ['kind', 'exposure']
  exactObject(value.classification, classificationKeys, `${path}.classification`)
  const tag = value.classification.kind === 'composition'
    ? 'composition'
    : `${value.classification.kind}/${value.classification.exposure}`
  if (!['contract/shared', 'contract/bounded', 'runtime/effect', 'adapter/effect', 'composition'].includes(tag)) {
    fail('canonical-world-schema', `${path}.classification`, 'invalid terminal classification')
  }
  const allowedDirectConsumers = uniqueTexts(value.allowed_direct_consumers, `${path}.allowed_direct_consumers`)
  const laws = uniqueTexts(value.laws, `${path}.laws`)
  const semanticEvidenceRows = evidenceRows(value.semantic_evidence, `${path}.semantic_evidence`)
  if (allowedDirectConsumers.length === 0) fail('canonical-world-schema', `${path}.allowed_direct_consumers`, 'published slice must have a direct consumer')
  validateLawEvidence(laws, semanticEvidenceRows, path)
  return {
    id: text(value.id, `${path}.id`),
    owner: text(value.owner, `${path}.owner`),
    provider_locality: text(value.provider_locality, `${path}.provider_locality`),
    classification: { ...value.classification },
    allowed_direct_consumers: allowedDirectConsumers,
    ...(bounded ? { allowed_effective_consumers: uniqueTexts(value.allowed_effective_consumers, `${path}.allowed_effective_consumers`) } : {}),
    laws,
    semantic_evidence: semanticEvidenceRows,
  }
}

const capabilityRelation = (value, path) => {
  exactObject(value, ['id', 'kind', 'consumer_locality', 'provider_slice', 'consumer_module', 'provider_surface_module', 'laws', 'semantic_evidence'], path)
  if (!['physical-port', 'adapter', 'composition-wiring'].includes(value.kind)) fail('canonical-world-schema', `${path}.kind`, 'unknown capability relation kind')
  const laws = uniqueTexts(value.laws, `${path}.laws`)
  const semanticEvidenceRows = evidenceRows(value.semantic_evidence, `${path}.semantic_evidence`)
  validateLawEvidence(laws, semanticEvidenceRows, path)
  return {
    id: text(value.id, `${path}.id`),
    kind: value.kind,
    consumer_locality: text(value.consumer_locality, `${path}.consumer_locality`),
    provider_slice: text(value.provider_slice, `${path}.provider_slice`),
    consumer_module: text(value.consumer_module, `${path}.consumer_module`),
    provider_surface_module: text(value.provider_surface_module, `${path}.provider_surface_module`),
    laws,
    semantic_evidence: semanticEvidenceRows,
  }
}

const entryPoint = (value, path) => {
  exactObject(value, ['path', 'entry'], path)
  return { path: repositoryPath(value.path, `${path}.path`), entry: text(value.entry, `${path}.entry`) }
}

const determinismProof = (value, path) => {
  exactObject(value, ['path', 'title', 'what_id'], path)
  return {
    path: repositoryPath(value.path, `${path}.path`),
    title: text(value.title, `${path}.title`),
    what_id: text(value.what_id, `${path}.what_id`),
  }
}

const generatedModuleRelation = (value, path) => {
  exactObject(value, ['id', 'kind', 'consumer_locality', 'import_specifier', 'generated_owner', 'package_import_target', 'generator', 'build_invocation', 'input_selector', 'runtime_surface_module', 'laws', 'determinism_proof'], path)
  if (value.kind !== 'compile-contract-support') fail('canonical-world-schema', `${path}.kind`, 'unknown generated relation kind')
  const proof = determinismProof(value.determinism_proof, `${path}.determinism_proof`)
  const laws = uniqueTexts(value.laws, `${path}.laws`)
  if (laws.length !== 1 || laws[0] !== proof.what_id) fail('canonical-world-schema', `${path}.laws`, 'generated relation law must equal its determinism proof WHAT')
  return {
    id: text(value.id, `${path}.id`),
    kind: value.kind,
    consumer_locality: text(value.consumer_locality, `${path}.consumer_locality`),
    import_specifier: text(value.import_specifier, `${path}.import_specifier`),
    generated_owner: text(value.generated_owner, `${path}.generated_owner`),
    package_import_target: text(value.package_import_target, `${path}.package_import_target`),
    generator: entryPoint(value.generator, `${path}.generator`),
    build_invocation: entryPoint(value.build_invocation, `${path}.build_invocation`),
    input_selector: entryPoint(value.input_selector, `${path}.input_selector`),
    runtime_surface_module: text(value.runtime_surface_module, `${path}.runtime_surface_module`),
    laws,
    determinism_proof: proof,
  }
}

const opaqueCapabilityFact = (value, path) => {
  if (!validateCanonicalCapabilityFactV1(value)) fail('canonical-world-schema', path, 'capability fact is not closed or its identities do not match')
  return structuredClone(value)
}

const validateReferences = (world) => {
  const localities = new Map(world.observed.localities.map((row) => [row.id, row]))
  const artifacts = new Map(world.observed.generated_artifacts.map((row) => [row.id, row]))
  const traversals = new Map(world.observed.javascript_traversals.map((row) => [row.id, row]))
  const sourceOwner = new Map()
  for (const row of localities.values()) {
    for (const source of row.sources) {
      for (const path of [source.implementation_path, source.signature_path]) {
        if (sourceOwner.has(path)) fail('canonical-world-duplicate-identity', '$.observed.localities', `source ${path} belongs to multiple localities`)
        sourceOwner.set(path, row.id)
      }
    }
  }
  for (const [path, consumer, provider] of [
    ...world.observed.project_references.map((row) => ['$.observed.project_references', row.consumer_locality, row.provider_locality]),
    ...world.observed.actual_source_edges.map((row) => ['$.observed.actual_source_edges', row.consumer_locality, row.provider_locality]),
  ]) {
    if (!localities.has(consumer) || !localities.has(provider)) fail('canonical-world-schema', path, 'locality reference does not resolve')
  }
  for (const edge of world.observed.actual_source_edges) {
    if (sourceOwner.get(edge.consumer_source) !== edge.consumer_locality || sourceOwner.get(edge.provider_source) !== edge.provider_locality) {
      fail('canonical-world-schema', '$.observed.actual_source_edges', 'source edge does not match source ownership')
    }
  }
  const slices = new Map(world.normative.slices.map((row) => [row.id, row]))
  const slicesByProvider = new Set()
  for (const row of world.normative.slices) {
    const provider = localities.get(row.provider_locality)
    if (!provider || provider.owner !== row.owner || provider.kind !== row.classification.kind) {
      fail('canonical-world-schema', '$.normative.slices', 'slice owner or kind does not match its locality')
    }
    if (slicesByProvider.has(row.provider_locality)) fail('canonical-world-duplicate-identity', '$.normative.slices', `provider ${row.provider_locality} has multiple slices`)
    slicesByProvider.add(row.provider_locality)
    for (const consumer of [
      ...row.allowed_direct_consumers,
      ...(row.allowed_effective_consumers ?? []),
    ]) if (!localities.has(consumer)) fail('canonical-world-schema', '$.normative.slices', `slice consumer ${consumer} does not resolve`)
  }
  for (const relation of world.normative.capability_relations) {
    if (!localities.has(relation.consumer_locality) || !slices.has(relation.provider_slice)) fail('canonical-world-schema', '$.normative.capability_relations', 'relation endpoint does not resolve')
  }
  for (const relation of world.normative.generated_module_relations) {
    if (!localities.has(relation.consumer_locality)
      || ![...localities.values()].some(({ owner }) => owner === relation.generated_owner)) {
      fail('canonical-world-schema', '$.normative.generated_module_relations', 'generated relation consumer or owner does not resolve')
    }
  }
  for (const artifact of artifacts.values()) {
    const traversal = traversals.get(artifact.javascript_traversal_id)
    if (!traversal || traversal.source_kind !== 'generated-artifact' || traversal.source_id !== artifact.id) {
      fail('canonical-world-schema', '$.observed.generated_artifacts', 'generated artifact traversal does not resolve')
    }
  }
  for (const traversal of traversals.values()) {
    if (traversal.source_kind === 'generated-artifact') {
      if (!artifacts.has(traversal.source_id)) fail('canonical-world-schema', '$.observed.javascript_traversals', 'generated traversal source does not resolve')
      continue
    }
    const sourceFact = world.observed.capability_facts.find(({ observation }) => observation.case === traversal.source_kind
      && observation.payload.javascript_traversal_id === traversal.id
      && javascriptSourceIdV1(observation.payload.expression, observation.payload.site) === traversal.source_id)
    if (!sourceFact) fail('canonical-world-schema', '$.observed.javascript_traversals', 'Fable interop traversal source does not resolve')
  }
  for (const fact of world.observed.capability_facts) {
    const site = fact.observation.payload.site
    if (!localities.has(site.locality_id) || sourceOwner.get(site.source_path) !== site.locality_id) {
      fail('canonical-world-schema', '$.observed.capability_facts', 'capability fact site does not match source ownership')
    }
    const { observation } = fact
    if (observation.case === 'fable-import' && observation.payload.generated_artifact_id !== null
      && !artifacts.has(observation.payload.generated_artifact_id)) {
      fail('canonical-world-schema', '$.observed.capability_facts', 'Fable import artifact does not resolve')
    }
    if (['fable-emit', 'emit-js-expr'].includes(observation.case) && observation.payload.javascript_traversal_id !== null) {
      const sourceId = javascriptSourceIdV1(observation.payload.expression, observation.payload.site)
      const traversal = traversals.get(observation.payload.javascript_traversal_id)
      if (observation.payload.javascript_traversal_id !== javascriptTraversalIdV1(observation.case, sourceId)
        || !traversal
        || traversal.source_kind !== observation.case
        || traversal.source_id !== sourceId) {
        fail('canonical-world-schema', '$.observed.capability_facts', 'Fable interop traversal does not resolve')
      }
    }
    if (observation.case === 'javascript-capability') {
      const traversalId = javascriptTraversalIdV1(observation.payload.source_kind, observation.payload.source_id)
      if (!traversals.has(traversalId)
        || (observation.payload.source_kind === 'generated-artifact' && !artifacts.has(observation.payload.generated_artifact_id))) {
        fail('canonical-world-schema', '$.observed.capability_facts', 'JavaScript capability source does not resolve')
      }
      if (observation.payload.source_kind !== 'generated-artifact') {
        const sourceObservation = world.observed.capability_facts
          .map(({ observation: candidate }) => candidate)
          .find((candidate) => candidate.case === observation.payload.source_kind
            && javascriptSourceIdV1(candidate.payload.expression, candidate.payload.site) === observation.payload.source_id)
        const sourceSite = sourceObservation?.payload.site
        if (!sourceSite
          || sourceSite.locality_id !== site.locality_id
          || sourceSite.source_path !== site.source_path
          || sourceSite.semantic_declaration_anchor !== site.semantic_declaration_anchor
          || site.same_anchor_occurrence_ordinal < sourceSite.same_anchor_occurrence_ordinal) {
          fail('canonical-world-schema', '$.observed.capability_facts', 'JavaScript capability site does not match its Fable interop source')
        }
      }
    }
  }
}

export const buildCanonicalWorldV1 = (input) => {
  exactObject(input, ['schema_version', 'fact_schema_version', 'observed', 'normative'], '$')
  if (input.schema_version !== 1 || input.fact_schema_version !== 1) fail('canonical-world-schema', '$', 'world and fact schema versions must be 1')
  exactObject(input.observed, ['localities', 'project_references', 'actual_source_edges', 'generated_artifacts', 'javascript_traversals', 'capability_extraction', 'capability_facts'], '$.observed')
  exactObject(input.normative, ['authorization_schema_version', 'slices', 'capability_relations', 'generated_module_relations'], '$.normative')
  if (input.normative.authorization_schema_version !== 2) fail('canonical-world-schema', '$.normative.authorization_schema_version', 'authorization schema version must be 2')

  const world = {
    schema_version: 1,
    fact_schema_version: 1,
    observed: {
      localities: sortedUnique(array(input.observed.localities, '$.observed.localities').map((row, index) => locality(row, `$.observed.localities[${index}]`)), (row) => row.id, '$.observed.localities'),
      project_references: sortedUnique(array(input.observed.project_references, '$.observed.project_references').map((row, index) => projectReference(row, `$.observed.project_references[${index}]`)), (row) => `${row.consumer_locality}\0${row.provider_locality}`, '$.observed.project_references'),
      actual_source_edges: sortedUnique(array(input.observed.actual_source_edges, '$.observed.actual_source_edges').map((row, index) => sourceEdge(row, `$.observed.actual_source_edges[${index}]`)), (row) => `${row.consumer_locality}\0${row.consumer_source}\0${row.provider_locality}\0${row.provider_source}`, '$.observed.actual_source_edges', { allowIdentical: true }),
      generated_artifacts: sortedUnique(array(input.observed.generated_artifacts, '$.observed.generated_artifacts').map((row, index) => generatedArtifact(row, `$.observed.generated_artifacts[${index}]`)), (row) => row.id, '$.observed.generated_artifacts'),
      javascript_traversals: sortedUnique(array(input.observed.javascript_traversals, '$.observed.javascript_traversals').map((row, index) => javascriptTraversal(row, `$.observed.javascript_traversals[${index}]`)), (row) => row.id, '$.observed.javascript_traversals'),
      capability_extraction: extractionCoverage(input.observed.capability_extraction, '$.observed.capability_extraction'),
      capability_facts: sortedUnique(array(input.observed.capability_facts, '$.observed.capability_facts').map((row, index) => opaqueCapabilityFact(row, `$.observed.capability_facts[${index}]`)), (row) => row.observation_id, '$.observed.capability_facts', { allowIdentical: true }),
    },
    normative: {
      authorization_schema_version: 2,
      slices: sortedUnique(array(input.normative.slices, '$.normative.slices').map((row, index) => slice(row, `$.normative.slices[${index}]`)), (row) => row.id, '$.normative.slices'),
      capability_relations: sortedUnique(array(input.normative.capability_relations, '$.normative.capability_relations').map((row, index) => capabilityRelation(row, `$.normative.capability_relations[${index}]`)), (row) => row.id, '$.normative.capability_relations'),
      generated_module_relations: sortedUnique(array(input.normative.generated_module_relations, '$.normative.generated_module_relations').map((row, index) => generatedModuleRelation(row, `$.normative.generated_module_relations[${index}]`)), (row) => row.id, '$.normative.generated_module_relations'),
    },
  }
  const derivedCapabilityPartition = validateCapabilityPartitionV1({
    observations: world.observed.capability_facts.map(({ observation }) => observation),
    facts: world.observed.capability_facts,
  })
  if (encodeCanonicalJsonV1(world.observed.capability_extraction) !== encodeCanonicalJsonV1(derivedCapabilityPartition.coverage)) {
    fail('canonical-world-schema', '$.observed.capability_extraction', 'capability extraction coverage does not match canonical facts')
  }
  validateReferences(world)
  return world
}

export const serializeCanonicalWorldV1 = (world) => encodeCanonicalJsonV1(buildCanonicalWorldV1(world))

export const canonicalWorldDigestV1 = (world) => canonicalDigestV1('canonical-world/v1\0', buildCanonicalWorldV1(world))

const adjacency = (world) => {
  const result = new Map(world.observed.localities.map(({ id }) => [id, []]))
  for (const { consumer_locality: consumer, provider_locality: provider } of world.observed.project_references) result.get(consumer).push(provider)
  return result
}

const forwardProjectClosure = (world, localityId) => {
  const graph = adjacency(world)
  if (!graph.has(localityId)) fail('canonical-world-schema', '$.locality_id', `unknown locality ${localityId}`)
  const seen = new Set([localityId])
  const pending = [localityId]
  while (pending.length > 0) {
    for (const provider of graph.get(pending.pop())) {
      if (seen.has(provider)) continue
      seen.add(provider)
      pending.push(provider)
    }
  }
  return [...seen].sort(compareCanonicalTextV1)
}

export const forwardProjectClosureV1 = (worldInput, localityId) =>
  forwardProjectClosure(buildCanonicalWorldV1(worldInput), localityId)

const actualEffectiveConsumers = (world, localityId) => {
  const reverse = new Map(world.observed.localities.map(({ id }) => [id, []]))
  if (!reverse.has(localityId)) fail('canonical-world-schema', '$.locality_id', `unknown locality ${localityId}`)
  for (const { consumer_locality: consumer, provider_locality: provider } of world.observed.project_references) reverse.get(provider).push(consumer)
  const seen = new Set([localityId])
  const pending = [localityId]
  while (pending.length > 0) {
    for (const consumer of reverse.get(pending.pop())) {
      if (seen.has(consumer)) continue
      seen.add(consumer)
      pending.push(consumer)
    }
  }
  seen.delete(localityId)
  return [...seen].sort(compareCanonicalTextV1)
}

export const actualEffectiveConsumersV1 = (worldInput, localityId) =>
  actualEffectiveConsumers(buildCanonicalWorldV1(worldInput), localityId)

const terminal = (name) => ({ case: name, payload: {} })

export const classifyTerminalV1 = (worldInput, localityId) => {
  const world = buildCanonicalWorldV1(worldInput)
  const localityRow = world.observed.localities.find(({ id }) => id === localityId)
  if (!localityRow) fail('canonical-world-schema', '$.locality_id', `unknown locality ${localityId}`)
  const slices = world.normative.slices.filter(({ provider_locality: provider }) => provider === localityId)
  if (slices.length === 0) return terminal('private')
  if (slices.length !== 1) fail('canonical-world-schema', '$.normative.slices', `locality ${localityId} has multiple slices`)
  const classification = slices[0].classification
  if (classification.kind === 'composition') return terminal('composition-terminal')
  return terminal(`${classification.kind}-${classification.exposure}`)
}

const missingClosureEdges = (world) => world.observed.actual_source_edges.filter((edge) =>
  edge.consumer_locality !== edge.provider_locality
  && !forwardProjectClosure(world, edge.consumer_locality).includes(edge.provider_locality))

export const deriveAdjudicationCandidates = (worldInput) => {
  const world = buildCanonicalWorldV1(worldInput)
  const referenced = new Set(world.observed.project_references.map((row) => row.provider_locality))
  for (const edge of world.observed.actual_source_edges) referenced.add(edge.provider_locality)
  const missing = missingClosureEdges(world)
  const sliceById = new Map(world.normative.slices.map((row) => [row.id, row]))
  return world.observed.localities.map((row) => {
    const reasons = new Set(['TerminalClassificationRequired'])
    if (referenced.has(row.id)) reasons.add('ReferencedProvider')
    if (row.kind === 'composition' && referenced.has(row.id)) reasons.add('CompositionProvider')
    const localFacts = world.observed.capability_facts.filter((fact) => factLocality(fact) === row.id)
    if (localFacts.some(({ disposition }) => disposition.case !== 'irrelevant')) reasons.add('CapabilityBearing')
    if (declaredKindMismatch(world, row.id)) reasons.add('KindCapabilityMismatch')
    for (const sliceRow of world.normative.slices) {
      if (sliceRow.provider_locality === row.id) reasons.add(`RelationEndpoint:slice:provider:${sliceRow.id}`)
    }
    for (const relation of world.normative.capability_relations) {
      const provider = sliceById.get(relation.provider_slice)?.provider_locality
      if (relation.consumer_locality === row.id) reasons.add(`RelationEndpoint:${relation.kind}:consumer:${relation.id}`)
      if (provider === row.id) reasons.add(`RelationEndpoint:${relation.kind}:provider:${relation.id}`)
    }
    for (const relation of world.normative.generated_module_relations) {
      if (relation.consumer_locality === row.id) reasons.add(`RelationEndpoint:generated-module:consumer:${relation.id}`)
    }
    if (missing.some((edge) => edge.consumer_locality === row.id || edge.provider_locality === row.id)) reasons.add('MissingClosureEndpoint')
    return { locality_id: row.id, reasons: [...reasons].sort(compareCanonicalTextV1) }
  })
}

export const queryDigestV1 = (queryId, result) => {
  if (typeof queryId !== 'string' || !/^(surface|audience|capability)\/v1:[A-Za-z0-9._:-]+$/.test(queryId)) {
    fail('canonical-world-schema', '$.query_id', 'query ID must name one canonical locality query')
  }
  return canonicalDigestV1(`canonical-query/v1\0${queryId}\0`, result)
}

const factLocality = (fact) => fact.observation?.payload?.site?.locality_id ?? fact.observation?.site?.locality_id ?? null

const declaredKindMismatch = (world, localityId) => {
  const localityRow = world.observed.localities.find(({ id }) => id === localityId)
  if (!localityRow) fail('canonical-world-schema', '$.locality_id', `unknown locality ${localityId}`)
  if (localityRow.kind !== 'contract') return false
  return world.observed.capability_facts
    .filter((fact) => factLocality(fact) === localityId)
    .some(({ disposition }) => capabilityDispositionViolatesContractV1(disposition))
}

export const declaredKindMismatchV1 = (worldInput, localityId) =>
  declaredKindMismatch(buildCanonicalWorldV1(worldInput), localityId)

export const queryCanonicalLocalityV1 = (worldInput, localityId) => {
  const world = buildCanonicalWorldV1(worldInput)
  const localityRow = world.observed.localities.find(({ id }) => id === localityId)
  if (!localityRow) fail('canonical-world-schema', '$.locality_id', `unknown locality ${localityId}`)
  const sourceConsumers = [...new Set(world.observed.actual_source_edges
    .filter(({ provider_locality: provider }) => provider === localityId)
    .map(({ consumer_locality: consumer }) => consumer))]
    .sort(compareCanonicalTextV1)
  const directConsumers = [...new Set(world.observed.project_references
    .filter(({ provider_locality: provider }) => provider === localityId)
    .map(({ consumer_locality: consumer }) => consumer))]
    .sort(compareCanonicalTextV1)
  const sliceById = new Map(world.normative.slices.map((row) => [row.id, row]))
  const relationEndpoints = []
  for (const relation of world.normative.capability_relations) {
    if (relation.consumer_locality === localityId) relationEndpoints.push({ relation_kind: relation.kind, role: 'consumer', relation_id: relation.id })
    if (sliceById.get(relation.provider_slice)?.provider_locality === localityId) relationEndpoints.push({ relation_kind: relation.kind, role: 'provider', relation_id: relation.id })
  }
  for (const relation of world.normative.generated_module_relations) {
    if (relation.consumer_locality === localityId) relationEndpoints.push({ relation_kind: 'generated-module', role: 'consumer', relation_id: relation.id })
  }
  relationEndpoints.sort((left, right) => compareCanonicalTextV1(`${left.relation_kind}\0${left.role}\0${left.relation_id}`, `${right.relation_kind}\0${right.role}\0${right.relation_id}`))
  const localFacts = world.observed.capability_facts.filter((fact) => factLocality(fact) === localityId)
  const artifactIds = new Set(localFacts.flatMap(({ observation }) => {
    if (observation.case === 'javascript-capability' && observation.payload.generated_artifact_id !== null) return [observation.payload.generated_artifact_id]
    if (observation.case === 'fable-import' && observation.payload.generated_artifact_id !== null) return [observation.payload.generated_artifact_id]
    return []
  }))
  const generatedArtifacts = world.observed.generated_artifacts.filter(({ id }) => artifactIds.has(id))
  const traversalIds = new Set([
    ...localFacts.flatMap(({ observation }) => ['fable-emit', 'emit-js-expr'].includes(observation.case)
      && observation.payload.javascript_traversal_id !== null
      ? [observation.payload.javascript_traversal_id]
      : []),
    ...generatedArtifacts.map(({ javascript_traversal_id: traversalId }) => traversalId),
  ])
  const javascriptTraversals = world.observed.javascript_traversals.filter(({ id }) => traversalIds.has(id))
  const signatures = localityRow.sources.map((source) => ({
    signature_path: source.signature_path,
    signature_digest: source.signature_digest,
    exports: sortedUnique(localFacts
      .filter((fact) => fact.observation?.case === 'public-signature-export' && fact.observation.payload.site.source_path === source.signature_path)
      .map((fact) => ({
        export_kind: fact.observation.payload.export_kind,
        declaration_identity: fact.observation.payload.declaration_identity,
      })),
    (row) => `${row.export_kind}\0${row.declaration_identity}`,
    '$.query.surface.exports', { allowIdentical: true }),
  }))
  const missing = missingClosureEdges(world).filter((edge) => edge.consumer_locality === localityId || edge.provider_locality === localityId)
  return {
    surface: { signatures },
    audience: {
      direct_project_consumers: directConsumers,
      actual_source_consumers: sourceConsumers,
      reverse_closure_effective_consumers: actualEffectiveConsumers(world, localityId),
      relation_endpoints: relationEndpoints,
      missing_closure_violations: missing,
    },
    capability: {
      facts: localFacts,
      generated_artifacts: generatedArtifacts,
      javascript_traversals: javascriptTraversals,
      declared_kind_mismatch: declaredKindMismatch(world, localityId),
    },
  }
}

export const projectManifestClaimsV1 = (worldInput, localityId) => {
  const world = buildCanonicalWorldV1(worldInput)
  if (!world.observed.localities.some(({ id }) => id === localityId)) fail('canonical-world-schema', '$.locality_id', `unknown locality ${localityId}`)
  const providerSlices = world.normative.slices.filter(({ provider_locality: provider }) => provider === localityId)
  if (providerSlices.length > 1) fail('canonical-world-schema', '$.normative.slices', `locality ${localityId} has multiple slices`)
  return {
    provider_slice_id: providerSlices[0]?.id ?? null,
    consumer_capability_relation_ids: world.normative.capability_relations
      .filter(({ consumer_locality: consumer }) => consumer === localityId)
      .map(({ id }) => id)
      .sort(compareCanonicalTextV1),
    consumer_generated_module_relation_ids: world.normative.generated_module_relations
      .filter(({ consumer_locality: consumer }) => consumer === localityId)
      .map(({ id }) => id)
      .sort(compareCanonicalTextV1),
  }
}
