import assert from 'node:assert/strict'
import test from 'node:test'

import {
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
  const closure = resolveCutoverInputClosureV1(legalClosureInput())
  assert.deepEqual(closure.violations, [])
  const indexEntries = legalIndexEntries()
  const indexBlobBytes = bytesByPath(indexEntries)
  return {
    closure,
    index_entries: indexEntries,
    object_format: 'sha1',
    index_blob_bytes_by_path: indexBlobBytes,
    working_tree_bytes_by_path: new Map(indexBlobBytes),
    excluded_paths: [
      'docs/OWNER-CONTRACT-SLICE-ADJUDICATION-WORKSHEET.json',
      'docs/OWNER-CONTRACT-SLICE-ADJUDICATIONS.json',
    ],
    build_output_paths: ['dist/Generated.js'],
  }
}

test('WHAT[STRUCTURED-WORKFLOW-016] cutover closure and stage index reject every competing input world', () => {
  const state = legalState()
  const validated = validateCutoverInputStateV1(state)
  assert.deepEqual(validated.violations, [])
  assert.match(canonicalInputIndexDigestV1(validated.index_rows), /^sha256:[0-9a-f]{64}$/)
  assert.ok(!validated.index_rows.some(({ path }) => state.excluded_paths.includes(path)))

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

  const missingStage = legalState()
  missingStage.index_entries = missingStage.index_entries.filter(({ path }) => path !== 'src/A.fs')
  assert.deepEqual(validateCutoverInputStateV1(missingStage).violations, [{ code: 'cutover-input-closure-incomplete', path: 'src/A.fs', reason: 'missing-stage-zero-entry' }])

  const unmerged = legalState()
  unmerged.index_entries.find(({ path }) => path === 'src/A.fs').stage = 2
  assert.deepEqual(validateCutoverInputStateV1(unmerged).violations, [{ code: 'cutover-input-closure-incomplete', path: 'src/A.fs', reason: 'missing-stage-zero-entry' }])

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
