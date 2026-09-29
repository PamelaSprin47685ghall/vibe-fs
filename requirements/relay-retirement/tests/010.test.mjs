import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[relay-retirement-007] Accepted road cannot reopen new incumbency while certificate is valid', () => {
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1')
  assert.equal(opened.ok, true)
  const assessed = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill('PERFECT'),
  )
  assert.equal(assessed.ok, true)

  const retired = relay.retireAccepted(
    assessed.state,
    'road-1',
    'inc-1',
    'ret-accepted-1',
    'run-1',
    'tool-1',
    'certificate:assessment-1',
    'snapshot-1',
  )
  assert.equal(retired.ok, true)

  // Direct attempt to reopen incumbency while certificate is still valid fails with RoadAlreadyAccepted
  const reopened = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-1', 'authority-1')
  assert.deepEqual(reopened, { ok: false, error: 'RoadAlreadyAccepted' })

  // After explicit invalidation of the certificate, reopen succeeds
  const invalidated = relay.invalidateCertificate(retired.state, 'road-1', 'NewHumanInputAdvancesPhase')
  assert.equal(invalidated.ok, true)
  const reopenedAfterInvalidate = relay.openIncumbency(invalidated.state, 'road-1', 'inc-2', 'snapshot-1', 'authority-1')
  assert.equal(reopenedAfterInvalidate.ok, true)
  assert.equal(relay.view(reopenedAfterInvalidate.state, 'road-1').activeIncumbency, 'inc-2')
})

test('WHAT[relay-retirement-008] physical prompt after Accepted suicide invalidates certificate and unblocks successor manager tools', async () => {
  await withExecutablePlugin(async (hooks, _directory, _children, runtime) => {
    const sessionID = 'ses-accepted-human-successor'
    const rootID = `root-${sessionID}`
    const root = {
      id: rootID,
      role: 'user',
      parts: [{ type: 'text', text: 'Design the capability reuse resolver.' }],
    }
    await acceptAuthorityRoot(runtime, sessionID, 'manager')
    runtime.pushHostMessage(sessionID, root)
    await hooks['chat.message'](
      { sessionID, messageID: rootID, agent: 'manager' },
      { message: root, parts: root.parts },
    )
    const user = { info: { id: rootID, role: 'user', sessionID }, parts: root.parts }
    await hooks['experimental.chat.messages.transform']({ sessionID }, { messages: [user] })

    // Step 1: Submit PERFECT review
    const scores = Object.fromEntries([
      'language_algorithms', 'simplicity', 'structure', 'granularity',
      'tests_evidence', 'logic_reliability_boundaries', 'caller_ergonomics', 'completeness',
    ].map((name) => [name, 'PERFECT']))
    const review = {
      id: 'run-review', role: 'assistant', parentID: rootID,
      parts: [
        { type: 'text', text: 'All criteria perfect.' },
        { type: 'tool', tool: 'review', callID: 'call-review', state: { status: 'pending', input: scores } },
      ],
    }
    runtime.pushHostMessage(sessionID, review)
    const context = (callID, messageID) => ({ sessionID, agent: 'manager', callID, messageID })
    const reviewResult = await hooks.tool.review.execute(scores, context('call-review', review.id))
    assert.match(reviewResult, /recorded = true/)

    // Step 2: Suicide commits Accepted retirement
    const retiredRun = {
      id: 'run-suicide', role: 'assistant', parentID: rootID,
      parts: [{ type: 'tool', tool: 'suicide', callID: 'call-suicide', state: { status: 'pending', input: {} } }],
    }
    runtime.pushHostMessage(sessionID, retiredRun)
    const suicideResult = await hooks.tool.suicide.execute({}, context('call-suicide', retiredRun.id))
    assert.match(suicideResult, /finished = true/)

    // Prior to human input, manager tools are denied (retirement frozen / Accepted certificate)
    const forkDuringRetirement = await hooks.tool.fork.execute(
      { calling: 'engineer', name: 'alice', charge: 'check', delegate_readonly_rounds: 0 },
      context('call-fork-stale', 'msg-stale'),
    )
    assert.match(forkDuringRetirement, /(当前不可用|is not available right now)/)

    // Step 3: Human inputs new instructions in the continuous session
    const humanNext = {
      id: 'human-phase-2', role: 'user',
      parts: [{ type: 'text', text: 'Plan approved. Please start implementation.' }],
    }
    await hooks['chat.message'](
      { sessionID, messageID: humanNext.id, agent: 'manager' },
      { message: humanNext, parts: humanNext.parts },
    )
    const humanRequest = {
      messages: [
        user,
        { info: { id: review.id, role: 'assistant', sessionID }, parts: review.parts },
        { info: { id: retiredRun.id, role: 'assistant', sessionID }, parts: retiredRun.parts },
        { info: { id: humanNext.id, role: 'user', sessionID }, parts: humanNext.parts },
      ],
    }
    await hooks['experimental.chat.messages.transform']({ sessionID }, humanRequest)

    // Step 4: After human input, fork must NOT be denied
    const forkSuccessor = await hooks.tool.fork.execute(
      { calling: 'engineer', name: 'alice', charge: 'implement', delegate_readonly_rounds: 0 },
      context('call-fork-successor', 'msg-successor'),
    )
    assert.doesNotMatch(forkSuccessor, /(当前不可用|is not available right now)/, 'fork must be unblocked after human input advances the continuous session')
  })
})
