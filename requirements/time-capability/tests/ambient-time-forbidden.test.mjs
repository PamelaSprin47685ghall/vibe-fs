// TIME-004 — every production source crosses one fail-closed ambient-time gate.

import assert from 'node:assert/strict'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { dirname, join, resolve } from 'node:path'

import {
  RAW_TIME_SCAN_ROOTS,
  collectRawTimeScanEntries,
  scanG4RCeVocabulary,
  scanRawTimeEntries,
} from '../../../scripts/checks/g4r-ce-vocabulary.mjs'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
const PRODUCTION_ROOT = join(ROOT, 'src', 'Wanxiangshu')

test('WHAT[TIME-004] domain_application_session_contain_no_raw_time_tokens', () => {
  const hits = scanG4RCeVocabulary(ROOT).rawTime
  assert.equal(
    hits.length,
    0,
    `no raw wall-clock / timer token may appear in Domain/Application/Session: ${hits
      .map((h) => `${h.file}:${h.line} ${h.token}`)
      .join('; ')}`,
  )
})

test('WHAT[TIME-004] collector_reads_the_whole_production_tree_before_allowlisting_adapters', () => {
  assert.deepEqual([...RAW_TIME_SCAN_ROOTS], ['.'])
  const entries = collectRawTimeScanEntries(PRODUCTION_ROOT, RAW_TIME_SCAN_ROOTS)
  assert.ok(entries.some((entry) => entry.file === 'Process/PtyTiming.fs'))
  assert.ok(entries.some((entry) => entry.file === 'Change/Program.fs'))
})

test('WHAT[TIME-004] exact_mutation_is_detected_without_a_directory_allowlist_escape', () => {
  const source = [
    'module Synthetic',
    'let a = DateTimeOffset.UtcNow',
    'let b = DateTime.Now',
    'let c = DateTime.UtcNow',
    'let d = Date.now()',
    'do setTimeout (fun () -> ()) 1',
    'do! PtyTiming.timerTask 100',
  ].join('\n')

  assert.match(source, /DateTimeOffset\.UtcNow/)
  const dirty = scanRawTimeEntries([
    {
      file: 'Execution/Synthetic.fs',
      text: source,
    },
  ])
  assert.deepEqual(
    dirty.map((hit) => hit.token),
    ['DateTimeOffset.UtcNow', 'DateTime.Now', 'DateTime.UtcNow', 'Date.now', 'setTimeout', 'timerTask'],
  )

  const unlistedSibling = scanRawTimeEntries([
    {
      file: 'Execution/Delegation/Fork/OpenCode/Unlisted.fs',
      text: 'let now = DateTimeOffset.UtcNow\n',
    },
  ])
  assert.deepEqual(unlistedSibling.map((hit) => hit.token), ['DateTimeOffset.UtcNow'])
})

test('WHAT[TIME-004] missing_production_root_fails_closed', () => {
  const missing = join(PRODUCTION_ROOT, '__missing_time_gate_root__')
  assert.throws(
    () => collectRawTimeScanEntries(missing, RAW_TIME_SCAN_ROOTS),
    /raw-time scan root does not exist/,
  )
})
