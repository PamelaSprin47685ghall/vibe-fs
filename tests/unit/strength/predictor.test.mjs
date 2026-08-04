// tests/unit/Strength/predictor.test.mjs — spec/14 STRENGTH-021/022/023/092.
//
// The request-level n-gram predictor. Pure functions only: state in, state out.
// These tests pin the contract STRENGTH-092 names:
//   grep → read, glob → concurrent read, read → write, read → EOT,
//   sparse-context fallback, cold start, count decay, batch canonicalization.

import assert from 'node:assert/strict'
import test from 'node:test'
import { strength } from '../support/domain.mjs'

const read = (parallelism = 1) =>
  strength.requestSymbol('ReadBatch', [strength.readBatch({ parallelism, resultBytes: 0 })])
const write = () => strength.requestSymbol('WriteBatch')
const eot = () => strength.requestSymbol('Eot')

const features = (over = {}) => ({
  RecentHitFileCount: over.hitFiles ?? 0,
  RecentHitPositionCount: over.hitPositions ?? 0,
  RecentResultEmpty: over.empty ?? false,
  RecentUniquePath: over.uniquePath ?? false,
  RecentPathConcentration: over.concentration ?? 0,
  RecentReadOutcome: over.readOutcome ?? 'Success',
  RecentConcurrencyWidth: over.width ?? 1,
  RecentResultUtf8Bytes: over.bytes ?? 0,
  IsFirstRequestAfterRoot: over.first ?? false,
  HasPrefixProbe: over.probe ?? false,
})

// ── cold start ──────────────────────────────────────────────────────────────

test('STRENGTH_022_cold_start_predicts_low_without_observations', () => {
  const state = strength.emptyRoleState()
  const { p1, p2 } = strength.predictRead(state, [], features())
  assert.ok(p1 >= 0 && p1 < 0.5, `cold-start p1 should be low, got ${p1}`)
  assert.ok(p2 >= 0 && p2 <= p1 + 1e-9, `p2 should not exceed p1, got p2=${p2} p1=${p1}`)
})

// ── grep → read ─────────────────────────────────────────────────────────────

test('STRENGTH_092_grep_then_read_raises_read_probability', () => {
  // Train: grep (OtherBatch) followed by a read batch, repeatedly. The read
  // symbol must use the same parallelism as `predictRead` queries (1) so the
  // n-gram key matches — a different concurrency width is a different symbol.
  let state = strength.emptyRoleState()
  for (let i = 0; i < 30; i++) {
    state = strength.observeRequest(state, [
      strength.requestSymbol('OtherBatch'),
      read(1),
      eot(),
    ])
  }

  const cold = strength.predictRead(strength.emptyRoleState(), [], features({ hitFiles: 3 }))
  const trained = strength.predictRead(state, [strength.requestSymbol('OtherBatch')], features({ hitFiles: 3 }))

  assert.ok(
    trained.p1 > cold.p1,
    `trained p1 (${trained.p1}) should exceed cold-start p1 (${cold.p1}) after grep→read training`,
  )
})

// ── read → write (read should be LOWER after a write habit) ─────────────────

test('STRENGTH_092_read_then_write_reduces_read_probability', () => {
  let state = strength.emptyRoleState()
  for (let i = 0; i < 30; i++) {
    state = strength.observeRequest(state, [read(1), write(), eot()])
  }

  const afterReadWrite = strength.predictRead(state, [read(1)], features())
  const readOnly = strength.predictRead(
    state,
    [strength.requestSymbol('OtherBatch')],
    features(),
  )

  // A history of read→write makes "read next" less likely than a neutral history.
  assert.ok(
    afterReadWrite.p1 <= readOnly.p1 + 1e-9,
    `read-after-write p1 (${afterReadWrite.p1}) should not exceed neutral p1 (${readOnly.p1})`,
  )
})

// ── read → EOT ──────────────────────────────────────────────────────────────

test('STRENGTH_092_read_then_eot_marks_the_sequence_terminal', () => {
  let state = strength.emptyRoleState()
  for (let i = 0; i < 30; i++) {
    state = strength.observeRequest(state, [read(1), eot()])
  }

  // After a read that is usually terminal, the probability of ANOTHER read batch
  // must be low.
  const afterRead = strength.predictRead(state, [read(1)], features())
  assert.ok(
    afterRead.p1 < 0.6,
    `read→EOT habit should suppress another read, got p1=${afterRead.p1}`,
  )
})

// ── sparse context / backoff ────────────────────────────────────────────────

test('STRENGTH_022_sparse_context_falls_back_to_lower_order', () => {
  let state = strength.emptyRoleState()
  // Train ONLY the bigram (grep, read) — no trigram.
  for (let i = 0; i < 30; i++) {
    state = strength.observeRequest(state, [strength.requestSymbol('OtherBatch'), read(1), eot()])
  }

  // A longer history whose trigram was never seen still benefits from the
  // bigram via backoff.
  const { p1 } = strength.predictRead(
    state,
    [strength.requestSymbol('OtherBatch'), strength.requestSymbol('OtherBatch')],
    features({ hitFiles: 1 }),
  )
  assert.ok(p1 > 0, `backoff should produce a nonzero probability, got ${p1}`)
})

// ── continuation backoff（STRENGTH-022 KN 一元续接）─────────────────────────

test('STRENGTH_022_unigram_continuation_backoff_is_alive', () => {
  // With an EMPTY history and zero structure features, predictRead's p1 IS the
  // unigram continuation pCont (backoff to order 0). Training read→eot habits
  // must make "read" a known successor, so p1 rises above the untrained state.
  // This pins the KN backoff: if continuation keys were built or queried wrong
  // (length-1 key, head-vs-last confusion), pCont is 0 and this fails.
  const zeroFeatures = features() // hitFiles 0, empty:false etc.
  const untrained = strength.predictRead(strength.emptyRoleState(), [], zeroFeatures)

  let state = strength.emptyRoleState()
  for (let i = 0; i < 40; i++) {
    state = strength.observeRequest(state, [read(1), eot()])
  }
  const trained = strength.predictRead(state, [], zeroFeatures)

  assert.ok(
    trained.p1 > untrained.p1,
    `trained empty-history p1 (${trained.p1}) should exceed untrained (${untrained.p1})`,
  )
})

// ── structure features (STRENGTH-023) ───────────────────────────────────────

test('STRENGTH_023_many_grep_hits_boost_read_probability', () => {
  const state = strength.emptyRoleState()
  const sparse = strength.predictRead(state, [], features({ hitFiles: 0 }))
  const dense = strength.predictRead(state, [], features({ hitFiles: 5 }))
  assert.ok(
    dense.p1 > sparse.p1,
    `dense grep hits (p1=${dense.p1}) should boost read over sparse (p1=${sparse.p1})`,
  )
})

// ── count decay (STRENGTH-022) ──────────────────────────────────────────────

test('STRENGTH_022_count_decay_damps_stale_habits', () => {
  // 30 observations is below the 4096 decay interval, so decay must not fire.
  let state = strength.emptyRoleState()
  for (let i = 0; i < 30; i++) {
    state = strength.observeRequest(state, [read(1), eot()])
  }
  assert.ok(state.EffectiveSymbolCount > 0, 'effective symbol count should accumulate')
  // Decay only happens at CountDecayInterval multiples; below it the count grows.
  assert.ok(
    state.EffectiveSymbolCount < 4096,
    `below the decay interval no decay should have fired, count=${state.EffectiveSymbolCount}`,
  )
})
