import {
  assertRepositoryPathV1,
  canonicalDigestV1,
  compareCanonicalTextV1,
} from './canonical-json-v1.mjs'

const violation = (path, reason, code = 'cutover-input-closure-incomplete') => ({ code, path, reason })

const sortedViolations = (violations) => violations.sort((left, right) =>
  compareCanonicalTextV1(`${left.path}\0${left.reason}`, `${right.path}\0${right.reason}`))

const canonicalPath = (value, reason, violations) => {
  try {
    return assertRepositoryPathV1(value, '$.cutover_path')
  } catch {
    violations.push(violation(String(value), reason))
    return null
  }
}

const valuesOfMap = (map, key) => map instanceof Map ? map.get(key) : map?.[key]

const uniquePaths = (values, duplicateReason, violations) => {
  const paths = []
  const seen = new Set()
  for (const value of values ?? []) {
    const path = canonicalPath(value, 'invalid-repository-path', violations)
    if (path === null) continue
    if (seen.has(path)) violations.push(violation(path, duplicateReason))
    else {
      seen.add(path)
      paths.push(path)
    }
  }
  return paths.sort(compareCanonicalTextV1)
}

export const resolveCutoverInputClosureV1 = ({
  entry_paths: entryPaths,
  imports_by_path: importsByPath,
  selector_outputs_by_entry: selectorOutputsByEntry,
  tracked_read_paths: trackedReadPaths,
  build_output_paths: buildOutputPaths,
}) => {
  const violations = []
  const entries = uniquePaths(entryPaths, 'duplicate-entry-path', violations)
  const outputs = uniquePaths(buildOutputPaths, 'duplicate-build-output-path', violations)
  const outputSet = new Set(outputs)
  const closure = new Set(entries)
  const pending = [...entries]

  while (pending.length > 0) {
    const importer = pending.shift()
    for (const imported of valuesOfMap(importsByPath, importer) ?? []) {
      const importedPath = canonicalPath(imported?.path, 'invalid-import-path', violations)
      if (importedPath === null) continue
      if (imported?.kind === 'dynamic-local') {
        violations.push(violation(importedPath, 'dynamic-local-import'))
        continue
      }
      if (imported?.kind !== 'static-local') {
        violations.push(violation(importedPath, 'unknown-import-kind'))
        continue
      }
      if (!closure.has(importedPath)) {
        closure.add(importedPath)
        pending.push(importedPath)
      }
    }
  }

  const selectorEntries = selectorOutputsByEntry instanceof Map
    ? [...selectorOutputsByEntry.entries()]
    : Object.entries(selectorOutputsByEntry ?? {})
  for (const [selector, selected] of selectorEntries) {
    const selectedPaths = uniquePaths(selected, 'duplicate-selector-output', violations)
    for (const selectedPath of selectedPaths) closure.add(selectedPath)
    const selectorPath = String(selector).split('#')[0]
    const canonicalSelectorPath = canonicalPath(selectorPath, 'invalid-selector-entry', violations)
    if (canonicalSelectorPath !== null && !closure.has(canonicalSelectorPath)) violations.push(violation(canonicalSelectorPath, 'selector-entry-outside-closure'))
  }

  for (const readPath of uniquePaths(trackedReadPaths, 'duplicate-tracked-read', violations)) {
    if (!closure.has(readPath) && !outputSet.has(readPath)) violations.push(violation(readPath, 'unclosed-read'))
  }

  return {
    paths: [...closure].sort(compareCanonicalTextV1),
    build_output_paths: outputs,
    violations: sortedViolations(violations),
  }
}

const bytesAt = (map, key) => map instanceof Map ? map.get(key) : map?.[key]

const sameBytes = (left, right) => {
  if (!(Buffer.isBuffer(left) || left instanceof Uint8Array) || !(Buffer.isBuffer(right) || right instanceof Uint8Array)) return false
  return Buffer.from(left).equals(Buffer.from(right))
}

export const canonicalInputIndexDigestV1 = (rows) => canonicalDigestV1('cutover-input-index/v1\0', rows)

