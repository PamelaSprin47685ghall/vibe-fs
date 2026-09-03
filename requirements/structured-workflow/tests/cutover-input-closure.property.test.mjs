import assert from 'node:assert/strict'
import test from 'node:test'
import fc from 'fast-check'

import {
  resolveCutoverInputClosureV1,
  validateCutoverInputStateV1,
} from '../../../scripts/lib/cutover-inputs-v1.mjs'

const SEED = 0x4355544f

test('WHAT[STRUCTURED-WORKFLOW-016] one staged input mutation yields the exact closure or index violation', () => {
  fc.assert(fc.property(
    fc.uniqueArray(fc.stringMatching(/^[a-z][a-z0-9-]{0,8}\.fs$/), { minLength: 1, maxLength: 20 }),
    fc.integer({ min: 0, max: 19 }),
    (names, rawIndex) => {
      const paths = names.map((name) => `src/${name}`).sort()
      const closure = resolveCutoverInputClosureV1({
        entry_paths: paths,
        imports_by_path: new Map(paths.map((path) => [path, []])),
        selector_outputs_by_entry: new Map(),
        tracked_read_paths: paths,
        build_output_paths: [],
      })
      assert.deepEqual(closure.violations, [])
      const indexEntries = paths.map((path, index) => ({
        path,
        mode: '100644',
        stage: 0,
        blob_oid: (index + 1).toString(16).padStart(40, '0'),
        object_type: 'blob',
      }))
      const bytes = new Map(paths.map((path) => [path, Buffer.from(path)]))
      const index = rawIndex % paths.length
      const working = new Map(bytes)
      working.set(paths[index], Buffer.from(`${paths[index]}:changed`))
      const result = validateCutoverInputStateV1({
        closure,
        index_entries: indexEntries,
        object_format: 'sha1',
        index_blob_bytes_by_path: bytes,
        working_tree_bytes_by_path: working,
        excluded_paths: [],
        build_output_paths: [],
      })
      assert.deepEqual(result.violations, [{
        code: 'cutover-input-closure-incomplete',
        path: paths[index],
        reason: 'working-tree-index-mismatch',
      }])
    },
  ), { seed: SEED, numRuns: 100 })
})
