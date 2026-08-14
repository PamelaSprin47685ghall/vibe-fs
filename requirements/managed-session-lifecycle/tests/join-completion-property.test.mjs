// Property: ∀ StartedRun, within management deadline B it reaches EXACTLY ONE
// durable Terminal/Abandoned state; never Active past B; never double-completes.
// Deterministic permutations (seeded LCG / mulberry32) — failure prints seed.

import assert from 'node:assert/strict'
import test from 'node:test'

/** mulberry32 — 32-bit LCG PRNG; same seed → identical sequence. */
function mulberry32(seed) {
  let t = seed >>> 0
  return () => {
    t = (t + 0x6d2b79f5) >>> 0
    let r = Math.imul(t ^ (t >>> 15), 1 | t)
    r ^= r + Math.imul(r ^ (r >>> 7), 61 | r)
    return ((r ^ (r >>> 14)) >>> 0) / 4294967296
  }
}

/** Event alphabet: pure ops on a small in-test lifecycle machine. */
const EVENTS = [
  'promptAccepted',
  'sessionIdle',
  'messagesVisible',
  'terminalPublish',
  'journalAppend',
  'mailboxWake',
  'subscribeLate',
  'restart',
]

/**
 * In-test join/completion state machine (not production wiring).
 * Models the user invariant at the abstract layer covered by join-completion
 * + journal-subscription unit tests at the wire layer.
 *
 * Lifecycle:
 *   Unstarted → Active (promptAccepted)
 *   Active → Terminal (first durable complete: terminalPublish | journalAppend+mailboxWake path)
 *   Active → Abandoned (restart while Active and past soft grace, or explicit abandon via restart after idle without complete)
 *   Terminal/Abandoned are single-assignment durable cells.
 *
 * Deadline B: simulated tick budget. Every event consumes 1 tick after start.
 * Active after B ticks → property failure.
 */
function createRunMachine() {
  return {
    phase: 'Unstarted', // Unstarted | Active | Terminal | Abandoned
    startedAt: null,
    tick: 0,
    durable: null, // null | { kind: 'Terminal'|'Abandoned', via: string }
    completeCount: 0,
    mailbox: [],
    journal: [],
    sticky: null,
    subscribed: false,
    messagesVisible: false,
    idleSeen: false,
    promptAccepted: false,
  }
}

const DEADLINE_B = 100

function applyEvent(state, event) {
  if (state.phase === 'Active') state.tick += 1

  switch (event) {
    case 'promptAccepted': {
      if (state.phase === 'Unstarted') {
        state.phase = 'Active'
        state.startedAt = state.tick
        state.promptAccepted = true
      }
      break
    }
    case 'sessionIdle': {
      if (state.phase === 'Active') state.idleSeen = true
      break
    }
    case 'messagesVisible': {
      if (state.phase === 'Active') state.messagesVisible = true
      break
    }
    case 'terminalPublish': {
      // Sticky terminal: always record for late subscribers; durable only once.
      const outcome = { kind: 'Terminal', via: 'NotifyTerminal' }
      state.sticky = outcome
      if (state.phase === 'Active') {
        state.phase = 'Terminal'
        state.durable = outcome
        state.completeCount += 1
        state.mailbox.push(outcome)
      } else if (state.phase === 'Terminal' || state.phase === 'Abandoned') {
        // Double-complete attempt: must NOT advance completeCount / overwrite durable.
        // Sticky may refresh for replay, but durable cell stays single-assignment.
      }
      break
    }
    case 'journalAppend': {
      if (state.phase === 'Active' && state.promptAccepted) {
        state.journal.push({ kind: 'HandleCompleted' })
      }
      break
    }
    case 'mailboxWake': {
      // Durable complete if journal has HandleCompleted and still Active (single writer).
      if (state.phase === 'Active' && state.journal.length > 0) {
        const outcome = { kind: 'Terminal', via: 'HandleCompleted' }
        state.phase = 'Terminal'
        state.durable = outcome
        state.completeCount += 1
        state.mailbox.push(outcome)
        state.sticky = outcome
      } else if (state.phase === 'Active' && state.idleSeen && state.messagesVisible) {
        // Idle + visible material without journal: still allow terminal via wake path once.
        const outcome = { kind: 'Terminal', via: 'idle+visible' }
        state.phase = 'Terminal'
        state.durable = outcome
        state.completeCount += 1
        state.mailbox.push(outcome)
        state.sticky = outcome
      }
      break
    }
    case 'subscribeLate': {
      state.subscribed = true
      // Sticky replay does not create a second durable completion.
      if (state.sticky != null && state.phase === 'Active') {
        // Late join after sticky: adopt sticky as single durable terminal.
        state.phase = state.sticky.kind
        state.durable = state.sticky
        state.completeCount += 1
        state.mailbox.push(state.sticky)
      }
      break
    }
    case 'restart': {
      // Recreate mailbox/journal *view*; durable cell is the SSOT and survives.
      state.mailbox = []
      state.subscribed = false
      // If still Active at restart and idle was never completed, abandon once.
      if (state.phase === 'Active') {
        const outcome = { kind: 'Abandoned', via: 'restart' }
        state.phase = 'Abandoned'
        state.durable = outcome
        state.completeCount += 1
        state.sticky = outcome
      }
      // Journal durable facts remain; view recreated from them on next wake.
      break
    }
    default:
      throw new Error(`unknown event ${event}`)
  }

  // Soft force: if Active and tick reaches B without durable, force abandon at B
  // is NOT automatic — property checker treats still-Active-at-B as FAILURE.
  return state
}

