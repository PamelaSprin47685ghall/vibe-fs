import assert from 'node:assert/strict'
import test from 'node:test'
import {withReview, scores} from './support/plugin.mjs'
import {withSuccessor, messageId} from './support/cut.mjs'
import { withExecutablePlugin, acceptAuthorityRoot, injectAcceptedAssessment, lifecycleWorkView } from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[work-011] actual Promoted deliver confirms then retires without abort and dispatches no successor', async () => {
  await withReview(async ({execute, hooks, runtime, session}) => {
    assert.match(await execute(scores('PERFECT')), /recorded = true/)
    const retirement = {
      info: {id: 'retirement-run', role: 'assistant', sessionID: session, parentID: 'user-root', time: {created: 3}},
      parts: [{type: 'tool', tool: 'deliver', callID: 'deliver-call', state: {status: 'pending', input: {}}}],
    }
    runtime.pushHostMessage(session, retirement)
    const confirm = {sessionID: session, callID: 'deliver-confirm', messageID: 'retirement-confirm', providerRunID: 'run-confirm', agent: 'manager'}
    const retire = {sessionID: session, callID: 'deliver-call', messageID: 'retirement-run', providerRunID: 'run-retire', agent: 'manager'}
    // First call only confirms. It does not retire and it does not abort.
    const confirmation = await hooks.tool.deliver.execute({}, confirm)
    assert.match(confirmation, /confirmation_required = true/)
    assert.match(confirmation, /finished = false/)
    assert.deepEqual(runtime.abortedIds, [])
    // A different callID retires. Promoted writes Delivered and starts no successor.
    const result = await hooks.tool.deliver.execute({}, retire)
    assert.match(result, /Finished\. Do not continue work\./)
    assert.deepEqual(runtime.abortedIds, [])
    assert.equal(runtime.prompts.length, 0)
    const output = {messages: [
      {info: {id: 'user-root', role: 'user', sessionID: session, model: {providerID: 'provider', modelID: 'manager-model'}}, parts: [{type: 'text', text: 'Deliver the requested behavior and verification.'}]},
      {info: {id: 'review-run', role: 'assistant', parentID: 'user-root', time: {created: 2, completed: 2}}, parts: [{type: 'text', text: 'review closed'}]},
      {info: {id: 'retirement-run', role: 'assistant', parentID: 'user-root', time: {created: 3}}, parts: [{type: 'text', text: 'Old closing tail'}]},
    ]}
    await hooks['experimental.chat.messages.transform']({sessionID: session}, output)
    // Delivered has no successor. The transform must not stop this attempt
    // and must not abort the session.
    assert.notEqual(output.messages.length, 0)
    assert.deepEqual(runtime.abortedIds.filter(id => id === session), [])
  })
})

test('WHAT[work-011] actual Stayed deliver starts one same-session successor, the next transform stops the retired attempt, and the successor transform retains the old physical history without another abort', async () => {
  await withSuccessor(async ({hooks, runtime, session, history, gate}) => {
    // withSuccessor is the two-stage Stayed path: first call confirms, the
    // second call with a different callID retires Relayed, and that retirement
    // already stopped the retired attempt and started exactly one successor.
    // The stop clears the request; a root session is not aborted.
    assert.deepEqual(runtime.abortedIds.filter(id => id === session), [])
    assert.equal(runtime.prompts.filter(p => p.sessionID === session).length, 1)
    const output = {messages: [...structuredClone(history), gate]}
    await hooks['experimental.chat.messages.transform']({sessionID: session}, output)
    const successorParamsOut = { temperature: 0, options: {} }
    await assert.doesNotReject(
      () => Promise.resolve(hooks['chat.params']({ sessionID: session, messageID: messageId(gate), agent: 'manager' }, successorParamsOut)),
      'chat.params on the successor manager turn must pass with its committed lease',
    )
    assert.equal(successorParamsOut.temperature, 1)

    // And if a second Stayed retirement later stops the successor attempt, the
    // superseded successor's physical message also passes chat.params without PROMPT-006.
    const supersededParamsOut = { temperature: 0, options: {} }
    await hooks['chat.message'](
      { sessionID: session, messageID: 'next-successor-msg', agent: 'manager' },
      {
        message: { id: 'next-successor-msg', sessionID: session, role: 'user', agent: 'manager' },
        parts: [{ type: 'text', text: 'next turn' }],
      },
    )
    await assert.doesNotReject(
      () => Promise.resolve(hooks['chat.params']({ sessionID: session, messageID: messageId(gate), agent: 'manager' }, supersededParamsOut)),
      'chat.params on a superseded retired attempt must not throw PROMPT-006',
    )
    // WHAT[008] strips the predecessor's assistant and tool from the successor.
    // The user root and the successor prompt stay.
    assert.ok(output.messages.some(message => messageId(message) === 'user-root'))
    assert.ok(output.messages.some(message => messageId(message) === messageId(gate)))
    assert.equal(output.messages.some(message => messageId(message) === 'review-run'), false)
    // The successor keeps the user root, strips the predecessor assistant, and does not abort.
    assert.deepEqual(runtime.abortedIds.filter(id => id === session), [])
    assert.equal(runtime.prompts.filter(p => p.sessionID === session).length, 1)
  })
})

