import assert from 'node:assert/strict'
import test from 'node:test'

import {
  buildFreshMigrationWorksheetV1,
  canonicalInputIndexDigestV1,
  resolveCutoverInputClosureV1,
  validateCutoverInputStateV1,
  validateMigrationWorksheetV1,
} from '../../../scripts/lib/cutover-inputs-v1.mjs'

const oid = (digit) => digit.repeat(40)

const legalClosureInput = () => ({
  entry_paths: ['scripts/build.mjs', 'package.json'],
  imports_by_path: new Map([
    ['scripts/build.mjs', [
      { path: 'scripts/generate.mjs', kind: 'static-local' },
      { path: 'scripts/select.mjs', kind: 'static-local' },
    ]],
    ['scripts/generate.mjs', []],
    ['scripts/select.mjs', []],
    ['package.json', []],
  ]),
  selector_outputs_by_entry: new Map([
    ['scripts/select.mjs#inputs', ['src/A.fs', 'requirements/a/WHAT.md']],
  ]),
  tracked_read_paths: [
    'scripts/build.mjs',
    'scripts/generate.mjs',
    'scripts/select.mjs',
    'package.json',
    'src/A.fs',
    'requirements/a/WHAT.md',
  ],
  build_output_paths: ['dist/Generated.js'],
})

const legalIndexEntries = () => [
  ['package.json', '1'],
  ['requirements/a/WHAT.md', '2'],
  ['scripts/build.mjs', '3'],
  ['scripts/generate.mjs', '4'],
  ['scripts/select.mjs', '5'],
  ['src/A.fs', '6'],
  ['docs/OWNER-CONTRACT-SLICE-ADJUDICATION-WORKSHEET.json', '7'],
  ['docs/OWNER-CONTRACT-SLICE-ADJUDICATIONS.json', '8'],
].map(([path, digit]) => ({ path, mode: '100644', stage: 0, blob_oid: oid(digit), object_type: 'blob' }))

const bytesByPath = (entries) => new Map(entries.map(({ path }) => [path, Buffer.from(`bytes:${path}`)]))

const legalState = () => {
  const indexEntries = legalIndexEntries()
  const indexBlobBytes = bytesByPath(indexEntries)
  return {
    closure_input: legalClosureInput(),
    index_entries: indexEntries,
    object_format: 'sha1',
    index_blob_bytes_by_path: indexBlobBytes,
    working_tree_bytes_by_path: new Map(indexBlobBytes),
  }
}

