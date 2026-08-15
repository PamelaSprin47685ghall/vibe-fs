import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  CONTROL_PYRAMID_GUIDE,
  ROOT,
  collectControlPyramidEntries,
  evaluateBaseline,
  makeBaseline,
  renderFailure,
  scanControlPyramidEntries,
} from '../../../scripts/checks/fsharp-control-pyramid.mjs'

const scan = (text) => scanControlPyramidEntries([{ file: 'Example.fs', text }])

test('CONTROL_PYRAMID_nested_match_is_RED_at_the_inner_decision', () => {
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

test('CONTROL_PYRAMID_mixed_match_if_try_is_aggressively_detected', () => {
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

test('CONTROL_PYRAMID_flat_sequential_decisions_are_GREEN', () => {
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

test('CONTROL_PYRAMID_tuple_match_is_GREEN', () => {
  const hits = scan(`
let decide a b =
    match a, b with
    | Some a, Some b -> useBoth a b
    | None, _ -> missingA ()
    | _, None -> missingB ()
`)

  assert.equal(hits.length, 0)
})

test('CONTROL_PYRAMID_if_elif_chain_is_GREEN_because_it_is_one_decision_level', () => {
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

test('CONTROL_PYRAMID_comments_and_multiline_strings_do_not_create_fake_hits', () => {
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

test('CONTROL_PYRAMID_ratchet_accepts_equal_or_lower_per_file_debt', () => {
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

test('CONTROL_PYRAMID_ratchet_rejects_new_or_increased_file_debt', () => {
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

test('CONTROL_PYRAMID_many_hits_print_locations_but_the_long_tutorial_once', () => {
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

  assert.equal(output.match(/F# CONTROL PYRAMID — REPAIR MANUAL/g)?.length, 1)
  assert.match(output, /a\.fs:10/)
  assert.match(output, /b\.fs:20/)
  assert.match(output, /c\.fs:30/)
  assert.match(output, /match → match!/)
})

test.skip('CONTROL_PYRAMID_production_baseline_is_exact_and_the_main_check_runner_enforces_it', () => {
  const hits = scanControlPyramidEntries(collectControlPyramidEntries(ROOT, 'src/Wanxiangshu'))
  const baseline = JSON.parse(
    readFileSync('scripts/checks/fsharp-control-pyramid-baseline.json', 'utf8'),
  )
  const runner = readFileSync('scripts/check.mjs', 'utf8')

  assert.deepEqual(baseline, makeBaseline(hits))
  assert.ok(hits.length <= 2166, 'control-pyramid debt may only decrease from the bootstrap ceiling')
  assert.match(runner, /checks\/fsharp-control-pyramid\.mjs/)
  assert.match(runner, /fsharp-control-pyramid-baseline\.json/)
})

test('CONTROL_PYRAMID_tutorial_prerequisites_are_repo_concrete_and_cannot_be_shrunk', () => {
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