test('WHAT[work-011] physical prompt after Accepted deliver invalidates certificate and unblocks successor manager tools', async () => {
  const { withExecutablePlugin, acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
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
    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-root', role: 'assistant', sessionID, parentID: rootID, time: { created: 1 } },
      parts: [],
    })
    await hooks['experimental.chat.messages.transform']({ sessionID }, { messages: [user] })

    // Step 1: a non-empty review is Stayed, so the second deliver Relays a successor.
    const findings = { findings: [{ acceptance_criteria: 'the target state is not yet reached', work_plan: 'close the remaining gap' }] }
    const review = {
      id: 'run-review', role: 'assistant', parentID: rootID, time: { created: 2 },
      parts: [
        { type: 'text', text: 'All criteria perfect.' },
        { type: 'tool', tool: 'review', callID: 'call-review', state: { status: 'pending', input: findings } },
      ],
    }
    runtime.pushHostMessage(sessionID, review)
    const context = (callID, messageID, providerRunID = `run-${callID}`) => ({ sessionID, agent: 'manager', callID, messageID, providerRunID })
    const reviewResult = await hooks.tool.review.execute(findings, context('call-review', review.id))
    assert.match(reviewResult, /recorded = true/)

    // Step 2: deliver commits Accepted retirement
    const retiredRun = {
      id: 'run-deliver', role: 'assistant', parentID: rootID, time: { created: 3 },
      parts: [{ type: 'tool', tool: 'deliver', callID: 'call-deliver', state: { status: 'pending', input: {} } }],
    }
    runtime.pushHostMessage(sessionID, retiredRun)
    const confirmation = await hooks.tool.deliver.execute({}, context('call-deliver-confirm', 'confirm-run', 'run-confirm'))
    assert.match(confirmation, /confirmation_required = true/)
    assert.match(confirmation, /finished = false/)
    const deliverResult = await hooks.tool.deliver.execute({}, context('call-deliver', retiredRun.id, 'run-retire'))
    assert.match(deliverResult, /Finished\. Do not continue work\./)

    // Step 3: Human inputs new instructions in the continuous session.
    // A Relayed successor is a new tenure, so the retirement freeze does not
    // carry over; the denial the old certificate freeze asserted no longer exists.
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
        { info: { id: review.id, role: 'assistant', sessionID, parentID: rootID, time: { created: 2, completed: 2 } }, parts: review.parts },
        { info: { id: retiredRun.id, role: 'assistant', sessionID, parentID: rootID, time: { created: 3, completed: 3 } }, parts: retiredRun.parts },
        { info: { id: humanNext.id, role: 'user', sessionID }, parts: humanNext.parts },
      ],
    }
    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-human-successor', role: 'assistant', sessionID, parentID: humanNext.id, time: { created: 4 } },
      parts: [],
    })
    await hooks['experimental.chat.messages.transform']({ sessionID }, humanRequest)

    // Step 4: After human input, fork must NOT be denied
    const forkSuccessor = await hooks.tool.fork.execute(
      { calling: 'engineer', name: 'alice', charge: 'implement' },
      context('call-fork-successor', 'msg-successor'),
    )
    assert.doesNotMatch(forkSuccessor, /(当前不可用|is not available right now)/, 'fork must be unblocked after human input advances the continuous session')
  })
})

test.todo('WHAT[work-011] a controlled in-flight Host interrupt prevents successor dispatch until exact provider-step release and physical interruption complete')

