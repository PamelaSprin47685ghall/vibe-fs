// port-observation-timing.test.mjs — Runtime observation-timing contract for
// the journal-port adapters (call-time reads, single-commit views, revision
// wait, cancellation, poison/unknown). Every case drives a real AgentJournal
// over a real filesystem EventStore through the production
// Verification/JournalPortObservationSurface.js entries; nothing here re-reads
// adapter source text or replays the fold.

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as surface from '../../../dist/Verification/JournalPortObservationSurface.js'

const withJournalDir = async (tag, scenario) => {
  const dir = mkdtempSync(join(tmpdir(), `wxs-portobs-${tag}-`))
  execFileSync('git', ['init', '--quiet', dir])
  try {
    return await scenario(join(dir, '.git'), tag)
  } finally {
    rmSync(dir, { recursive: true, force: true })
  }
}

test('WHAT[DURABLE-EVENTS-023] EXEC_port_members_read_journal_at_call_time', () =>
  withJournalDir('live-read', (commonDir, tag) =>
    surface.liveReadScenario(commonDir, tag).then((result) => {
      assert.equal(result.folded, true, 'the seeded fact must fold')
      assert.equal(result.openedOk, true, 'the session-opening fact must fold')
      assert.equal(result.pendingBefore, 0, 'no deferred work before the append')
      assert.equal(result.pendingAfter, 1, 'a port built before the append must still read live')
      assert.equal(result.freshAfter, 1, 'a port built after the append reads the same live state')
      assert.equal(result.poisonedBefore, false)
      assert.equal(result.poisonedAfter, false, 'a healthy journal is never poisoned')
      assert.equal(result.stateBefore, false, 'no session state before the opening append')
      assert.equal(result.stateAfter, true, 'ReadView must observe the XTrace slice the opening wrote')
    }),
  ))

test('WHAT[DURABLE-EVENTS-023] EXEC_one_commit_moves_every_related_view_together', () =>
  withJournalDir('same-commit', (commonDir, tag) =>
    surface.sameCommitViewScenario(commonDir, tag).then((result) => {
      assert.equal(result.outcome, 'Ok', `commit must succeed, got ${result.outcome}`)
      assert.equal(result.preMember, false)
      assert.equal(result.preLinked, false)
      assert.equal(result.preState, false)
      // While the physical append is parked mid-commit, every affected member
      // must still read the pre-commit projection — a view that tears would
      // already show one slice advanced.
      assert.equal(result.midMember, false, 'mid-commit member must not see the parked handle')
      assert.equal(result.midLinked, false, 'mid-commit view must not see the parked link')
      assert.equal(result.midState, false, 'mid-commit state must stay absent')
      assert.equal(result.midCompanion, false)
      assert.equal(result.postMember, true, 'after release both handle slices are visible')
      assert.equal(result.postLinked, true)
      assert.equal(result.postState, true, 'ReadView must carry the session state the commit created')
    }),
  ))

test('WHAT[DURABLE-EVENTS-023] EXEC_revision_waiter_wakes_on_next_commit', () =>
  withJournalDir('wait', (commonDir, tag) =>
    Promise.race([
      surface.revisionWaitScenario(commonDir, tag),
      new Promise((_, reject) => setTimeout(() => reject(new Error('revision waiter hung past 8s')), 8000)),
    ]).then((result) => {
      assert.equal(result.committed, 'Ok')
      assert.equal(result.resolved, true, 'the registered waiter must resolve through the commit, not a poll')
      assert.ok(result.changeRevision > 0, 'the woken waiter must carry the new revision')
      assert.equal(result.changeRevision, result.currentRevision, 'the wake revision is the live revision')
      assert.equal(result.observedHandle, true, 'the member reads the committed state after the wake')
    }),
  ))

test('WHAT[DURABLE-EVENTS-023] EXEC_cancelled_waiter_releases_without_stealing_a_commit', () =>
  withJournalDir('cancel', (commonDir, tag) =>
    surface.cancelWaiterScenario(commonDir, tag).then((result) => {
      assert.equal(result.cancelledToNone, true, 'a cancelled waiter resolves to None')
      assert.equal(result.committed, 'Ok')
      assert.equal(result.revisionAdvanced, true, 'the commit still lands and advances revision')
    }),
  ))

test('WHAT[DURABLE-EVENTS-023] EXEC_unknown_append_poisons_and_is_never_confirmed', () =>
  withJournalDir('poison', (commonDir, tag) =>
    surface.poisonedUnknownAppendScenario(commonDir, tag).then((result) => {
      assert.equal(result.seededOk, true)
      assert.ok(result.failedOutcome.startsWith('Unknown:'), `uncertain append must report unknown, got ${result.failedOutcome}`)
      assert.equal(result.poisoned, true, 'the port must observe the poisoned writer')
      assert.ok(result.afterOutcome.startsWith('Poisoned:'), `a poisoned writer must refuse later appends, got ${result.afterOutcome}`)
    }),
  ))