test('WHAT[STRUCTURED-WORKFLOW-016] cutover closure and stage index reject every competing input world', () => {
  const state = legalState()
  const validated = validateCutoverInputStateV1(state)
  assert.deepEqual(validated.violations, [])
  assert.match(canonicalInputIndexDigestV1(validated.index_rows), /^sha256:[0-9a-f]{64}$/)
  assert.ok(!validated.index_rows.some(({ path }) => [
    'docs/OWNER-CONTRACT-SLICE-ADJUDICATION-WORKSHEET.json',
    'docs/OWNER-CONTRACT-SLICE-ADJUDICATIONS.json',
  ].includes(path)))

  const forgedClosure = legalState()
  forgedClosure.closure = { paths: [], build_output_paths: [], violations: [] }
  assert.deepEqual(validateCutoverInputStateV1(forgedClosure).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: '$',
    reason: 'invalid-cutover-state-schema',
  }])

  const secondBuildOutputAuthority = legalState()
  secondBuildOutputAuthority.build_output_paths = []
  assert.deepEqual(validateCutoverInputStateV1(secondBuildOutputAuthority).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: '$',
    reason: 'invalid-cutover-state-schema',
  }])

  const arbitraryExclusion = legalState()
  arbitraryExclusion.excluded_paths = ['src/A.fs']
  assert.deepEqual(validateCutoverInputStateV1(arbitraryExclusion).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: '$',
    reason: 'invalid-cutover-state-schema',
  }])

  const unread = legalClosureInput()
  unread.tracked_read_paths.push('secrets/ambient.txt')
  assert.deepEqual(resolveCutoverInputClosureV1(unread).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: 'secrets/ambient.txt',
    reason: 'unclosed-read',
  }])

  const dynamic = legalClosureInput()
  dynamic.imports_by_path.get('scripts/build.mjs').push({ path: 'scripts/dynamic.mjs', kind: 'dynamic-local' })
  assert.deepEqual(resolveCutoverInputClosureV1(dynamic).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: 'scripts/dynamic.mjs',
    reason: 'dynamic-local-import',
  }])

  const missingScan = legalClosureInput()
  missingScan.imports_by_path.delete('scripts/generate.mjs')
  assert.deepEqual(resolveCutoverInputClosureV1(missingScan).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: 'scripts/generate.mjs',
    reason: 'missing-import-scan-row',
  }])

  const invalidScan = legalClosureInput()
  invalidScan.imports_by_path.set('scripts/generate.mjs', 'scripts/ambient.mjs')
  assert.deepEqual(resolveCutoverInputClosureV1(invalidScan).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: 'scripts/generate.mjs',
    reason: 'invalid-import-scan-row',
  }])

  const unusedInvalidScan = legalClosureInput()
  unusedInvalidScan.imports_by_path.set('scripts/unused.mjs', [{ path: 'src/A.fs', kind: 'static-local', extra: true }])
  assert.deepEqual(resolveCutoverInputClosureV1(unusedInvalidScan).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: 'scripts/unused.mjs',
    reason: 'invalid-import-scan-row',
  }])

  const invalidSelectorOutput = legalClosureInput()
  invalidSelectorOutput.selector_outputs_by_entry.set('scripts/select.mjs#inputs', 'src/A.fs')
  assert.deepEqual(resolveCutoverInputClosureV1(invalidSelectorOutput).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: 'scripts/select.mjs#inputs',
    reason: 'invalid-selector-output',
  }])

  const invalidSelectorEntry = legalClosureInput()
  invalidSelectorEntry.selector_outputs_by_entry = new Map([['scripts/select.mjs', ['src/A.fs']]])
  assert.deepEqual(resolveCutoverInputClosureV1(invalidSelectorEntry).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: 'scripts/select.mjs',
    reason: 'invalid-selector-entry',
  }])

  const selectedBuildOutput = legalClosureInput()
  selectedBuildOutput.selector_outputs_by_entry.set('scripts/select.mjs#inputs', ['src/A.fs', 'dist/Generated.js'])
  assert.deepEqual(resolveCutoverInputClosureV1(selectedBuildOutput).violations, [{
    code: 'cutover-input-closure-incomplete',
    path: 'dist/Generated.js',
    reason: 'selected-input-build-output-overlap',
  }])

  for (const [field, value, path, reason] of [
    ['entry_paths', null, '$.entry_paths', 'invalid-entry-paths'],
    ['imports_by_path', {}, '$.imports_by_path', 'invalid-import-scan-map'],
    ['selector_outputs_by_entry', [], '$.selector_outputs_by_entry', 'invalid-selector-output-map'],
    ['tracked_read_paths', 'src/A.fs', '$.tracked_read_paths', 'invalid-tracked-read-paths'],
    ['build_output_paths', {}, '$.build_output_paths', 'invalid-build-output-paths'],
  ]) {
    const malformed = legalClosureInput()
    malformed[field] = value
    assert.deepEqual(resolveCutoverInputClosureV1(malformed).violations, [{
      code: 'cutover-input-closure-incomplete',
      path,
      reason,
    }])
  }

  const missingStage = legalState()
  missingStage.index_entries = missingStage.index_entries.filter(({ path }) => path !== 'src/A.fs')
  missingStage.index_blob_bytes_by_path.delete('src/A.fs')
  missingStage.working_tree_bytes_by_path.delete('src/A.fs')
  assert.deepEqual(validateCutoverInputStateV1(missingStage).violations, [{ code: 'cutover-input-closure-incomplete', path: 'src/A.fs', reason: 'missing-stage-zero-entry' }])

  const unmerged = legalState()
  unmerged.index_entries.find(({ path }) => path === 'src/A.fs').stage = 2
  assert.deepEqual(validateCutoverInputStateV1(unmerged).violations, [{ code: 'cutover-input-closure-incomplete', path: 'src/A.fs', reason: 'unmerged-index-entry' }])

  const unrelatedUnmerged = legalState()
  unrelatedUnmerged.index_entries.push({ path: 'scratch.txt', mode: '100644', stage: 3, blob_oid: oid('9'), object_type: 'blob' })
  assert.deepEqual(validateCutoverInputStateV1(unrelatedUnmerged).violations, [{ code: 'cutover-input-closure-incomplete', path: 'scratch.txt', reason: 'unmerged-index-entry' }])

  const malformedIndexCollection = legalState()
  malformedIndexCollection.index_entries = null
  assert.deepEqual(validateCutoverInputStateV1(malformedIndexCollection).violations, [{ code: 'cutover-input-closure-incomplete', path: '$.index_entries', reason: 'invalid-index-entries' }])

  const openIndexRow = legalState()
  openIndexRow.index_entries[0].extra = true
  assert.deepEqual(validateCutoverInputStateV1(openIndexRow).violations, [{ code: 'cutover-input-closure-incomplete', path: '$.index_entries[0]', reason: 'invalid-index-entry-schema' }])

  const invalidObjectFormat = legalState()
  invalidObjectFormat.object_format = 'sha512'
  invalidObjectFormat.index_entries[0].blob_oid = 'not-an-oid'
  assert.deepEqual(validateCutoverInputStateV1(invalidObjectFormat).violations, [{ code: 'cutover-input-closure-incomplete', path: '$object-format', reason: 'unsupported-object-format' }])

  const arbitraryByteRow = legalState()
  arbitraryByteRow.index_blob_bytes_by_path.set('src/not-indexed.fs', Buffer.from('ambient'))
  assert.deepEqual(validateCutoverInputStateV1(arbitraryByteRow).violations, [{ code: 'cutover-input-closure-incomplete', path: 'src/not-indexed.fs', reason: 'unexpected-index-blob-bytes' }])

  const symlink = legalState()
  symlink.index_entries.find(({ path }) => path === 'docs/OWNER-CONTRACT-SLICE-ADJUDICATIONS.json').mode = '120000'
  assert.deepEqual(validateCutoverInputStateV1(symlink).violations, [{ code: 'cutover-input-closure-incomplete', path: 'docs/OWNER-CONTRACT-SLICE-ADJUDICATIONS.json', reason: 'invalid-stage-zero-entry' }])
  assert.throws(() => canonicalInputIndexDigestV1([{ path: 'link', mode: '120000', blob_oid: oid('a') }]), TypeError)

  const unstaged = legalState()
  unstaged.working_tree_bytes_by_path.set('src/A.fs', Buffer.from('different'))
  assert.deepEqual(validateCutoverInputStateV1(unstaged).violations, [{ code: 'cutover-input-closure-incomplete', path: 'src/A.fs', reason: 'working-tree-index-mismatch' }])

  const gitlink = legalState()
  gitlink.index_entries.find(({ path }) => path === 'src/A.fs').mode = '160000'
  assert.deepEqual(validateCutoverInputStateV1(gitlink).violations, [{ code: 'cutover-input-closure-incomplete', path: 'src/A.fs', reason: 'non-regular-closure-input' }])

  assert.deepEqual(validateMigrationWorksheetV1({
    schema_version: 1,
    purpose: 'm6.3b-migration-only',
    records: [{
      locality_id: 'locality-a',
      status: 'undecided',
      draft_reason: null,
      draft_target_classification: null,
      draft_migration_path: null,
      draft_what_ids: [],
      draft_proofs: [],
    }],
  }), [])
  const freshWorksheet = buildFreshMigrationWorksheetV1([
    { locality_id: 'locality-b', reasons: ['TerminalClassificationRequired'] },
    { locality_id: 'locality-a', reasons: ['ReferencedProvider', 'TerminalClassificationRequired'] },
  ])
  assert.deepEqual(freshWorksheet.records.map(({ locality_id: localityId }) => localityId), ['locality-a', 'locality-b'])
  assert.ok(freshWorksheet.records.every((record) => record.status === 'undecided'
    && record.draft_reason === null
    && record.draft_target_classification === null
    && record.draft_migration_path === null
    && record.draft_what_ids.length === 0
    && record.draft_proofs.length === 0))
  assert.deepEqual(validateMigrationWorksheetV1(freshWorksheet), [])
  assert.throws(() => buildFreshMigrationWorksheetV1([
    { locality_id: 'locality-a', reasons: ['TerminalClassificationRequired'] },
    { locality_id: 'locality-a', reasons: ['TerminalClassificationRequired'] },
  ]), /duplicate migration worksheet locality/)
  assert.throws(() => buildFreshMigrationWorksheetV1([
    { locality_id: 'locality-a', reasons: [] },
  ]), /migration worksheet candidate 0 is invalid/)
  const decidedWorksheet = {
    schema_version: 1,
    purpose: 'm6.3b-migration-only',
    records: [{
      locality_id: 'locality-a',
      status: 'decided',
      draft_reason: 'The complete signed surface has one audience.',
      draft_target_classification: { case: 'contract-bounded', payload: {} },
      draft_migration_path: 'Publish one bounded contract slice.',
      draft_what_ids: ['OWNER-001'],
      draft_proofs: [{
        what_id: 'OWNER-001',
        path: 'requirements/owner/tests/owner.test.mjs',
        title: 'WHAT[OWNER-001] owner law',
      }],
    }],
  }
  assert.deepEqual(validateMigrationWorksheetV1(decidedWorksheet), [])
  decidedWorksheet.records[0].draft_proofs.push(structuredClone(decidedWorksheet.records[0].draft_proofs[0]))
  assert.deepEqual(validateMigrationWorksheetV1(decidedWorksheet), [{
    code: 'migration-worksheet-schema',
    path: '$.records[0]',
    reason: 'unknown-or-missing-key',
  }])
  assert.deepEqual(validateMigrationWorksheetV1({
    schema_version: 1,
    purpose: 'm6.3b-migration-only',
    canonical_world_digest: `sha256:${'0'.repeat(64)}`,
    records: [],
  }), [{ code: 'migration-worksheet-schema', path: '$', reason: 'unknown-or-missing-key' }])
})
