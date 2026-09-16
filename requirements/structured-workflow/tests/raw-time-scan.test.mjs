/** Ambient-time scanner unit coverage (TIME-004 instrument), formerly G4R-CE §24. */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  RAW_TIME_ALLOWLIST,
  RAW_TIME_SCAN_ROOTS,
  RAW_TIME_TOKENS,
  isRawTimeAllowlisted,
  scanRawTimeEntries,
} from '../../../scripts/lib/raw-time-scan.mjs'

test('WHAT[STRUCTURED-WORKFLOW-002] G4R_CE_documents_raw_time_tokens_and_scan_root', () => {
  for (const token of [
    'DateTimeOffset.UtcNow',
    'DateTime.Now',
    'DateTime.UtcNow',
    'Date.now',
    'setTimeout',
    'timerTask',
  ]) {
    assert.ok(RAW_TIME_TOKENS.includes(token), `missing token ${token}`)
  }
  assert.deepEqual([...RAW_TIME_SCAN_ROOTS], ['.'])
})

test('WHAT[STRUCTURED-WORKFLOW-002] G4R_CE_S0_raw_time_scanner_RED_on_synthetic_tokens', () => {
  const dirty = scanRawTimeEntries([
    {
      file: 'Application/Reconciliation/Evil.fs',
      text: [
        'module Evil',
        'let deadline = DateTimeOffset.UtcNow.AddMilliseconds 25.',
        'let wall = DateTime.UtcNow',
        'let local = DateTime.Now',
        'let js = Date.now()',
        'do setTimeout (fun () -> ()) 10',
        'do! PtyTiming.timerTask 100',
      ].join('\n'),
    },
  ])
  assert.ok(dirty.length >= 6, `expected ≥6 hits, got ${dirty.length}: ${JSON.stringify(dirty)}`)
  for (const token of RAW_TIME_TOKENS) {
    assert.ok(
      dirty.some((h) => h.token === token),
      `expected detection of ${token}`,
    )
  }
})

test('WHAT[STRUCTURED-WORKFLOW-002] G4R_CE_S0_raw_time_scanner_ignores_comment_only_mentions', () => {
  const clean = scanRawTimeEntries([
    {
      file: 'Domain/Doc.fs',
      text: '/// Prefer CausalAwait; do not use DateTimeOffset.UtcNow here.\nmodule Doc\n',
    },
  ])
  assert.equal(clean.length, 0)
})

test('WHAT[STRUCTURED-WORKFLOW-002] G4R_CE_raw_time_allowlist_is_exact_file_only', () => {
  const file = 'Session/PhysicalClockAdapter.fs'
  assert.equal(isRawTimeAllowlisted(file, []), false)
  assert.equal(isRawTimeAllowlisted(file, ['Session/PhysicalClockAdapter.fs']), true)
  assert.equal(isRawTimeAllowlisted(file, ['Session/']), false)

  const hits = scanRawTimeEntries(
    [{ file, text: 'let now = DateTimeOffset.UtcNow\n' }],
    { allowlist: ['Session/PhysicalClockAdapter.fs'] },
  )
  assert.equal(hits.length, 0)

  const unlisted = scanRawTimeEntries(
    [{ file, text: 'let now = DateTimeOffset.UtcNow\n' }],
    { allowlist: RAW_TIME_ALLOWLIST },
  )
  assert.equal(unlisted.length, 1)
})