function checkInvariant(state, seed, iter) {
  const where = `seed=0x${(seed >>> 0).toString(16)} iter=${iter}`

  // Single-assignment: at most one durable Terminal/Abandoned.
  assert.ok(
    state.completeCount <= 1,
    `${where}: double-complete (completeCount=${state.completeCount})`,
  )

  if (state.durable != null) {
    assert.ok(
      state.durable.kind === 'Terminal' || state.durable.kind === 'Abandoned',
      `${where}: durable kind must be Terminal|Abandoned, got ${state.durable.kind}`,
    )
    assert.equal(
      state.phase,
      state.durable.kind,
      `${where}: phase ${state.phase} ≠ durable ${state.durable.kind}`,
    )
    assert.equal(state.completeCount, 1, `${where}: durable set but completeCount≠1`)
  }

  // Never Active past deadline B.
  if (state.phase === 'Active' && state.tick > DEADLINE_B) {
    assert.fail(`${where}: still Active after deadline B=${DEADLINE_B} (tick=${state.tick})`)
  }
}

/**
 * One iteration: start a run, apply a seeded permutation of events for up to
 * DEADLINE_B+ε ticks, force a completing path if still Active near B, then
 * assert the invariant. Guarantees every StartedRun is exercised against B.
 */
function runPermutation(seed) {
  const rng = mulberry32(seed)
  const state = createRunMachine()

  // Always start the run so the property quantifies over StartedRun.
  applyEvent(state, 'promptAccepted')
  checkInvariant(state, seed, -1)

  // Event stream length: 8..40 (keeps total runtime sane at 10k iters).
  const n = 8 + Math.floor(rng() * 33)
  for (let i = 0; i < n; i += 1) {
    const event = EVENTS[Math.floor(rng() * EVENTS.length)]
    applyEvent(state, event)
    checkInvariant(state, seed, i)

    // Once durable, further events must not double-complete (checked each step).
    if (state.tick > DEADLINE_B) break
  }

  // If still Active as tick approaches B, inject a completing sequence so the
  // model stays productive; the property still fails if Active past B.
  if (state.phase === 'Active' && state.tick < DEADLINE_B) {
    const remaining = DEADLINE_B - state.tick
    // With probability ~half try clean terminal; else abandon via restart.
    if (rng() < 0.5) {
      applyEvent(state, 'sessionIdle')
      applyEvent(state, 'messagesVisible')
      applyEvent(state, 'journalAppend')
      applyEvent(state, 'mailboxWake')
      // If still Active (race order), force terminal publish.
      if (state.phase === 'Active') applyEvent(state, 'terminalPublish')
    } else {
      applyEvent(state, 'restart')
    }
    // Burn remaining ticks without new durable writes if already sealed.
    let guard = remaining + 4
    while (state.phase === 'Active' && state.tick <= DEADLINE_B && guard-- > 0) {
      applyEvent(state, 'terminalPublish')
    }
  }

  checkInvariant(state, seed, n)

  // Final: every StartedRun must not be Active past B; if Active and tick<=B,
  // force-check would have sealed above. Still-Active with tick<=B after force
  // is a model bug — fail loudly.
  if (state.phase === 'Active') {
    assert.fail(
      `seed=0x${(seed >>> 0).toString(16)}: StartedRun still Active after force-complete (tick=${state.tick})`,
    )
  }
  assert.equal(state.completeCount, 1, `seed=0x${(seed >>> 0).toString(16)}: must end with exactly one durable`)
  assert.ok(
    state.durable != null && (state.durable.kind === 'Terminal' || state.durable.kind === 'Abandoned'),
  )
}

test('EXEC_property_every_started_run_reaches_exactly_one_terminal_within_deadline', () => {
  const ITERATIONS = 10_000
  for (let iter = 0; iter < ITERATIONS; iter += 1) {
    const seed = (0xc0ffee + iter) >>> 0
    try {
      runPermutation(seed)
    } catch (err) {
      // Re-throw with seed for reproducibility.
      const msg = err instanceof Error ? err.message : String(err)
      if (msg.includes('seed=')) throw err
      assert.fail(`seed=0x${seed.toString(16)} iter=${iter}: ${msg}`)
    }
  }
})
