/**
 * G4R-CE §24 S0 RED scaffolding: obsolete-controller absence + raw-time scanners
 * must detect synthetic violations. Production tree is allowed to still be dirty
 * (controllers not deleted in S0; check.mjs not hard-wired).
 */
import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import {
  OBSOLETE_CONTROLLER_PATHS,
  RAW_TIME_ALLOWLIST,
  RAW_TIME_SCAN_LAYERS,
  RAW_TIME_TOKENS,
  isRawTimeAllowlisted,
  parsePhase,
  scanG4RCeVocabulary,
  scanObsoleteControllerAbsence,
  scanRawTimeEntries,
} from '../../../scripts/checks/g4r-ce-vocabulary.mjs'

test('G4R_CE_S0_documents_obsolete_controller_paths', () => {
  assert.ok(OBSOLETE_CONTROLLER_PATHS.includes('Application/Reconciliation/TurnCompletionProgram.fs'))
  assert.ok(OBSOLETE_CONTROLLER_PATHS.includes('Infrastructure/OpenCode/Tools/FinalityController.fs'))
  assert.ok(OBSOLETE_CONTROLLER_PATHS.includes('Session/ReviewController.fs'))
  assert.ok(OBSOLETE_CONTROLLER_PATHS.includes('Application/Reconciliation/ManagerLifecycleGate.fs'))
  assert.ok(OBSOLETE_CONTROLLER_PATHS.includes('Application/Reconciliation/ReviewerGuardState.fs'))
  assert.ok(
    OBSOLETE_CONTROLLER_PATHS.includes('Infrastructure/OpenCode/Orchestration/HostReviewProgram.fs'),
  )
  assert.equal(OBSOLETE_CONTROLLER_PATHS.length, 6)
})

test('G4R_CE_S0_documents_raw_time_tokens_and_layers', () => {
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
  assert.deepEqual([...RAW_TIME_SCAN_LAYERS], ['Domain', 'Application', 'Session'])
})

test('G4R_CE_S0_obsolete_scanner_RED_on_synthetic_presence', () => {
  const present = new Set([
    'Application/Reconciliation/TurnCompletionProgram.fs',
    'Session/ReviewController.fs',
  ])
  const hits = scanObsoleteControllerAbsence(OBSOLETE_CONTROLLER_PATHS, (rel) => present.has(rel))
  assert.equal(hits.length, 2)
  assert.ok(hits.every((h) => h.kind === 'obsolete-controller'))
  assert.ok(hits.some((h) => h.path.includes('TurnCompletionProgram')))
  assert.ok(hits.some((h) => h.path.includes('ReviewController')))
})

test('G4R_CE_S0_obsolete_scanner_GREEN_when_all_absent', () => {
  const hits = scanObsoleteControllerAbsence(OBSOLETE_CONTROLLER_PATHS, () => false)
  assert.equal(hits.length, 0)
})

test('G4R_CE_S0_raw_time_scanner_RED_on_synthetic_tokens', () => {
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

test('G4R_CE_S0_raw_time_scanner_ignores_comment_only_mentions', () => {
  const clean = scanRawTimeEntries([
    {
      file: 'Domain/Doc.fs',
      text: '/// Prefer CausalAwait; do not use DateTimeOffset.UtcNow here.\nmodule Doc\n',
    },
  ])
  assert.equal(clean.length, 0)
})

test('G4R_CE_S0_raw_time_allowlist_hook_skips_physical_adapter', () => {
  const file = 'Session/PhysicalClockAdapter.fs'
  assert.equal(isRawTimeAllowlisted(file, []), false)
  assert.equal(isRawTimeAllowlisted(file, ['Session/PhysicalClockAdapter.fs']), true)
  assert.equal(isRawTimeAllowlisted(file, ['Session/']), true)

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

test('G4R_CE_S0_synthetic_tree_scan_proves_gate_can_fail', () => {
  const tmp = mkdtempSync(join(tmpdir(), 'g4r-ce-s0-'))
  try {
    const prod = join(tmp, 'src/Wanxiangshu')
    const turnDir = join(prod, 'Application/Reconciliation')
    const sessionDir = join(prod, 'Session')
    mkdirSync(turnDir, { recursive: true })
    mkdirSync(sessionDir, { recursive: true })
    writeFileSync(
      join(turnDir, 'TurnCompletionProgram.fs'),
      'module TurnCompletionProgram\nlet x = DateTimeOffset.UtcNow\n',
    )
    writeFileSync(join(sessionDir, 'Clean.fs'), 'module Clean\nlet x = 1\n')

    const { obsolete, rawTime, violations } = scanG4RCeVocabulary(tmp)
    assert.ok(
      obsolete.some((h) => h.path.endsWith('TurnCompletionProgram.fs')),
      'expected obsolete TurnCompletionProgram hit',
    )
    assert.ok(
      rawTime.some((h) => h.token === 'DateTimeOffset.UtcNow'),
      'expected raw-time hit in synthetic Application file',
    )
    assert.ok(violations.length >= 2)
  } finally {
    rmSync(tmp, { recursive: true, force: true })
  }
})

test('G4R_CE_S0_parse_phase_defaults_to_soft', () => {
  assert.equal(parsePhase([]), 's0-soft')
  assert.equal(parsePhase(['--phase=s0-soft']), 's0-soft')
  assert.equal(parsePhase(['--phase=hard']), 'hard')
  assert.throws(() => parsePhase(['--phase=nope']), /unknown --phase/)
})

test('G4R_CE_S0_production_may_still_be_dirty_soft_scaffolding', () => {
  // Document current world: detectors run, but S0 does not require a clean tree.
  const { obsolete, rawTime } = scanG4RCeVocabulary()
  assert.ok(
    obsolete.length >= 1,
    'S0 expects obsolete controllers still present in production (do not delete yet)',
  )
  // Session still has UtcNow / timerTask — raw-time debt is expected until S1+.
  assert.ok(Array.isArray(rawTime), 'raw-time scanner must return an array against production')
})
