import assert from 'node:assert/strict'
import {mkdtempSync, rmSync} from 'node:fs'
import {tmpdir} from 'node:os'
import {join} from 'node:path'
import test from 'node:test'
import * as journalSurface from '../../../dist/Persistence/Journal/Surface.js'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'
import * as workspaceSurface from '../../../dist/OpenCode/Host/WorkspaceEventStoreSurface.js'
import * as hostSurface from '../../../dist/Change/Host/Surface.js'
import * as managerWorkflow from '../../../dist/Mission/Manager/WorkflowSurface.js'
import {withReview, scores} from './support/plugin.mjs'
import {withSuccessor, messageId} from './support/cut.mjs'
import { withExecutablePlugin, acceptAuthorityRoot, injectAcceptedAssessment, lifecycleWorkView } from '../../verification-system/tests/support/plugin-fixture.mjs'

const life = lifecycle.LifecycleSurface ?? lifecycle

test('WHAT[work-011] a Delivered deliver records the established delivery, and the next transform suppresses the request, interrupts the attempt and dispatches no successor', async () => {
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
    // 交付已成立是提交面事实：退休落盘即登记精确 attempt 围栏。物理中断
    // 不再发生在工具内部——停止由 transform 钩子的交付裁决执行。
    const delivery = await hooks.tool.deliver.execute({}, retire)
    assert.match(delivery, /Finished\. Do not continue work\./)
    assert.deepEqual(runtime.abortedIds, [], 'the deliver tool does not interrupt the attempt')
    const committed = await lifecycleWorkView(runtime, session)
    assert.equal(committed.delivered, true, 'the delivery is committed')
    assert.equal(committed.active, false, 'a final delivery has no successor tenure')
    assert.equal(runtime.prompts.length, 0)

    // 下一次请求按已成立的交付裁决：不再放行给 provider（LLM 看不到 deliver
    // 回复）、中断本次 attempt，且不派发任何后继。
    const output = {messages: [
      {info: {id: 'user-root', role: 'user', sessionID: session, model: {providerID: 'provider', modelID: 'manager-model'}}, parts: [{type: 'text', text: 'Deliver the requested behavior and verification.'}]},
      {info: {id: 'review-run', role: 'assistant', parentID: 'user-root', time: {created: 2, completed: 2}}, parts: [{type: 'text', text: 'review closed'}]},
      {info: {id: 'retirement-run', role: 'assistant', parentID: 'user-root', time: {created: 3}}, parts: [{type: 'text', text: 'Old closing tail'}]},
    ]}
    await hooks['experimental.chat.messages.transform']({sessionID: session}, output)
    assert.deepEqual(output.messages, [], 'the request carries no content: the model never sees the deliver reply')
    assert.deepEqual(runtime.abortedIds, [session], 'the transform interrupts the current attempt')
    assert.equal(runtime.prompts.length, 0, 'the interrupted attempt dispatches no successor')
    const stopped = await lifecycleWorkView(runtime, session)
    assert.equal(stopped.delivered, true, 'the established delivery survives the stop')
    assert.equal(stopped.active, false, 'a final delivery has no successor tenure')
  })
})

