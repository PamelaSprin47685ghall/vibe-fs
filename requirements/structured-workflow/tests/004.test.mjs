import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { TaskResultListSurface_traverseM } = await import("../../../dist/Foundation/FsToolkitFableCompat.js");
const outcomeSurface = await import("../../../dist/Foundation/OutcomeSurface.js");


test('WHAT[structured-workflow-004] Fable async Result plumbing provides sequential short-circuiting traversal', async () => {
  const traversedOk = await TaskResultListSurface_traverseM((x) => Promise.resolve(x > 0), [1, 2, 3])
  assert.deepEqual(traversedOk, ['Ok', 1, 2, 3])

  const traversedErr = await TaskResultListSurface_traverseM((x) => Promise.resolve(x !== 2), [1, 2, 3])
  assert.deepEqual(traversedErr, ['Error', 2])
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { CONTROL_PYRAMID_GUIDE, evaluateBaseline, renderFailure, scanControlPyramidEntries } = await import("../../../scripts/checks/fsharp-control-pyramid.mjs");

const scan = (text) => scanControlPyramidEntries([{ file: 'Example.fs', text }])

test('WHAT[structured-workflow-004] CONTROL_PYRAMID_nested_match_is_RED_at_the_inner_decision', () => {
  const hits = scan(`
module Example

let decode input =
    match input with
    | None -> None
    | Some a ->
        match parse a with
        | None -> None
        | Some b -> Some b
`)

  assert.equal(hits.length, 1)
  assert.equal(hits[0].kind, 'match-pyramid')
  assert.equal(hits[0].depth, 2)
  assert.equal(hits[0].line, 8)
  assert.deepEqual(hits[0].chain, ['match', 'match'])
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_mixed_match_if_try_is_aggressively_detected', () => {
  const hits = scan(`
let run state =
    match state with
    | Ready value ->
        if value > 0 then
            try
                work value
            with ex ->
                recover ex
    | Idle -> idle ()
`)

  assert.deepEqual(
    hits.map((hit) => [hit.kind, hit.depth, hit.chain.join('>')]),
    [
      ['branch-pyramid', 2, 'match>if'],
      ['branch-pyramid', 3, 'match>if>try'],
    ],
  )
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_flat_sequential_decisions_are_GREEN', () => {
  const hits = scan(`
let first =
    match a with
    | A -> one ()
    | B -> two ()

let second =
    match b with
    | C -> three ()
    | D -> four ()
`)

  assert.equal(hits.length, 0)
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_tuple_match_is_GREEN', () => {
  const hits = scan(`
let decide a b =
    match a, b with
    | Some a, Some b -> useBoth a b
    | None, _ -> missingA ()
    | _, None -> missingB ()
`)

  assert.equal(hits.length, 0)
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_if_elif_chain_is_GREEN_because_it_is_one_decision_level', () => {
  const hits = scan(`
let decide enabled accepted leased =
    if not enabled then
        Disabled
    elif not accepted then
        NotAccepted
    elif not leased then
        LeaseMissing
    else
        Ready
`)

  assert.equal(hits.length, 0)
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_comments_and_multiline_strings_do_not_create_fake_hits', () => {
  const hits = scan(`
let sample = """
match fake with
| A ->
    match fakeAgain with
    | B -> 1
"""

(*
match commented with
| A ->
    if fake then
        1
*)

let real x =
    match x with
    | A -> 1
    | B -> 2
`)

  assert.equal(hits.length, 0)
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_ratchet_accepts_equal_or_lower_per_file_debt', () => {
  const baseline = {
    version: 1,
    files: { 'A.fs': 2, 'B.fs': 1 },
  }
  const hits = [
    { file: 'A.fs', line: 10, depth: 2 },
    { file: 'A.fs', line: 20, depth: 3 },
    { file: 'B.fs', line: 30, depth: 2 },
  ]

  const result = evaluateBaseline(hits, baseline)
  assert.equal(result.regressions.length, 0)
  assert.equal(result.currentTotal, 3)
  assert.equal(result.baselineTotal, 3)
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_ratchet_rejects_new_or_increased_file_debt', () => {
  const baseline = {
    version: 1,
    files: { 'A.fs': 1 },
  }
  const hits = [
    { file: 'A.fs', line: 10, depth: 2, text: 'match b with' },
    { file: 'A.fs', line: 20, depth: 3, text: 'if c then' },
    { file: 'New.fs', line: 7, depth: 2, text: 'match d with' },
  ]

  const result = evaluateBaseline(hits, baseline)
  assert.deepEqual(
    result.regressions.map((r) => [r.file, r.baseline, r.current]),
    [
      ['A.fs', 1, 2],
      ['New.fs', 0, 1],
    ],
  )
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_many_hits_print_locations_but_the_long_tutorial_once', () => {
  const output = renderFailure([
    {
      file: 'a.fs',
      line: 10,
      depth: 2,
      kind: 'match-pyramid',
      chain: ['match', 'match!'],
      text: 'match! readA () with',
    },
    {
      file: 'b.fs',
      line: 20,
      depth: 3,
      kind: 'branch-pyramid',
      chain: ['match', 'if', 'match'],
      text: 'match status with',
    },
    {
      file: 'c.fs',
      line: 30,
      depth: 2,
      kind: 'branch-pyramid',
      chain: ['if', 'if'],
      text: 'if accepted then',
    },
  ])

  assert.equal(output.match(/F# CONTROL PYRAMID — REPAIR MANUAL/g), null)
  assert.match(output, /a\.fs:10/)
  assert.match(output, /b\.fs:20/)
  assert.match(output, /c\.fs:30/)
  assert.match(output, /match → match!/)
  assert.match(output, /--explain/)
})
test('WHAT[structured-workflow-004] CONTROL_PYRAMID_tutorial_prerequisites_are_repo_concrete_and_cannot_be_shrunk', () => {
  assert.match(CONTROL_PYRAMID_GUIDE, /FsToolkit\.ErrorHandling/)
  assert.match(CONTROL_PYRAMID_GUIDE, /open FsToolkit\.ErrorHandling/)
  assert.match(CONTROL_PYRAMID_GUIDE, /open Wanxiangshu\.Foundation/)
  assert.match(CONTROL_PYRAMID_GUIDE, /TaskResultCE\.ofTask/)
  assert.match(CONTROL_PYRAMID_GUIDE, /TaskValue\.map/)
  assert.match(CONTROL_PYRAMID_GUIDE, /TaskResult\.mapError/)
  assert.match(CONTROL_PYRAMID_GUIDE, /TaskResultList\.traverseM/)
  assert.match(CONTROL_PYRAMID_GUIDE, /只有 Fable 平台/)
  assert.match(CONTROL_PYRAMID_GUIDE, /taskResult \{/)
  assert.match(CONTROL_PYRAMID_GUIDE, /result \{/)
  assert.match(CONTROL_PYRAMID_GUIDE, /match a, b with/)
  assert.match(CONTROL_PYRAMID_GUIDE, /false positive/)
  assert.match(CONTROL_PYRAMID_GUIDE, /--explain/)
  assert.ok(
    CONTROL_PYRAMID_GUIDE.split('\n').length >= 512,
    'the tightened repair manual must not become shorter than the original 512-line tutorial',
  )
  assert.ok(
    CONTROL_PYRAMID_GUIDE.length >= 9302,
    'the tightened repair manual must not become smaller than the original 9302-character tutorial',
  )
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

test('WHAT[structured-workflow-004] RECONCILE_PROGRAM_001: isTerminalOutcome classifies terminal vs provisional', () => {
  assert.equal(typeof reconcileSurface.isTerminalOutcome, 'function')
  assert.equal(typeof reconcileSurface.classifyTurn, 'function')

  for (const outcome of ['TurnCompleted', 'TurnAborted', 'TurnFailed']) {
    assert.equal(reconcileSurface.isTerminalOutcome(outcome), true)
    assert.deepEqual(reconcileSurface.classifyTurn(outcome), {
      outcome,
      state: 'terminal',
      isTerminal: true,
    })
  }

  for (const outcome of ['TurnInProgress', 'TurnNeedsContinuation']) {
    assert.equal(reconcileSurface.isTerminalOutcome(outcome), false)
    assert.deepEqual(reconcileSurface.classifyTurn(outcome), {
      outcome,
      state: 'provisional',
      isTerminal: false,
    })
  }

  // HOST-004 Clean Break: TurnUnknown is a snapshot observation, not a
  // publishable business turn. The owner rejects it rather than classifying it
  // as a provisional or terminal outcome.
  assert.equal(reconcileSurface.isSnapshotObservation('TurnUnknown'), true)
  assert.equal(reconcileSurface.isPublishableOutcome('TurnUnknown'), false)
  assert.throws(() => reconcileSurface.isTerminalOutcome('TurnUnknown'), /TurnUnknown/)
})
test('WHAT[structured-workflow-004] RECONCILE_PROGRAM_003: decideStep produces one decision per causal edge (host-boundary-005)', () => {
  assert.equal(typeof reconcileSurface.decideStep, 'function')
  assert.equal(typeof reconcileSurface.decisionName, 'function')

  // host-boundary-005: a later read needs a new coarse Host signal or exact
  // projection-change edge. The decision API has no read-budget input.
  //
  // SnapshotError / NoTurn → StopPass (nothing to act on).
  assert.equal(name(evidence.snapshotError('transient')), 'StopPass')
  assert.equal(name(evidence.noTurn()), 'StopPass')

  // Provisional: IdleWake → Publish (quiescent, belongs to business repair);
  // Retry/Failure/Abort → StopPass (provider status can race the public
  // session projection; Scheduler parks and the exact terminal edge re-kicks).
  assert.equal(name(evidence.provisional('TurnInProgress'), wake.idle('ses-a', 1)), 'Publish')
  assert.equal(name(evidence.provisional('TurnNeedsContinuation'), wake.idle('ses-a', 1)), 'Publish')
  assert.equal(name(evidence.provisional('TurnInProgress'), wake.retry()), 'StopPass')
  assert.equal(name(evidence.provisional('TurnNeedsContinuation'), wake.retry()), 'StopPass')
  assert.equal(name(evidence.provisional('TurnInProgress'), wake.failure()), 'StopPass')
  assert.equal(name(evidence.provisional('TurnInProgress'), wake.abort()), 'StopPass')

  // Unknown: IdleWake → Publish; Retry/Failure/Abort → StopPass.
  assert.equal(name(evidence.unknown(), wake.retry()), 'StopPass')
  assert.equal(name(evidence.unknown(), wake.failure()), 'StopPass')
  assert.equal(name(evidence.unknown(), wake.abort()), 'StopPass')
  assert.equal(name(evidence.unknown(), wake.idle('ses-a', 1)), 'Publish')

  // Successful/aborted terminal observations publish directly. A failed
  // provider terminal must wait for the matching typed physical witness.
  assert.equal(name(evidence.terminal('TurnCompleted')), 'Publish')
  assert.equal(name(evidence.terminal('TurnAborted')), 'Publish')
  assert.equal(name(evidence.terminal('TurnFailed')), 'StopPass')

  const failedPhysical = 'msg-structured-failure'
  assert.equal(
    reconcileSurface.decisionName(
      reconcileSurface.decideStep(
        reconcileSurface.failureWakeFor(failedPhysical),
        reconcileSurface.evidenceTerminalFor(failedPhysical, 'TurnFailed'),
      ),
    ),
    'Publish',
  )

  // Session cleared → StopPass.
  assert.equal(name(evidence.sessionCleared()), 'StopPass')

  assert.deepEqual(reconcileSurface.decideStep(wake.retry(), evidence.unknown()), { name: 'StopPass' })
})
test('WHAT[structured-workflow-004] RECONCILE_PROGRAM_004: publishDecision gates already-published terminal and provisional', () => {
  assert.equal(typeof reconcileSurface.publishDecision, 'function')
  assert.equal(typeof reconcileSurface.consumeKey, 'function')
  assert.deepEqual(reconcileSurface.acceptedTurnFields(), ['session', 'physical', 'providerRun', 'outcome'])

  const terminal = reconcileSurface.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })
  const provisional = reconcileSurface.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnInProgress',
  })
  const laterTerminal = reconcileSurface.turnFixture({
    session: 'ses-a',
    physical: 'user-1',
    providerRun: 'asst-1',
    outcome: 'TurnCompleted',
  })

  const empty = reconcileSurface.empty()

  // First provisional publish allowed; marks provisional map.
  const firstProv = reconcileSurface.publishDecision(empty, provisional)
  assert.equal(firstProv.shouldPublish, true)
  assert.equal(reconcileSurface.provisionalHas(firstProv.maps, provisional), true)
  assert.equal(reconcileSurface.consumedHas(firstProv.maps, provisional), false)

  // Same provisional token again: sealed out.
  const secondProv = reconcileSurface.publishDecision(firstProv.maps, provisional)
  assert.equal(secondProv.shouldPublish, false)

  // Terminal with same run identity is not sealed by provisional map.
  const term = reconcileSurface.publishDecision(firstProv.maps, laterTerminal)
  assert.equal(term.shouldPublish, true)
  assert.equal(reconcileSurface.consumedHas(term.maps, laterTerminal), true)
  // Terminal mark clears provisional for the session.
  assert.equal(reconcileSurface.provisionalHas(term.maps, provisional), false)

  // Duplicate terminal token sealed.
  const dupTerm = reconcileSurface.publishDecision(term.maps, laterTerminal)
  assert.equal(dupTerm.shouldPublish, false)

  // clearProvisional removes provisional seal without touching consumed.
  const cleared = reconcileSurface.clearProvisional(firstProv.maps, 'ses-a')
  assert.equal(reconcileSurface.provisionalHas(cleared, provisional), false)
})
}
