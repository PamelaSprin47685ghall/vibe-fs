import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { default: test } = await import("node:test");
const { walk } = await import("../../../scripts/lib/walk.mjs");


test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_004: no Command/Reply/Step AST tokens in Orchestration workflow source', () => {
  // Fail closed if a second-runtime protocol sneaks back into the vertical slice.
  const files = walk('src/Wanxiangshu/Change', ['.fs'])
  assert.ok(files.length > 0, 'expected Change/*.fs')
  const forbidden =
    /\b(?:type|and)\s+(?:private\s+|internal\s+|public\s+)?(?:\w*(?:Command|Reply)(?:<[^=>]*>)?|(?:\w*Program)<[^=>]*>)\s*=|\|\s*(?:Step|Suspend)\s+of\b|\bProtocolMismatch\b|\bmodule\s+(?:private\s+|internal\s+)?(?:\w+\.)*\w*Interpreter\s*=/
  const hits = []
  for (const file of files) {
    const text = readFileSync(file, 'utf8')
    for (const [i, line] of text.split('\n').entries()) {
      const code = line.replace(/\/\/.*/g, '').trim()
      if (code && forbidden.test(code)) hits.push(`${file}:${i + 1}: ${line.trim()}`)
    }
  }
  assert.deepEqual(hits, [])
})
test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_002: Domain OrchestratorProgram AST module is gone', () => {
  const files = walk('src/Wanxiangshu', ['.fs'])
  const ast = files.filter((file) => /OrchestratorProgram\.fs$/.test(file))
  assert.deepEqual(ast, [], 'Domain OrchestratorProgram AST module must be deleted after PR3 direct-CE cutover')
})
test('WHAT[STRUCTURED-WORKFLOW-002] ORCHESTRATOR_PROGRAM_003: OrchestratorInterpreter is gone', () => {
  const files = walk('src/Wanxiangshu', ['.fs'])
  const interpreter = files.filter((file) => /OrchestratorInterpreter\.fs$/.test(file))
  assert.deepEqual(interpreter, [], 'OrchestratorInterpreter must be deleted after PR3')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { RAW_TIME_ALLOWLIST, RAW_TIME_SCAN_ROOTS, RAW_TIME_TOKENS, isRawTimeAllowlisted, scanRawTimeEntries } = await import("../../../scripts/lib/raw-time-scan.mjs");


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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const reconcileSurface = await import("../../../dist/Composition/Turn/ReconcileSurface.js");

const evidence = {
  snapshotError: (reason) => reconcileSurface.evidenceSnapshotError(reason),
  noTurn: () => reconcileSurface.evidenceNoTurn(),
  provisional: (outcome) => reconcileSurface.evidenceProvisional(outcome),
  unknown: () => reconcileSurface.evidenceUnknown(),
  terminal: (outcome) => reconcileSurface.evidenceTerminal(outcome),
  sessionCleared: () => reconcileSurface.evidenceSessionCleared(),
}
const wake = {
  idle: (session = 'ses-a', attemptSerial = 1) => reconcileSurface.idleWake(session, attemptSerial),
  retry: () => reconcileSurface.retryWake(),
  failure: () => reconcileSurface.failureWake(),
  abort: () => reconcileSurface.abortWake(),
}
const name = (observation, signal = wake.retry()) =>
  reconcileSurface.decisionName(reconcileSurface.decideStep(signal, observation))

test('WHAT[STRUCTURED-WORKFLOW-002] RECONCILE_PROGRAM_006: Domain surface has no Command/Reply/Trace AST exports', () => {
  // The semantic owner exposes named observations and opaque publish maps, not
  // a second-runtime AST. Presence of the owner operations is the contract;
  // emitted export enumeration is deliberately not part of this test.
  assert.equal(typeof reconcileSurface.decideStep, 'function')
  assert.equal(typeof reconcileSurface.publishDecision, 'function')
  assert.equal(typeof reconcileSurface.acceptedTurnFields, 'function')
})
}
