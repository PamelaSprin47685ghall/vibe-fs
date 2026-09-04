import path from 'node:path'

import {
  assertRepositoryPathV1,
  compareCanonicalTextV1,
  encodeCanonicalJsonV1,
  sha256BytesV1,
} from './canonical-json-v1.mjs'
import {
  capabilityDispositionViolatesContractV1,
  javascriptTraversalIdV1,
  validateCanonicalCapabilityFactV1,
} from './capability-observations-v1.mjs'

export class GeneratedArtifactV1Error extends TypeError {
  constructor(code, coordinates, message) {
    super(message)
    this.name = 'GeneratedArtifactV1Error'
    this.code = code
    Object.assign(this, coordinates)
  }
}

const fail = (code, coordinates, message) => {
  throw new GeneratedArtifactV1Error(code, coordinates, message)
}

const bytesOf = (value, pathValue) => {
  if (Buffer.isBuffer(value)) return Buffer.from(value)
  if (value instanceof Uint8Array) return Buffer.from(value.buffer, value.byteOffset, value.byteLength)
  fail('generated-input-not-bytes', { path: pathValue }, 'tracking reader must return bytes')
}

export const createTrackingReaderV1 = ({ root = null, readFile }) => {
  if (typeof readFile !== 'function') throw new TypeError('tracking reader requires readFile')
  const reads = new Map()
  const read = (repositoryPath) => {
    assertRepositoryPathV1(repositoryPath, '$.tracking_reader.path')
    const bytes = bytesOf(readFile(root === null ? repositoryPath : path.join(root, repositoryPath)), repositoryPath)
    const previous = reads.get(repositoryPath)
    if (previous && !previous.bytes.equals(bytes)) {
      fail('generated-input-changed-during-read', { path: repositoryPath }, 'one tracked path produced different bytes')
    }
    const row = { path: repositoryPath, blob_digest: sha256BytesV1(bytes), bytes }
    reads.set(repositoryPath, row)
    return row
  }
  return {
    read,
    rows: () => [...reads.values()].sort((left, right) => compareCanonicalTextV1(left.path, right.path)),
  }
}

export const canonicalizeSelectedInputPathsV1 = (root, inputPaths) => {
  if (typeof root !== 'string' || root.length === 0 || !Array.isArray(inputPaths)) {
    throw new TypeError('selected input path normalization requires repository root and filesystem paths')
  }
  const repositoryRoot = path.resolve(root)
  return inputPaths.map((inputPath) => {
    if (typeof inputPath !== 'string' || inputPath.length === 0) {
      fail('generated-selected-input-path-invalid', { path: inputPath }, 'selected input must be a filesystem path')
    }
    const absolutePath = path.isAbsolute(inputPath)
      ? path.resolve(inputPath)
      : path.resolve(repositoryRoot, inputPath)
    const relativePath = path.relative(repositoryRoot, absolutePath)
    if (relativePath === '' || relativePath === '..' || relativePath.startsWith(`..${path.sep}`) || path.isAbsolute(relativePath)) {
      fail('generated-selected-input-outside-root', { path: inputPath }, 'selected input must stay inside the repository root')
    }
    return assertRepositoryPathV1(relativePath.split(path.sep).join('/'), '$.selected_inputs.path')
  })
}

export const selectedInputsDigestV1 = (rows) => sha256BytesV1(Buffer.from(encodeCanonicalJsonV1(rows
  .map(({ path: inputPath, blob_digest: blobDigest }) => ({
    path: assertRepositoryPathV1(inputPath, '$.selected_inputs.path'),
    blob_digest: blobDigest,
  }))
  .sort((left, right) => compareCanonicalTextV1(left.path, right.path))), 'utf8'))

