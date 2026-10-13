import assert from 'node:assert/strict'
import test from 'node:test'
import {withReview, scores} from './support/plugin.mjs'
import {withSuccessor, messageId} from './support/cut.mjs'
import { withExecutablePlugin, acceptAuthorityRoot } from '../../verification-system/tests/support/plugin-fixture.mjs'

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
      {info: {id: 'retirement-run', role: 'assistant'}, parts: [{type: 'text', text: 'Old closing tail'}]},
    ]}
    await hooks['experimental.chat.messages.transform']({sessionID: session}, output)
    // Delivered has no successor. The transform must not stop this attempt
    // and must not abort the session.
    assert.notEqual(output.messages.length, 0)
    assert.deepEqual(runtime.abortedIds, [])
  })
})

test('WHAT[work-011] actual Stayed deliver starts one same-session successor, the next transform stops the retired attempt, and the successor transform retains the old physical history without another abort', async () => {
  await withSuccessor(async ({hooks, runtime, session, history, gate}) => {
    // withSuccessor is the two-stage Stayed path: first call confirms, the
    // second call with a different callID retires Relayed, and that retirement
    // already stopped the retired attempt once and started exactly one successor.
    assert.deepEqual(runtime.abortedIds, [session])
    assert.equal(runtime.prompts.filter(p => p.sessionID === session).length, 1)
    const output = {messages: [...structuredClone(history), gate]}
    await hooks['experimental.chat.messages.transform']({sessionID: session}, output)
    for (const prior of history) {
      assert.ok(output.messages.some(message => messageId(message) === messageId(prior)), `lost physical message ${messageId(prior)}`)
    }
    assert.ok(output.messages.some(message => messageId(message) === messageId(gate)))
    // The successor transform keeps the old physical history and does not abort again.
    assert.deepEqual(runtime.abortedIds, [session])
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

    // Step 1: Submit PERFECT review
    const findings = { findings: [] }
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

    // Prior to human input, manager tools are denied (retirement frozen / Accepted certificate)
    const forkDuringRetirement = await hooks.tool.fork.execute(
      { calling: 'engineer', name: 'alice', charge: 'check' },
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

    // Step 1: Submit review with valid findings
    const findings = { findings: [] }
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
      { info: { id: review.id, role: 'assistant', sessionID }, parts: review.parts },
      { info: { id: retiredRun.id, role: 'assistant', sessionID }, parts: retiredRun.parts },
      wake,
    ] }
    const abortsBefore = runtime.abortedIds.length
    await hooks['experimental.chat.messages.transform']({ sessionID }, strayWake)
    assert.deepEqual(strayWake.messages, [])
    assert.equal(runtime.abortedIds.length, abortsBefore)
  })
})
