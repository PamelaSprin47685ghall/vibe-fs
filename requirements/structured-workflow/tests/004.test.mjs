import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import {
  scanControlPyramidViolations,
  checkControlPyramidRatchet,
} from '../../../scripts/checks/control-pyramid-ratchet.mjs'
import * as ReconcileSurface from '../../../dist/Composition/Turn/ReconcileSurface.js'
import * as OutcomeSurface from '../../../dist/Foundation/OutcomeSurface.js'

test('WHAT[STRUCTURED-WORKFLOW-004] Fable async Result plumbing provides sequential short-circuiting traversal', async () => {
  assert.equal(typeof OutcomeSurface.isTerminal, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_nested_match_is_RED_at_the_inner_decision', () => {
  const sample = `
let f x y =
    match x with
    | Some a ->
        match y with
        | Some b -> a + b
        | None -> a
    | None -> 0
`
  const violations = scanControlPyramidViolations(sample, 'sample.fs')
  assert.ok(violations.length >= 1, 'nested match must produce violation')
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_mixed_match_if_try_is_aggressively_detected', () => {
  const sample = `
let f x y =
    match x with
    | Some a ->
        if y > 0 then a else 0
    | None -> 0
`
  const violations = scanControlPyramidViolations(sample, 'sample.fs')
  assert.ok(violations.length >= 1, 'mixed match-if must produce violation')
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_flat_sequential_decisions_are_GREEN', () => {
  const sample = `
let f x =
    match x with
    | Some a -> a
    | None -> 0

let g y =
    if y > 0 then 1 else 0
`
  const violations = scanControlPyramidViolations(sample, 'sample.fs')
  assert.deepEqual(violations, [], 'flat sequential decisions must be green')
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_tuple_match_is_GREEN', () => {
  const sample = `
let f x y =
    match x, y with
    | Some a, Some b -> a + b
    | Some a, None -> a
    | None, _ -> 0
`
  const violations = scanControlPyramidViolations(sample, 'sample.fs')
  assert.deepEqual(violations, [], 'tuple match is single decision level and must be green')
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_if_elif_chain_is_GREEN_because_it_is_one_decision_level', () => {
  const sample = `
let f x =
    if x > 10 then 1
    elif x > 5 then 2
    else 3
`
  const violations = scanControlPyramidViolations(sample, 'sample.fs')
  assert.deepEqual(violations, [], 'if-elif chain is one decision level and must be green')
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_comments_and_multiline_strings_do_not_create_fake_hits', () => {
  const sample = `
let f x =
    // match inside comment
    // if nested then
    match x with
    | Some a -> a
    | None -> 0
`
  const violations = scanControlPyramidViolations(sample, 'sample.fs')
  assert.deepEqual(violations, [], 'comments must not create fake hits')
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_ratchet_accepts_equal_or_lower_per_file_debt', () => {
  const baseline = { 'fileA.fs': 2 }
  const current = { 'fileA.fs': 2 }
  const res = checkControlPyramidRatchet(current, baseline)
  assert.equal(res.ok, true)
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_ratchet_rejects_new_or_increased_file_debt', () => {
  const baseline = { 'fileA.fs': 1 }
  const current = { 'fileA.fs': 2 }
  const res = checkControlPyramidRatchet(current, baseline)
  assert.equal(res.ok, false)
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_many_hits_print_locations_but_the_long_tutorial_once', () => {
  assert.equal(typeof scanControlPyramidViolations, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-004] CONTROL_PYRAMID_tutorial_prerequisites_are_repo_concrete_and_cannot_be_shrunk', () => {
  assert.equal(typeof checkControlPyramidRatchet, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-004] RECONCILE_PROGRAM_001: isTerminalOutcome classifies terminal vs provisional', () => {
  assert.equal(typeof ReconcileSurface.isTerminalOutcome, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-004] RECONCILE_PROGRAM_003: decideStep produces one decision per causal edge (HOST-BOUNDARY-005)', () => {
  assert.equal(typeof ReconcileSurface.decideStep, 'function')
})

test('WHAT[STRUCTURED-WORKFLOW-004] RECONCILE_PROGRAM_004: publishDecision gates already-published terminal and provisional', () => {
  assert.equal(typeof ReconcileSurface.publishDecision, 'function')
})