export const readSelectedInputsV1 = (inputPaths, trackingReader) => {
  if (!Array.isArray(inputPaths) || typeof trackingReader?.read !== 'function') throw new TypeError('selected inputs require paths and tracking reader')
  const normalized = inputPaths.map((inputPath) => assertRepositoryPathV1(inputPath, '$.selected_inputs.path'))
    .sort(compareCanonicalTextV1)
  for (let index = 1; index < normalized.length; index += 1) {
    if (normalized[index - 1] === normalized[index]) {
      fail('generated-selected-input-duplicate', { path: normalized[index] }, 'selected input paths must be unique')
    }
  }
  return normalized.map((inputPath) => trackingReader.read(inputPath))
}

export const generatedArtifactIdV1 = ({ artifact_path: artifactPath, linkage }) =>
  `generated-artifact/v1:${sha256BytesV1(Buffer.from(encodeCanonicalJsonV1({
    artifact_path: assertRepositoryPathV1(artifactPath, '$.artifact_path'),
    linkage,
  }), 'utf8')).slice('sha256:'.length)}`

export const buildGeneratedArtifactRowV1 = ({
  artifact_path: artifactPath,
  artifact_bytes: artifactBytes,
  selected_inputs: selectedInputs,
  linkage,
  javascript_traversal_id: javascriptTraversalId,
}) => {
  const id = generatedArtifactIdV1({ artifact_path: artifactPath, linkage })
  const expectedTraversalId = javascriptTraversalIdV1('generated-artifact', id)
  if (javascriptTraversalId !== undefined && javascriptTraversalId !== expectedTraversalId) {
    fail('generated-artifact-traversal-id-mismatch', { artifact_id: id }, 'traversal identity must be derived from the artifact identity')
  }
  return {
    id,
    artifact_path: assertRepositoryPathV1(artifactPath, '$.artifact_path'),
    artifact_digest: sha256BytesV1(bytesOf(artifactBytes, artifactPath)),
    selected_inputs_digest: selectedInputsDigestV1(selectedInputs),
    linkage: structuredClone(linkage),
    javascript_traversal_id: expectedTraversalId,
  }
}

const relationLinkage = (relation) => ({
  import_specifier: relation.import_specifier,
  package_import_target: relation.package_import_target,
  generator_path: relation.generator.path,
  generator_entry: relation.generator.entry,
  input_selector_path: relation.input_selector.path,
  input_selector_entry: relation.input_selector.entry,
  build_path: relation.build_invocation.path,
  build_entry: relation.build_invocation.entry,
})

const violation = (code, coordinates = {}) => ({ code, ...coordinates })

const countBy = (rows, identity) => {
  const result = new Map()
  for (const row of rows) {
    const key = identity(row)
    if (!result.has(key)) result.set(key, [])
    result.get(key).push(row)
  }
  return result
}

const bytesAt = (map, key) => map instanceof Map ? map.get(key) : map?.[key]

const exactKeys = (value, keys) => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return false
  const actual = Object.keys(value).sort(compareCanonicalTextV1)
  const expected = [...keys].sort(compareCanonicalTextV1)
  return actual.length === expected.length && actual.every((key, index) => key === expected[index])
}

const nonEmptyText = (value) => typeof value === 'string' && value.length > 0
const digestValid = (value) => typeof value === 'string' && /^sha256:[0-9a-f]{64}$/.test(value)

const repositoryPathValid = (value, jsonPath) => {
  try {
    assertRepositoryPathV1(value, jsonPath)
    return true
  } catch {
    return false
  }
}

const entryPointValid = (value, jsonPath) => exactKeys(value, ['path', 'entry'])
  && repositoryPathValid(value.path, `${jsonPath}.path`)
  && nonEmptyText(value.entry)

const linkageValid = (value, jsonPath) => exactKeys(value, [
  'import_specifier',
  'package_import_target',
  'generator_path',
  'generator_entry',
  'input_selector_path',
  'input_selector_entry',
  'build_path',
  'build_entry',
])
  && [value.import_specifier, value.package_import_target, value.generator_entry, value.input_selector_entry, value.build_entry].every(nonEmptyText)
  && [value.generator_path, value.input_selector_path, value.build_path]
    .every((entryPath) => repositoryPathValid(entryPath, jsonPath))