for (const failure of ['rejected', 'thrown']) {
  test(`WHAT[work-011] a ${failure} Host interrupt leaves the committed delivery established and is not reported as a failed delivery`, async () => {
    await withReview(async ({execute, hooks, runtime, session}) => {
      await execute(scores('PERFECT'))
      runtime.pushHostMessage(session, {
        info: {id: 'final-run', role: 'assistant', sessionID: session, parentID: 'user-root', time: {created: 3}},
        parts: [{type: 'tool', tool: 'deliver', callID: 'final-call', state: {status: 'pending', input: {}}}],
      })
      await hooks.tool.deliver.execute({}, {
        sessionID: session, agent: 'manager', messageID: 'confirmation-run', callID: 'confirmation-call',
      })
      const reason = `terminal-interrupt-${failure}`
      let interruptAttempts = 0
      runtime.client.session.abort = async () => {
        interruptAttempts += 1
        if (failure === 'thrown') throw new Error(reason)
        return {error: reason}
      }
      // 「已提交」与「中断未确认」是两个分别取证的事实：交付落盘不因后来的
      // 中断失败而消失，工具本身也不再因中断失败而失败。
      const delivery = await hooks.tool.deliver.execute({}, {
        sessionID: session, agent: 'manager', messageID: 'final-run', callID: 'final-call',
      })
      assert.match(delivery, /Finished\. Do not continue work\./)
      const work = await lifecycleWorkView(runtime, session)
      assert.equal(work.delivered, true, 'a failed physical interrupt must not undo committed delivery')
      assert.equal(work.active, false, 'interrupt failure must not create a successor')
      assert.equal(runtime.prompts.length, 0)

      // 中断在 transform 钩子处发出：发出被登记，未被确认也被登记；两者都
      // 不把已提交的交付呈现为失败，本次请求仍被抑制。
      const output = {messages: [
        {info: {id: 'user-root', role: 'user', sessionID: session, model: {providerID: 'provider', modelID: 'manager-model'}}, parts: [{type: 'text', text: 'Deliver the requested behavior and verification.'}]},
        {info: {id: 'final-run', role: 'assistant', sessionID: session, parentID: 'user-root', time: {created: 3}}, parts: [{type: 'text', text: 'final delivery'}]},
      ]}
      await assert.doesNotReject(
        () => hooks['experimental.chat.messages.transform']({sessionID: session}, output),
        'an unconfirmed interrupt must not present the committed delivery as a failure',
      )
      assert.equal(interruptAttempts, 1, 'the interrupt was issued once for this attempt')
      assert.deepEqual(output.messages, [], 'the request is still suppressed when the interrupt is not confirmed')
      const after = await lifecycleWorkView(runtime, session)
      assert.equal(after.delivered, true, 'the delivery stays established when the interrupt is not confirmed')
      assert.equal(after.active, false, 'an unconfirmed interrupt must not create a successor')
      assert.equal(runtime.prompts.length, 0, 'no successor is dispatched')
    }, {session: `final-interrupt-${failure}`})
  })
}

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
    // The relay keeps the predecessor's physical history: context retention
    // belongs to the view declaration, not to the successor hand-off.
    assert.ok(output.messages.some(message => messageId(message) === 'user-root'))
    assert.ok(output.messages.some(message => messageId(message) === messageId(gate)))
    assert.ok(output.messages.some(message => messageId(message) === 'review-run'))
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
    assert.equal((await lifecycleWorkView(runtime, session)).active, true)
    await assert.rejects(
      execute(scores('PERFECT')),
      (error) => {
        assert.match(String(error && error.message ? error.message : error), /owner cannot review/i)
        return true
      },
    )
    // The refusal wrote nothing: the tenure stays active and unreviewed.
    const afterTool = await lifecycleWorkView(runtime, session)
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
    assert.equal((await lifecycleWorkView(runtime, session)).reviewed, '')
  }, {ownerId: 'manager-reviewer', session: 'review-manager-self-review'})
})

test('WHAT[work-011] Stayed deliver enqueues the successor manager-assess prompt before returning so no LLM request can slip in between', async () => {
  await withReview(async ({execute, hooks, runtime, session}) => {
    assert.match(await execute(scores('REVISE')), /recorded = true/)
    const retirement = {
      info: {id: 'retirement-run', role: 'assistant', sessionID: session, parentID: 'user-root', time: {created: 3}},
      parts: [{type: 'tool', tool: 'deliver', callID: 'deliver-call', state: {status: 'pending', input: {}}}],
    }
    runtime.pushHostMessage(session, retirement)
    const confirm = {sessionID: session, callID: 'deliver-confirm', messageID: 'retirement-confirm', providerRunID: 'run-confirm', agent: 'manager'}
    const retire = {sessionID: session, callID: 'deliver-call', messageID: 'retirement-run', providerRunID: 'run-retire', agent: 'manager'}
    assert.match(await hooks.tool.deliver.execute({}, confirm), /confirmation_required = true/)
    assert.equal(runtime.prompts.length, 0, 'first confirmation does not enqueue successor')

    let promptsAtReturn = -1
    const deliverPromise = hooks.tool.deliver.execute({}, retire).then((res) => {
      promptsAtReturn = runtime.prompts.filter(p => p.sessionID === session).length
      return res
    })
    const result = await deliverPromise
    // 条款 5：交接回执必须说明「已接力 + 后继要做什么」，因此本用例不断言
    // 回执措辞，只断言交接事实成立、本任不再继续。
    assert.doesNotMatch(result, /confirmation_required = true/, 'the second deliver hands the work over instead of confirming again')
    assert.equal(promptsAtReturn, 1, 'successor prompt must already be enqueued when deliver resolves')
    const promptText = runtime.prompts[0]?.body?.parts?.find(p => p?.type === 'text')?.text ?? ''
    assert.match(promptText, /(你是接手此任务的第 2 个 Manager|You are the 2 Manager taking over this mission)/)
    const handedOver = await lifecycleWorkView(runtime, session)
    assert.equal(handedOver.active, true, 'the hand-off opened the successor tenure: this tenure does not continue')
    assert.equal(handedOver.delivered, false, 'a hand-off is not a final delivery')
  }, {session: 'review-manager-atomic-enqueue'})
})

