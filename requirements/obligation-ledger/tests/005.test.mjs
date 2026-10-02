import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { readFileSync, readdirSync } = await import("node:fs");
const { join } = await import("node:path");
const journalSurface = await import("../../../dist/Persistence/Journal/Surface.js");
const { acceptAuthorityRoot, withExecutablePlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");
const { integrationTest } = await import("../../verification-system/tests/support/tier-gate.mjs");

// Count the durable TodoCheckpointCommitted facts in the plugin workspace's
// event log — the projection deduping alone cannot prove append idempotence.
const countCheckpointFacts = (directory) => {
  const eventsDir = join(directory, '.git', 'wanxiang', 'events')
  let count = 0
  for (const file of readdirSync(eventsDir)) {
    for (const line of readFileSync(join(eventsDir, file), 'utf8').trim().split('\n')) {
      if (line.includes('TodoCheckpointCommitted')) count += 1
    }
  }
  return count
}

const terminalEvent = (sessionID, callID, status) => ({
  event: {
    type: 'message.part.updated',
    properties: {
      sessionID,
      part: { type: 'tool', tool: 'todowrite', callID, state: { status } },
    },
  },
})

const todoCall = (hooks, sessionID, callID) => {
  const output = { args: { todos: [{ content: 'native todo work', status: 'in_progress', priority: 'high' }] } }
  return hooks['tool.execute.before']({ tool: 'todowrite', sessionID, callID }, output).then(() => output)
}

integrationTest('WHAT[obligation-ledger-005] no checkpoint before the exact completed terminal evidence', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const sessionID = 'ol005-pending'
    const callID = 'ol005-call-pending'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    // before: admission alone commits nothing.
    await todoCall(hooks, sessionID, callID)
    assert.deepEqual(journalSurface.JournalSurface_snapshot(runtime.journal).todoCheckpoints, [], 'before admission commits no checkpoint')

    // after: the Host tool part is still running, so no checkpoint either.
    await hooks['tool.execute.after'](
      { tool: 'todowrite', sessionID, callID, args: { todos: [] } },
      { title: 'todowrite', output: 'Todos updated', metadata: {} },
    )
    assert.deepEqual(journalSurface.JournalSurface_snapshot(runtime.journal).todoCheckpoints, [], 'after with the part still running commits no checkpoint')

    // A non-terminal part update (running status) is not terminal evidence.
    await hooks.event(terminalEvent(sessionID, callID, 'running'))
    assert.deepEqual(journalSurface.JournalSurface_snapshot(runtime.journal).todoCheckpoints, [], 'a running part update commits no checkpoint')

    // A part update for a different tool is not terminal evidence either.
    await hooks.event({
      event: {
        type: 'message.part.updated',
        properties: { sessionID, part: { type: 'tool', tool: 'read', callID, state: { status: 'completed' } } },
      },
    })
    assert.deepEqual(journalSurface.JournalSurface_snapshot(runtime.journal).todoCheckpoints, [], 'a completed part for another tool commits no checkpoint')
  })
})

integrationTest('WHAT[obligation-ledger-005] the exact completed terminal commits exactly one durable checkpoint', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const sessionID = 'ol005-complete'
    const callID = 'ol005-call-complete'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')
    await todoCall(hooks, sessionID, callID)

    await hooks.event(terminalEvent(sessionID, callID, 'completed'))

    const snapshot = journalSurface.JournalSurface_snapshot(runtime.journal)
    assert.deepEqual(snapshot.todoCheckpoints, [
      { sessionId: sessionID, checkpoints: [{ callId: callID }] },
    ], 'the exact completed terminal commits the checkpoint')

    // Repeated completed events for the same exact call must not append a
    // second durable fact — counted in the event log, not just the projection.
    await hooks.event(terminalEvent(sessionID, callID, 'completed'))
    await hooks.event(terminalEvent(sessionID, callID, 'completed'))
    assert.equal(countCheckpointFacts(directory), 1, 'the durable log carries exactly one checkpoint fact for the call')
    assert.deepEqual(
      journalSurface.JournalSurface_snapshot(runtime.journal).todoCheckpoints,
      snapshot.todoCheckpoints,
      'the projection is unchanged by the replays',
    )
  })
})