const sortedUniqueTexts = (values) => Array.isArray(values)
  && values.every(nonEmptyText)
  && values.every((value, index) => index === 0 || compareCanonicalTextV1(values[index - 1], value) < 0)

const sortedUniqueEntries = (values, jsonPath) => Array.isArray(values)
  && values.every((value, index) => entryPointValid(value, `${jsonPath}[${index}]`))
  && values.every((value, index) => index === 0
    || compareCanonicalTextV1(`${values[index - 1].path}\0${values[index - 1].entry}`, `${value.path}\0${value.entry}`) < 0)

const entryReachabilityIdentity = ({ from_entry: fromEntry, to_entry: toEntry }) =>
  `${fromEntry.path}\0${fromEntry.entry}\0${toEntry.path}\0${toEntry.entry}`

const entryReachabilityValid = (value, jsonPath) => exactKeys(value, ['from_entry', 'to_entry'])
  && entryPointValid(value.from_entry, `${jsonPath}.from_entry`)
  && entryPointValid(value.to_entry, `${jsonPath}.to_entry`)

const sortedUniqueEntryReachability = (values, jsonPath) => Array.isArray(values)
  && values.every((value, index) => entryReachabilityValid(value, `${jsonPath}[${index}]`))
  && values.every((value, index) => index === 0
    || compareCanonicalTextV1(entryReachabilityIdentity(values[index - 1]), entryReachabilityIdentity(value)) < 0)

const generatedRelationValid = (value) => exactKeys(value, [
  'id',
  'kind',
  'consumer_locality',
  'import_specifier',
  'generated_owner',
  'package_import_target',
  'generator',
  'build_invocation',
  'input_selector',
  'runtime_surface_module',
  'laws',
  'determinism_proof',
  'justification',
])
  && value.kind === 'compile-contract-support'
  && [
    value.id,
    value.consumer_locality,
    value.import_specifier,
    value.generated_owner,
    value.package_import_target,
    value.runtime_surface_module,
    value.justification,
  ].every(nonEmptyText)
  && entryPointValid(value.generator, '$.relations.generator')
  && entryPointValid(value.build_invocation, '$.relations.build_invocation')
  && entryPointValid(value.input_selector, '$.relations.input_selector')
  && sortedUniqueTexts(value.laws)
  && exactKeys(value.determinism_proof, ['path', 'title', 'what_id'])
  && repositoryPathValid(value.determinism_proof.path, '$.relations.determinism_proof.path')
  && [value.determinism_proof.title, value.determinism_proof.what_id].every(nonEmptyText)

const generatedArtifactValid = (value) => exactKeys(value, [
  'id',
  'artifact_path',
  'artifact_digest',
  'selected_inputs_digest',
  'linkage',
  'javascript_traversal_id',
])
  && [value.id, value.javascript_traversal_id].every(nonEmptyText)
  && repositoryPathValid(value.artifact_path, '$.artifacts.artifact_path')
  && [value.artifact_digest, value.selected_inputs_digest].every(digestValid)
  && linkageValid(value.linkage, '$.artifacts.linkage')

const nonNegativeInteger = (value) => Number.isSafeInteger(value) && value >= 0

const javascriptTraversalValid = (value) => exactKeys(value, [
  'id',
  'source_kind',
  'source_id',
  'ast_node_count',
  'visited_node_count',
  'no_capability_node_count',
  'capability_emitting_node_count',
  'unknown_node_count',
  'ast_node_set_digest',
  'visit_partition_digest',
])
  && [value.id, value.source_id].every(nonEmptyText)
  && ['fable-emit', 'emit-js-expr', 'generated-artifact'].includes(value.source_kind)
  && [
    value.ast_node_count,
    value.visited_node_count,
    value.no_capability_node_count,
    value.capability_emitting_node_count,
    value.unknown_node_count,
  ].every(nonNegativeInteger)
  && value.ast_node_count > 0
  && value.ast_node_count === value.visited_node_count
  && value.visited_node_count === value.no_capability_node_count
    + value.capability_emitting_node_count
    + value.unknown_node_count
  && [value.ast_node_set_digest, value.visit_partition_digest].every(digestValid)