test('WHAT[work-011] a post-cut wake after a fresh successor root is cleared without session abort and without MANAGER-LOOP-004 crash', async () => {
  await withExecutablePlugin(async (hooks, _directory, _children, runtime) => {
    const sessionID = 'ses-late-retired-after-successor'
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
    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-root', role: 'assistant', sessionID, parentID: rootID, time: { created: 1 } },
      parts: [],
    })
    await hooks['experimental.chat.messages.transform']({ sessionID }, { messages: [user] })

    // Step 1: a non-empty review is Stayed, so the second deliver Relays a successor.
    const findings = { findings: [{ acceptance_criteria: 'the target state is not yet reached', work_plan: 'close the remaining gap' }] }
    const review = {
      id: 'run-review', role: 'assistant', parentID: rootID, time: { created: 2 },
      parts: [
        { type: 'text', text: 'All criteria perfect.' },
        { type: 'tool', tool: 'review', callID: 'call-review', state: { status: 'pending', input: findings } },
      ],
    }
    runtime.pushHostMessage(sessionID, review)
    const context = (callID, messageID, providerRunID = `run-${callID}`) => ({ sessionID, agent: 'manager', callID, messageID, providerRunID })
    const reviewResult = await hooks.tool.review.execute(findings, context('call-review', review.id))
    assert.match(reviewResult, /recorded = true/)

    // Step 2: deliver commits retirement (with two-stage confirmation)
    const retiredRun = {
      id: 'run-deliver', role: 'assistant', parentID: rootID, time: { created: 3 },
      parts: [{ type: 'tool', tool: 'deliver', callID: 'call-deliver', state: { status: 'pending', input: {} } }],
    }
    runtime.pushHostMessage(sessionID, retiredRun)
    const confirmation = await hooks.tool.deliver.execute({}, context('call-deliver-confirm', 'confirm-run', 'run-confirm'))
    assert.match(confirmation, /confirmation_required = true/)
    assert.match(confirmation, /finished = false/)
    const deliverResult = await hooks.tool.deliver.execute({}, context('call-deliver', retiredRun.id, 'run-retire'))
    assert.match(deliverResult, /Finished\. Do not continue work\./)

    // Step 3: Human successor input activates a fresh authority root, so the
    // root session is no longer lifecycle-terminated and MANAGED-SESSION-016
    // refuses automation interrupts.
    const humanNext = {
      id: 'human-phase-2', role: 'user',
      parts: [{ type: 'text', text: 'Plan approved. Please start implementation.' }],
    }
    await hooks['chat.message'](
      { sessionID, messageID: humanNext.id, agent: 'manager' },
      { message: humanNext, parts: humanNext.parts },
    )
    runtime.pushHostMessage(sessionID, {
      info: { id: 'run-human-successor', role: 'assistant', sessionID, parentID: humanNext.id, time: { created: 4 } },
      parts: [],
    })

    // Step 4: A post-cut wake that was never admitted carries no ChatExecution,
    // so the provider-step gate passes it through and the relay transform must
    // stop it as a stale retired attempt. Per WHAT[work-011] no
    // old-term session abort may be re-issued after successor dispatch, and per
    // WHAT[managed-session-lifecycle-016] the guard rightly refuses the
    // interrupt: the wake is cleared without crashing the manager loop with
    // MANAGER-LOOP-004.
    const wake = {
      info: { id: 'wake-after-successor', role: 'user', sessionID },
      parts: [{ type: 'text', text: 'runtime/manager-assess stray wake' }],
    }
    const strayWake = { messages: [
      user,
      { info: { id: review.id, role: 'assistant', sessionID, parentID: rootID, time: { created: 2, completed: 2 } }, parts: review.parts },
      { info: { id: retiredRun.id, role: 'assistant', sessionID, parentID: rootID, time: { created: 3, completed: 3 } }, parts: retiredRun.parts },
      wake,
    ] }
    const abortsBefore = runtime.abortedIds.length
    await hooks['experimental.chat.messages.transform']({ sessionID }, strayWake)
    assert.deepEqual(strayWake.messages, [])
    assert.equal(runtime.abortedIds.length, abortsBefore)
  })
})

test('WHAT[work-011] a review whose reviewer identity is the incumbency owner is refused by the kernel self-review gate', async () => {
  // The Manager backend reviews as the fixed manager-reviewer identity. Seed the
  // incumbency with that same identity as owner: the reviewer equals the owner,
  // and the kernel refuses the self-review instead of recording it.
  await withReview(async ({execute, runtime, session}) => {
    assert.equal(lifecycleWorkView(runtime, session).active, true)
    await assert.rejects(
      execute(scores('PERFECT')),
      (error) => {
        assert.match(String(error && error.message ? error.message : error), /owner cannot review/i)
        return true
      },
    )
    // The refusal wrote nothing: the tenure stays active and unreviewed.
    const afterTool = lifecycleWorkView(runtime, session)
    assert.equal(afterTool.reviewed, '')
    assert.equal(afterTool.active, true)
    // The seeding path with the explicit reviewer identity is refused by the
    // same gate, so an identity that never reaches the kernel would fail here.
    await assert.rejects(
      injectAcceptedAssessment(runtime, session, undefined, 'Promoted', 'manager-reviewer'),
      (error) => {
        assert.match(String(error && error.message ? error.message : error), /owner cannot review/i)
        return true
      },
    )
    assert.equal(lifecycleWorkView(runtime, session).reviewed, '')
  }, {ownerId: 'manager-reviewer'})
})
