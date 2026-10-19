import assert from 'node:assert/strict'
import test from 'node:test'
import * as completedTurn from '../../../dist/Interaction/Repair/CompletedTurnSurface.js'
import * as managerWorkflow from '../../../dist/Mission/Manager/WorkflowSurface.js'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import { withReview } from './support/plugin.mjs'
import { acceptAuthorityRoot } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[work-027] Plan empty-stop cannot impersonate an explicit user wait', () => {
  assert.ok(
    typeof completedTurn.needsInteractionRepair === 'function',
    'CompletedTurnSurface.needsInteractionRepair must be exported',
  )

  // Empty prose carries no durable question or completion witness.
  assert.equal(
    completedTurn.needsInteractionRepair('plan', true, 'stop', []),
    true,
    'Plan no longer has a natural-stop exemption',
  )
})

test('WHAT[work-027] other roles keep their empty-stop repair behavior unchanged', () => {
  // The change does not make malformed output acceptable for other roles.
  assert.equal(
    completedTurn.needsInteractionRepair('engineer', true, 'stop', []),
    true,
    'engineer empty-stop must keep requiring interaction repair',
  )
  assert.equal(
    completedTurn.needsInteractionRepair('manager', true, 'stop', []),
    true,
    'manager empty-stop must keep requiring interaction repair',
  )
})

test('WHAT[work-027] provider failures and length stops are not natural stops for plan', () => {
  // Provider errors remain failures; length still needs continuation.
  for (const finish of ['error', 'length']) {
    const planNeeds = completedTurn.needsInteractionRepair('plan', true, finish, [])
    const engineerNeeds = completedTurn.needsInteractionRepair('engineer', true, finish, [])
    assert.equal(
      planNeeds,
      engineerNeeds,
      `finish=${finish}: plan must share the existing recovery classification with other roles`,
    )
  }
})

test('WHAT[work-027] a completed Manager reply with unfinished work receives an explanatory same-run nudge', async () => {
  await withReview(async ({ runtime, session, directory }) => {
    const sent = []
    const port = {
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async (_session, text) => {
        sent.push(text)
        return dispatch.admittedWithPhysicalMessage(`manager-nudge-${sent.length}`)
      },
    }
    const gate = quiescence.create()
    const observe = async run => {
      quiescence.beginAttempt(gate, session)
      const idle = quiescence.observeIdle(gate, session)
      await managerWorkflow.observeIdle(port, runtime.journal, gate, idle,
        session, 'user-root', 'user-root', run, directory)
    }
    const before = dispatch.projectionObservation(runtime.journal, session).activeLogicalRun
    // The production boundary provides TurnCompleted, not merely malformed text.
    await observe('manager-natural-stop-1')
    assert.equal(sent.length, 1)
    assert.match(sent[0], /not been completed|not end that obligation/i)
    assert.match(sent[0], /join/)
    assert.doesNotMatch(sent[0], /\bask\b/i)
    await observe('manager-natural-stop-1')
    assert.equal(sent.length, 1, 'the same terminal occasion cannot send twice')
    await observe('manager-natural-stop-2')
    assert.equal(sent.length, 2, 'a new natural stop cannot spend a permanent one-nudge budget')
    assert.deepEqual(dispatch.projectionObservation(runtime.journal, session).activeLogicalRun, before)
  }, { session: 'manager-long-work-natural-stop' })
})

test('WHAT[work-027] a missing lifecycle projection is not evidence that the Manager goal is complete', async () => {
  await withReview(async ({ runtime, directory }) => {
    const session = 'manager-without-work-projection'
    await acceptAuthorityRoot(runtime, session, 'manager', 'missing-work-root')
    const gate = quiescence.create()
    quiescence.beginAttempt(gate, session)
    let sent = 0
    await managerWorkflow.observeIdle({
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async (_session, text) => {
        sent += 1
        assert.match(text, /current user task/i)
        return dispatch.admittedWithPhysicalMessage('missing-work-nudge')
      },
    }, runtime.journal, gate, quiescence.observeIdle(gate, session),
    session, 'missing-work-root', 'missing-work-root', 'missing-work-stop', directory)
    assert.equal(sent, 1)
  }, { session: 'manager-missing-projection-fixture' })
})