const traversalObservationSetValid = (value) => exactKeys(value, ['traversal_id', 'emitted_observation_ids'])
  && nonEmptyText(value.traversal_id)
  && sortedUniqueTexts(value.emitted_observation_ids)

const semanticKey = ({ consumer_locality: consumerLocality, import_specifier: importSpecifier }) =>
  `${consumerLocality}\0${importSpecifier}`

const actualImportValid = (value) => exactKeys(value, ['consumer_locality', 'import_specifier', 'package_import_target', 'artifact_id', 'imported_members'])
  && [value.consumer_locality, value.import_specifier, value.package_import_target, value.artifact_id].every(nonEmptyText)
  && sortedUniqueTexts(value.imported_members)

const executionLineageValid = (value) => exactKeys(value, ['consumer_locality', 'import_specifier', 'artifact_id', 'entry_reachability'])
  && [value.consumer_locality, value.import_specifier, value.artifact_id].every(nonEmptyText)
  && sortedUniqueEntryReachability(value.entry_reachability, '$.execution_lineage.entry_reachability')

const runtimeSurfaceValid = (value) => exactKeys(value, ['module', 'owner', 'laws', 'exported_members'])
  && [value.module, value.owner].every(nonEmptyText)
  && sortedUniqueTexts(value.laws)
  && sortedUniqueTexts(value.exported_members)

const proofObservationValid = (value) => exactKeys(value, [
  'consumer_locality',
  'import_specifier',
  'owner',
  'path',
  'title',
  'what_id',
  'reached_entries',
  'used_surface_modules',
])
  && [value.consumer_locality, value.import_specifier, value.owner, value.title, value.what_id].every(nonEmptyText)
  && repositoryPathValid(value.path, '$.proof_observations.path')
  && sortedUniqueEntries(value.reached_entries, '$.proof_observations.reached_entries')
  && sortedUniqueTexts(value.used_surface_modules)

const exactCanonical = (left, right) => encodeCanonicalJsonV1(left) === encodeCanonicalJsonV1(right)

const relationEntryReachability = (relation) => [
  { from_entry: relation.build_invocation, to_entry: relation.generator },
  { from_entry: relation.generator, to_entry: relation.input_selector },
].map((entry) => structuredClone(entry)).sort((left, right) => compareCanonicalTextV1(
  entryReachabilityIdentity(left),
  entryReachabilityIdentity(right),
))

const artifactIdFromFact = (fact) => {
  if (fact.observation.case === 'javascript-capability') return fact.observation.payload.generated_artifact_id
  if (fact.observation.case === 'fable-import') return fact.observation.payload.generated_artifact_id
  return null
}

const importFactsFor = (facts, actualImport) => facts.filter(({ observation }) => observation.case === 'fable-import'
  && observation.payload.generated_artifact_id === actualImport.artifact_id
  && observation.payload.module_specifier === actualImport.import_specifier
  && observation.payload.site.locality_id === actualImport.consumer_locality)