export const validateCutoverInputStateV1 = ({
  closure,
  index_entries: indexEntries,
  object_format: objectFormat,
  index_blob_bytes_by_path: indexBlobBytesByPath,
  working_tree_bytes_by_path: workingTreeBytesByPath,
  excluded_paths: excludedPaths,
  build_output_paths: buildOutputPaths,
}) => {
  if (closure?.violations?.length > 0) return { index_rows: [], violations: closure.violations }
  const violations = []
  const exclusions = new Set(uniquePaths(excludedPaths, 'duplicate-excluded-path', violations))
  const outputs = new Set(uniquePaths(buildOutputPaths, 'duplicate-build-output-path', violations))
  if (!['sha1', 'sha256'].includes(objectFormat)) violations.push(violation('$object-format', 'unsupported-object-format'))
  const oidPattern = objectFormat === 'sha256' ? /^[0-9a-f]{64}$/ : /^[0-9a-f]{40}$/
  const stageZero = new Map()
  for (const entry of indexEntries ?? []) {
    const entryPath = canonicalPath(entry?.path, 'invalid-index-path', violations)
    if (entryPath === null || entry?.stage !== 0) continue
    if (stageZero.has(entryPath)) violations.push(violation(entryPath, 'duplicate-stage-zero-entry'))
    else stageZero.set(entryPath, entry)
  }

  for (const closurePath of closure?.paths ?? []) {
    if (outputs.has(closurePath)) continue
    const entry = stageZero.get(closurePath)
    if (!entry) {
      violations.push(violation(closurePath, 'missing-stage-zero-entry'))
      continue
    }
    if (!['100644', '100755'].includes(entry.mode) || entry.object_type !== 'blob') {
      violations.push(violation(closurePath, 'non-regular-closure-input'))
      continue
    }
    if (!oidPattern.test(entry.blob_oid)) {
      violations.push(violation(closurePath, 'invalid-object-id'))
      continue
    }
    if (!sameBytes(bytesAt(indexBlobBytesByPath, closurePath), bytesAt(workingTreeBytesByPath, closurePath))) {
      violations.push(violation(closurePath, 'working-tree-index-mismatch'))
    }
  }
  if (violations.length > 0) return { index_rows: [], violations: sortedViolations(violations) }

  const indexRows = []
  for (const [entryPath, entry] of [...stageZero].sort(([left], [right]) => compareCanonicalTextV1(left, right))) {
    if (exclusions.has(entryPath)) continue
    if (!['100644', '100755', '120000'].includes(entry.mode) || entry.object_type !== 'blob' || !oidPattern.test(entry.blob_oid)) {
      violations.push(violation(entryPath, 'invalid-stage-zero-entry'))
      continue
    }
    if (!sameBytes(bytesAt(indexBlobBytesByPath, entryPath), bytesAt(workingTreeBytesByPath, entryPath))) {
      violations.push(violation(entryPath, 'working-tree-index-mismatch'))
      continue
    }
    indexRows.push({ path: entryPath, mode: entry.mode, blob_oid: entry.blob_oid })
  }
  return { index_rows: indexRows, violations: sortedViolations(violations) }
}

const exactKeys = (value, keys) => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) return false
  const actual = Object.keys(value).sort(compareCanonicalTextV1)
  const expected = [...keys].sort(compareCanonicalTextV1)
  return actual.length === expected.length && actual.every((key, index) => key === expected[index])
}

const terminalClassification = (value) => exactKeys(value, ['case', 'payload'])
  && ['private', 'contract-shared', 'contract-bounded', 'runtime-effect', 'adapter-effect', 'composition-terminal'].includes(value.case)
  && exactKeys(value.payload, [])

const reviewTextValid = (value) => typeof value === 'string'
  && value.trim().length > 0
  && !/[\u0000-\u001f\u007f]/.test(value)

const canonicalTexts = (values) => Array.isArray(values)
  && values.every((value) => typeof value === 'string' && value.length > 0)
  && values.every((value, index) => index === 0 || compareCanonicalTextV1(values[index - 1], value) < 0)

const proofIdentity = ({ what_id: whatId, path, title }) => `${whatId}\0${path}\0${title}`

const proofValid = (value) => {
  if (!exactKeys(value, ['what_id', 'path', 'title'])
    || ![value.what_id, value.title].every(reviewTextValid)) return false
  try {
    assertRepositoryPathV1(value.path, '$.worksheet.proof.path')
    return true
  } catch {
    return false
  }
}

const canonicalProofs = (proofs) => Array.isArray(proofs)
  && proofs.every(proofValid)
  && proofs.every((proof, index) => index === 0 || compareCanonicalTextV1(proofIdentity(proofs[index - 1]), proofIdentity(proof)) < 0)

export const validateMigrationWorksheetV1 = (worksheet) => {
  const schemaViolation = (path) => [{ code: 'migration-worksheet-schema', path, reason: 'unknown-or-missing-key' }]
  if (!exactKeys(worksheet, ['schema_version', 'purpose', 'records']) || worksheet.schema_version !== 1 || worksheet.purpose !== 'm6.3b-migration-only' || !Array.isArray(worksheet.records)) return schemaViolation('$')
  const seen = new Set()
  for (let index = 0; index < worksheet.records.length; index += 1) {
    const path = `$.records[${index}]`
    const record = worksheet.records[index]
    if (!exactKeys(record, ['locality_id', 'status', 'draft_reason', 'draft_target_classification', 'draft_migration_path', 'draft_what_ids', 'draft_proofs'])) return schemaViolation(path)
    if (typeof record.locality_id !== 'string' || record.locality_id.length === 0 || seen.has(record.locality_id)) return schemaViolation(`${path}.locality_id`)
    seen.add(record.locality_id)
    if (!['undecided', 'decided'].includes(record.status) || !Array.isArray(record.draft_what_ids) || !Array.isArray(record.draft_proofs)) return schemaViolation(path)
    if (record.status === 'undecided') {
      if (record.draft_reason !== null || record.draft_target_classification !== null || record.draft_migration_path !== null || record.draft_what_ids.length > 0 || record.draft_proofs.length > 0) return schemaViolation(path)
    } else if (
      !reviewTextValid(record.draft_reason)
      || !reviewTextValid(record.draft_migration_path)
      || !terminalClassification(record.draft_target_classification)
      || record.draft_what_ids.length === 0 || !canonicalTexts(record.draft_what_ids)
      || record.draft_proofs.length === 0 || !canonicalProofs(record.draft_proofs)
      || record.draft_what_ids.some((whatId) => !record.draft_proofs.some(({ what_id: proofWhatId }) => proofWhatId === whatId))
      || record.draft_proofs.some(({ what_id: whatId }) => !record.draft_what_ids.includes(whatId))
    ) return schemaViolation(path)
  }
  const sorted = [...seen].sort(compareCanonicalTextV1)
  if (worksheet.records.some((record, index) => record.locality_id !== sorted[index])) return schemaViolation('$.records')
  return []
}