integrationTest('WHAT[obligation-ledger-005] an error terminal closes the candidate without a checkpoint and without blocking another call', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const sessionID = 'ol005-error'
    const failedCall = 'ol005-call-error'
    const goodCall = 'ol005-call-after-error'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer')

    await todoCall(hooks, sessionID, failedCall)
    await hooks.event(terminalEvent(sessionID, failedCall, 'error'))
    assert.deepEqual(journalSurface.JournalSurface_snapshot(runtime.journal).todoCheckpoints, [], 'an error terminal commits no checkpoint')
    assert.equal(countCheckpointFacts(directory), 0, 'the durable log carries no checkpoint fact for the error call')

    // A repeated error terminal for the same call stays deduplicated.
    await hooks.event(terminalEvent(sessionID, failedCall, 'error'))
    assert.equal(countCheckpointFacts(directory), 0, 'the repeated error terminal stays deduplicated')

    // The failed call does not block a later legal call from completing.
    await todoCall(hooks, sessionID, goodCall)
    await hooks.event(terminalEvent(sessionID, goodCall, 'completed'))
    assert.deepEqual(
      journalSurface.JournalSurface_snapshot(runtime.journal).todoCheckpoints,
      [{ sessionId: sessionID, checkpoints: [{ callId: goodCall }] }],
      'a later legal call completes normally',
    )
    assert.equal(countCheckpointFacts(directory), 1, 'the durable log carries exactly the later call\'s fact')
  })
})

integrationTest('WHAT[obligation-ledger-005] distinct sessions and calls never confuse checkpoint identities', async () => {
  await withExecutablePlugin(async (hooks, directory, _createdIds, runtime) => {
    const first = 'ol005-s1'
    const second = 'ol005-s2'
    await acceptAuthorityRoot(runtime, first, 'engineer')
    await acceptAuthorityRoot(runtime, second, 'engineer')

    // Same call id in two sessions: two independent checkpoints.
    await todoCall(hooks, first, 'shared-call')
    await todoCall(hooks, second, 'shared-call')
    await hooks.event(terminalEvent(first, 'shared-call', 'completed'))
    await hooks.event(terminalEvent(second, 'shared-call', 'completed'))
    assert.equal(countCheckpointFacts(directory), 2, 'the same call id in two sessions appends two facts')

    // Concatenation-collision counterexample: session "ab" + call "c" and
    // session "a" + call "bc" produce the same string when concatenated
    // without a separator. The dedup key must keep them distinct.
    const collideA = 'ol005-ab'
    const collideB = 'ol005-a'
    await acceptAuthorityRoot(runtime, collideA, 'engineer')
    await acceptAuthorityRoot(runtime, collideB, 'engineer')
    await todoCall(hooks, collideA, 'c')
    await todoCall(hooks, collideB, 'bc')
    await hooks.event(terminalEvent(collideA, 'c', 'completed'))
    await hooks.event(terminalEvent(collideB, 'bc', 'completed'))

    const snapshot = journalSurface.JournalSurface_snapshot(runtime.journal)
    const windowA = snapshot.todoCheckpoints.find((entry) => entry.sessionId === collideA)
    const windowB = snapshot.todoCheckpoints.find((entry) => entry.sessionId === collideB)
    assert.ok(windowA, 'the colliding first session keeps its checkpoint')
    assert.ok(windowB, 'the colliding second session keeps its checkpoint')
    assert.deepEqual(windowA.checkpoints, [{ callId: 'c' }])
    assert.deepEqual(windowB.checkpoints, [{ callId: 'bc' }])
    assert.equal(countCheckpointFacts(directory), 4, 'all four identities append their own fact')
  })
})
}

test.todo('WHAT[obligation-ledger-005] a failed durable append is not remembered as committed and the Host retry gets a real second attempt (GAP-190: the settled mark is now removed on append failure in the implementation; a controlled failure-injection fixture for the journal append path is still needed to prove the retry end to end)')
