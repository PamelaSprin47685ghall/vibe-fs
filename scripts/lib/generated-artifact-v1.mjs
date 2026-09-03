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

export const validateGeneratedModuleRelationV1 = ({
  relations = [],
  artifacts = [],
  actual_imports: actualImports = [],
  traversals = [],
  artifact_references: artifactReferences = [],
  artifact_bytes_by_path: artifactBytesByPath = new Map(),
  selected_input_rows_by_artifact: selectedInputRowsByArtifact = new Map(),
  capability_facts: capabilityFacts = [],
  deterministic_relation_ids: deterministicRelationIds = [],
}) => {
  const violations = []
  const relationsById = countBy(relations, (row) => row.id)
  const artifactsById = countBy(artifacts, (row) => row.id)
  const traversalsById = countBy(traversals, (row) => row.id)

  for (const [relationId, rows] of relationsById) {
    if (rows.length > 1) violations.push(violation('duplicate-generated-module-relation', { relation_id: relationId }))
  }
  for (const [artifactId, rows] of artifactsById) {
    if (rows.length > 1) violations.push(violation('generated-artifact-duplicate', { artifact_id: artifactId }))
  }
  for (const [traversalId, rows] of traversalsById) {
    if (rows.length > 1) violations.push(violation('javascript-traversal-duplicate', { traversal_id: traversalId }))
  }
  if (violations.length > 0) return violations

  const relationForConsumer = countBy(relations, (row) => row.consumer_locality)
  const importsForConsumer = countBy(actualImports, (row) => row.consumer_locality)
  for (const actualImport of actualImports) {
    const candidates = relationForConsumer.get(actualImport.consumer_locality) ?? []
    if (candidates.length === 0) {
      violations.push(violation('missing-generated-module-relation', { consumer_locality: actualImport.consumer_locality, import_specifier: actualImport.import_specifier }))
      continue
    }
    const specifier = candidates.find((row) => row.import_specifier === actualImport.import_specifier)
    if (!specifier) {
      violations.push(violation('generated-module-specifier-mismatch', { consumer_locality: actualImport.consumer_locality, import_specifier: actualImport.import_specifier }))
      continue
    }
    if (specifier.package_import_target !== actualImport.package_import_target) {
      violations.push(violation('generated-module-target-mismatch', { relation_id: specifier.id, package_import_target: actualImport.package_import_target }))
    }
  }
  for (const relation of relations) {
    const consumerImports = importsForConsumer.get(relation.consumer_locality) ?? []
    if (consumerImports.length === 0) {
      violations.push(violation('stale-generated-module-relation', { relation_id: relation.id }))
    }
    if (!deterministicRelationIds.includes(relation.id)) violations.push(violation('generated-module-nondeterministic', { relation_id: relation.id }))
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
    else if (traversalRows[0].source_kind !== 'generated-artifact' || traversalRows[0].source_id !== artifact.id) {
      violations.push(violation('javascript-traversal-source-mismatch', { artifact_id: artifact.id, traversal_id: artifact.javascript_traversal_id }))
    }
    if (!artifactReferences.some((reference) => reference.artifact_id === artifact.id)) {
      violations.push(violation('generated-artifact-reference-missing', { artifact_id: artifact.id }))
    }
    if (capabilityFacts.some((fact) => fact.artifact_id === artifact.id && capabilityDispositionViolatesContractV1(fact.disposition))) {
      violations.push(violation('generated-module-physical-authority', { artifact_id: artifact.id, relation_id: relation.id }))
    }
  }
  return violations.sort((left, right) => compareCanonicalTextV1(`${left.code}\0${encodeCanonicalJsonV1(left)}`, `${right.code}\0${encodeCanonicalJsonV1(right)}`))
}