// ── The successor-prompt occasion, read from the fold alone ────────────────
// WHAT[work-011]: the occasion is derived from the durable fold — sequence S4,
// an open tenure, and a committed retirement that the open tenure has not
// reviewed yet. `successorPromptContextOf` is the read-only probe over the same
// predicate the dispatch reads; it dispatches nothing.

const withSeededManagerWork = async (outcome, body) => {
  const root = mkdtempSync(join(tmpdir(), 'wxs-successor-context-'))
  const stateRoot = await journalSurface.JournalSurface_initializeStateRoot(root)
  const store = workspaceSurface.acquire(stateRoot)
  try {
    const sessionId = `manager-${outcome ?? 'first'}`
    const opened = await life.seedOpen(store, 'S4', sessionId, root, 0n)
    assert.equal(opened.ok, true, opened.error)
    if (outcome != null) {
      const reviewed = await life.seedReview(store, sessionId, outcome)
      assert.equal(reviewed.ok, true, reviewed.error)
      const confirmed = await life.seedDeliver(store, sessionId, 'run-1', 'call-1', 1n)
      assert.equal(confirmed.ok, true, confirmed.error)
      await life.seedDeliver(store, sessionId, 'run-2', 'call-2', 2n)
    }
    await body({root, stateRoot, store, sessionId, view: life.inspected(stateRoot, sessionId)})
  } finally {
    workspaceSurface.release(stateRoot)
    rmSync(root, {recursive: true, force: true})
  }
}

const successorPromptOf = async (root, sessionId) => managerWorkflow.successorPromptContextOf(root, sessionId)

test('WHAT[work-011] a first tenure with no retirement owes no successor prompt', async () => {
  await withSeededManagerWork(null, async ({root, sessionId}) => {
    const context = await successorPromptOf(root, sessionId)
    assert.equal(context.present, false, 'the first tenure has no predecessor to hand over')
    assert.equal(context.retirementId, '')
    assert.equal(context.providerRunId, '')
  })
})

test('WHAT[work-011] a Relayed retirement owes the successor prompt until that successor is reviewed', async () => {
  await withSeededManagerWork('Stayed', async ({root, store, sessionId, view}) => {
    const retirement = Array.from(view.Retirements ?? []).at(-1)
    assert.equal(retirement.Outcome, 'Relayed')
    assert.equal(view.ActiveId.length > 0, true, 'the relay opened its successor in the same record')
    assert.equal(view.Reviewed, false, 'the successor starts unreviewed')

    const context = await successorPromptOf(root, sessionId)
    assert.equal(context.present, true, 'the unreviewed successor owes the successor prompt')
    assert.equal(context.retirementId, retirement.RetirementId, 'the occasion is named by the retirement')
    assert.equal(context.providerRunId, 'run-2', 'the cut carries the provider run that made the retirement')

    const reviewed = await life.seedReview(store, sessionId, 'Stayed')
    assert.equal(reviewed.ok, true, reviewed.error)
    assert.equal((await successorPromptOf(root, sessionId)).present, false, 'a reviewed successor owes no successor prompt')
  })
})

test('WHAT[work-011] the delivered hand-off reopens the occasion on the unreviewed next tenure', async () => {
  await withSeededManagerWork('Promoted', async ({root, sessionId, view}) => {
    const delivered = Array.from(view.Retirements ?? []).at(-1)
    assert.equal(delivered.Outcome, 'Delivered')
    assert.equal(view.ActiveId ?? null, null, 'a delivery leaves no open tenure')
    assert.equal((await successorPromptOf(root, sessionId)).present, false, 'a delivered work owes no successor prompt')

    const receipt = await hostSurface.handOffDeliveredTenure(root, sessionId, 'hand-off:successor-context', 3)
    assert.equal(receipt.ok, true, receipt.error)
    assert.equal(receipt.kind, 'HandOff')

    const context = await successorPromptOf(root, sessionId)
    assert.equal(context.present, true, 'the reopened tenure owes the successor prompt')
    assert.equal(context.retirementId, delivered.RetirementId, 'the delivered retirement is the cut')
    assert.equal(context.providerRunId, 'run-2', 'the cut carries the delivering provider run')
  })
})