export const validateGeneratedModuleRelationV1 = ({
  relations = [],
  artifacts = [],
  actual_imports: actualImports = [],
  traversals = [],
  artifact_bytes_by_path: artifactBytesByPath = new Map(),
  selected_input_rows_by_artifact: selectedInputRowsByArtifact = new Map(),
  capability_facts: capabilityFacts = [],
  traversal_observation_sets: traversalObservationSets = [],
  execution_lineage: executionLineage = [],
  runtime_surfaces: runtimeSurfaces = [],
  proof_observations: proofObservations = [],
}) => {
  const violations = []
  if (![relations, artifacts, actualImports, traversals, capabilityFacts, traversalObservationSets, executionLineage, runtimeSurfaces, proofObservations].every(Array.isArray)
    || !relations.every(generatedRelationValid)
    || !artifacts.every(generatedArtifactValid)
    || !actualImports.every(actualImportValid)
    || !traversals.every(javascriptTraversalValid)
    || !traversalObservationSets.every(traversalObservationSetValid)
    || !executionLineage.every(executionLineageValid)
    || !runtimeSurfaces.every(runtimeSurfaceValid)
    || !proofObservations.every(proofObservationValid)
    || !capabilityFacts.every(validateCanonicalCapabilityFactV1)) {
    return [violation('generated-module-observed-evidence-invalid')]
  }
  const relationsById = countBy(relations, (row) => row.id)
  const artifactsById = countBy(artifacts, (row) => row.id)
  const traversalsById = countBy(traversals, (row) => row.id)
  const traversalObservationSetsById = countBy(traversalObservationSets, (row) => row.traversal_id)
  const capabilityFactsById = countBy(capabilityFacts, (row) => row.fact_id)
  const relationsBySemanticKey = countBy(relations, semanticKey)
  const importsBySemanticKey = countBy(actualImports, semanticKey)
  const lineagesBySemanticKey = countBy(executionLineage, semanticKey)
  const proofsBySemanticKey = countBy(proofObservations, semanticKey)
  const runtimeSurfacesByModule = countBy(runtimeSurfaces, (row) => row.module)

  for (const [relationId, rows] of relationsById) {
    if (rows.length > 1) violations.push(violation('duplicate-generated-module-relation', { relation_id: relationId }))
  }
  for (const [artifactId, rows] of artifactsById) {
    if (rows.length > 1) violations.push(violation('generated-artifact-duplicate', { artifact_id: artifactId }))
  }
  for (const [traversalId, rows] of traversalsById) {
    if (rows.length > 1) violations.push(violation('javascript-traversal-duplicate', { traversal_id: traversalId }))
  }
  for (const [traversalId, rows] of traversalObservationSetsById) {
    if (rows.length > 1) violations.push(violation('javascript-traversal-observation-set-duplicate', { traversal_id: traversalId }))
  }
  for (const [factId, rows] of capabilityFactsById) {
    if (rows.length > 1) violations.push(violation('generated-module-observed-evidence-duplicate', { fact_id: factId }))
  }
  for (const rows of relationsBySemanticKey.values()) {
    if (new Set(rows.map(({ id }) => id)).size > 1) {
      violations.push(violation('duplicate-generated-module-semantic-key', { consumer_locality: rows[0].consumer_locality, import_specifier: rows[0].import_specifier }))
    }
  }
  for (const rows of importsBySemanticKey.values()) {
    if (rows.length > 1) violations.push(violation('generated-module-observed-evidence-duplicate', { consumer_locality: rows[0].consumer_locality, import_specifier: rows[0].import_specifier }))
  }
  for (const rows of lineagesBySemanticKey.values()) {
    if (rows.length > 1) violations.push(violation('generated-module-lineage-duplicate', { consumer_locality: rows[0].consumer_locality, import_specifier: rows[0].import_specifier }))
  }
  for (const rows of proofsBySemanticKey.values()) {
    if (rows.length > 1) violations.push(violation('generated-module-proof-duplicate', { consumer_locality: rows[0].consumer_locality, import_specifier: rows[0].import_specifier }))
  }
  for (const rows of runtimeSurfacesByModule.values()) {
    if (rows.length > 1) violations.push(violation('generated-module-runtime-surface-duplicate', { runtime_surface_module: rows[0].module }))
  }
  for (const traversal of traversals) {
    if (traversal.unknown_node_count > 0) {
      violations.push(violation('javascript-ast-node-unknown', { traversal_id: traversal.id }))
    }
  }
  if (violations.length > 0) return violations

  const relationForConsumer = countBy(relations, (row) => row.consumer_locality)
  for (const actualImport of actualImports) {
    const relation = relationsBySemanticKey.get(semanticKey(actualImport))?.[0]
    if (!relation) {
      const candidates = relationForConsumer.get(actualImport.consumer_locality) ?? []
      if (candidates.length > 0) {
        violations.push(violation('generated-module-specifier-mismatch', { consumer_locality: actualImport.consumer_locality, import_specifier: actualImport.import_specifier }))
        continue
      }
      violations.push(violation('missing-generated-module-relation', { consumer_locality: actualImport.consumer_locality, import_specifier: actualImport.import_specifier }))
      continue
    }
    if (relation.package_import_target !== actualImport.package_import_target) {
      violations.push(violation('generated-module-target-mismatch', { relation_id: relation.id, package_import_target: actualImport.package_import_target }))
    }
  }
  if (violations.length > 0) return violations
  for (const relation of relations) {
    if (!importsBySemanticKey.has(semanticKey(relation))) {
      violations.push(violation('stale-generated-module-relation', { relation_id: relation.id }))
    }
  }
  if (violations.length > 0) return violations

  const importedArtifactIds = new Set(actualImports.map((row) => row.artifact_id))
  for (const actualImport of actualImports) {
    if (!artifactsById.has(actualImport.artifact_id)) violations.push(violation('generated-artifact-missing', { artifact_id: actualImport.artifact_id }))
  }
  for (const artifact of artifacts) {
    if (!importedArtifactIds.has(artifact.id)) violations.push(violation('generated-artifact-stale', { artifact_id: artifact.id }))
  }
  if (violations.length > 0) return violations

  for (const actualImport of actualImports) {
    const importFacts = importFactsFor(capabilityFacts, actualImport)
    if (importFacts.length === 0) {
      violations.push(violation('generated-artifact-reference-missing', { artifact_id: actualImport.artifact_id }))
      continue
    }
    const selectors = [...new Set(importFacts.map(({ observation }) => observation.payload.selector))].sort(compareCanonicalTextV1)
    if (!exactCanonical(selectors, actualImport.imported_members)) {
      violations.push(violation('generated-module-member-mismatch', {
        consumer_locality: actualImport.consumer_locality,
        import_specifier: actualImport.import_specifier,
      }))
    }
  }
  if (violations.length > 0) return violations
  const actualImportsByArtifact = countBy(actualImports, (row) => row.artifact_id)
  for (const fact of capabilityFacts) {
    const artifactId = artifactIdFromFact(fact)
    if (artifactId !== null && !artifactsById.has(artifactId)) {
      violations.push(violation('generated-artifact-reference-stale', { artifact_id: artifactId }))
      continue
    }
    if (fact.observation.case === 'fable-import') {
      const hasObservedImport = (actualImportsByArtifact.get(artifactId) ?? []).some((actualImport) =>
        actualImport.consumer_locality === fact.observation.payload.site.locality_id
        && actualImport.import_specifier === fact.observation.payload.module_specifier)
      if (!hasObservedImport) violations.push(violation('generated-artifact-reference-stale', { artifact_id: artifactId }))
    }
  }
  if (violations.length > 0) return violations

  for (const relation of relations) {
    const key = semanticKey(relation)
    const actualImport = importsBySemanticKey.get(key)[0]
    const rows = lineagesBySemanticKey.get(key) ?? []
    if (rows.length === 0) {
      violations.push(violation('generated-module-lineage-missing', { relation_id: relation.id }))
      continue
    }
    const lineage = rows[0]
    if (lineage.artifact_id !== actualImport.artifact_id
      || !exactCanonical(lineage.entry_reachability, relationEntryReachability(relation))) {
      violations.push(violation('generated-module-lineage-mismatch', { relation_id: relation.id }))
    }
  }
  for (const lineage of executionLineage) {
    if (!relationsBySemanticKey.has(semanticKey(lineage))) {
      violations.push(violation('generated-module-lineage-stale', {
        consumer_locality: lineage.consumer_locality,
        import_specifier: lineage.import_specifier,
      }))
    }
  }
  if (violations.length > 0) return violations

  for (const relation of relations) {
    const key = semanticKey(relation)
    const proofRows = proofsBySemanticKey.get(key) ?? []
    if (proofRows.length === 0) {
      violations.push(violation('generated-module-nondeterministic', { relation_id: relation.id }))
      continue
    }
    const proof = proofRows[0]
    if (proof.owner !== relation.generated_owner) {
      violations.push(violation('generated-module-determinism-proof-owner-mismatch', { relation_id: relation.id }))
      continue
    }
    if (relation.laws.length !== 1 || relation.laws[0] !== relation.determinism_proof.what_id) {
      violations.push(violation('generated-module-determinism-proof-law-mismatch', { relation_id: relation.id }))
      continue
    }
    if (!exactCanonical({ path: proof.path, title: proof.title, what_id: proof.what_id }, relation.determinism_proof)) {
      violations.push(violation('generated-module-determinism-proof-mismatch', { relation_id: relation.id }))
      continue
    }
    const surfaceRows = runtimeSurfacesByModule.get(relation.runtime_surface_module) ?? []
    if (surfaceRows.length === 0) {
      violations.push(violation('generated-module-runtime-surface-missing', { relation_id: relation.id, runtime_surface_module: relation.runtime_surface_module }))
      continue
    }
    const surface = surfaceRows[0]
    if (surface.owner !== relation.generated_owner) {
      violations.push(violation('generated-module-determinism-proof-owner-mismatch', { relation_id: relation.id }))
      continue
    }
    if (!exactCanonical(surface.laws, relation.laws)) {
      violations.push(violation('generated-module-determinism-proof-law-mismatch', { relation_id: relation.id }))
      continue
    }
    const importedMembers = importsBySemanticKey.get(key)[0].imported_members
    if (!importedMembers.every((member) => surface.exported_members.includes(member))) {
      violations.push(violation('generated-module-member-mismatch', { consumer_locality: relation.consumer_locality, import_specifier: relation.import_specifier }))
      continue
    }
    const expectedEntries = [relation.generator]
    if (!exactCanonical(proof.reached_entries, expectedEntries)
      || !exactCanonical(proof.used_surface_modules, [relation.runtime_surface_module])) {
      violations.push(violation('generated-module-runtime-surface-callback-mismatch', { relation_id: relation.id }))
    }
  }
  for (const proof of proofObservations) {
    if (!relationsBySemanticKey.has(semanticKey(proof))) {
      violations.push(violation('generated-module-proof-stale', {
        consumer_locality: proof.consumer_locality,
        import_specifier: proof.import_specifier,
      }))
    }
  }
  const relationSurfaceModules = new Set(relations.map(({ runtime_surface_module: module }) => module))
  for (const surface of runtimeSurfaces) {
    if (!relationSurfaceModules.has(surface.module)) {
      violations.push(violation('generated-module-runtime-surface-stale', { runtime_surface_module: surface.module }))
    }
  }
  if (violations.length > 0) return violations
  const referencedTraversalIds = new Set(artifacts.map(({ javascript_traversal_id: traversalId }) => traversalId))
  for (const traversal of traversals) {
    if (traversal.source_kind === 'generated-artifact' && !referencedTraversalIds.has(traversal.id)) {
      violations.push(violation('javascript-traversal-stale', { traversal_id: traversal.id }))
    }
  }
  if (violations.length > 0) return violations

  const relationByImport = new Map(relations.map((row) => [`${row.consumer_locality}\0${row.import_specifier}`, row]))
  for (const actualImport of actualImports) {
    const relation = relationByImport.get(`${actualImport.consumer_locality}\0${actualImport.import_specifier}`)
    const artifact = artifactsById.get(actualImport.artifact_id)[0]
    const expectedLinkage = relationLinkage(relation)
    const expectedId = generatedArtifactIdV1({ artifact_path: artifact.artifact_path, linkage: expectedLinkage })
    if (encodeCanonicalJsonV1(artifact.linkage) !== encodeCanonicalJsonV1(expectedLinkage) || artifact.id !== expectedId) {
      violations.push(violation('generated-artifact-linkage-mismatch', { artifact_id: artifact.id, relation_id: relation.id }))
      continue
    }
    const outputBytes = bytesAt(artifactBytesByPath, artifact.artifact_path)
    if (outputBytes === undefined || sha256BytesV1(bytesOf(outputBytes, artifact.artifact_path)) !== artifact.artifact_digest) {
      violations.push(violation('generated-artifact-digest-mismatch', { artifact_id: artifact.id }))
    }
    const selectedRows = bytesAt(selectedInputRowsByArtifact, artifact.id)
    if (!Array.isArray(selectedRows) || selectedInputsDigestV1(selectedRows) !== artifact.selected_inputs_digest) {
      violations.push(violation('generated-artifact-inputs-digest-mismatch', { artifact_id: artifact.id }))
    }
    const traversalRows = traversalsById.get(artifact.javascript_traversal_id) ?? []
    if (traversalRows.length === 0) violations.push(violation('javascript-traversal-missing', { artifact_id: artifact.id, traversal_id: artifact.javascript_traversal_id }))
    else if (artifact.javascript_traversal_id !== javascriptTraversalIdV1('generated-artifact', artifact.id)
      || traversalRows[0].id !== javascriptTraversalIdV1(traversalRows[0].source_kind, traversalRows[0].source_id)
      || traversalRows[0].source_kind !== 'generated-artifact'
      || traversalRows[0].source_id !== artifact.id) {
      violations.push(violation('javascript-traversal-source-mismatch', { artifact_id: artifact.id, traversal_id: artifact.javascript_traversal_id }))
    }
  }
  if (violations.length > 0) return violations

  const generatedTraversalIds = new Set(artifacts.map(({ javascript_traversal_id: traversalId }) => traversalId))
  for (const traversalId of generatedTraversalIds) {
    if (!traversalObservationSetsById.has(traversalId)) {
      violations.push(violation('javascript-traversal-observation-set-missing', { traversal_id: traversalId }))
    }
  }
  for (const row of traversalObservationSets) {
    if (!generatedTraversalIds.has(row.traversal_id)) {
      violations.push(violation('javascript-traversal-observation-set-stale', { traversal_id: row.traversal_id }))
    }
  }
  if (violations.length > 0) return violations

  for (const artifact of artifacts) {
    const traversal = traversalsById.get(artifact.javascript_traversal_id)[0]
    const emittedObservationIds = traversalObservationSetsById.get(traversal.id)[0].emitted_observation_ids
    const factObservationIds = capabilityFacts
      .filter(({ observation }) => observation.case === 'javascript-capability'
        && observation.payload.source_kind === 'generated-artifact'
        && observation.payload.source_id === artifact.id
        && observation.payload.generated_artifact_id === artifact.id)
      .map(({ observation_id: observationId }) => observationId)
      .sort(compareCanonicalTextV1)
    if ((traversal.capability_emitting_node_count === 0) !== (emittedObservationIds.length === 0)
      || !exactCanonical(emittedObservationIds, factObservationIds)) {
      violations.push(violation('javascript-traversal-source-mismatch', { artifact_id: artifact.id, traversal_id: traversal.id }))
    }
  }
  if (violations.length > 0) return violations

  for (const relation of relations) {
    const actualImport = importsBySemanticKey.get(semanticKey(relation))[0]
    if (capabilityFacts.some((fact) => artifactIdFromFact(fact) === actualImport.artifact_id && capabilityDispositionViolatesContractV1(fact.disposition))) {
      const artifact = artifactsById.get(actualImport.artifact_id)[0]
      violations.push(violation('generated-module-physical-authority', { artifact_id: artifact.id, relation_id: relation.id }))
    }
  }
  return violations.sort((left, right) => compareCanonicalTextV1(`${left.code}\0${encodeCanonicalJsonV1(left)}`, `${right.code}\0${encodeCanonicalJsonV1(right)}`))
}
