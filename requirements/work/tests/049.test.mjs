import assert from 'node:assert/strict'
import test from 'node:test'
import { withReview } from './support/plugin.mjs'
import { withRestartablePlugin, acceptAuthorityRoot, openIncumbency } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { projectionObservation, userQuestionObservation } from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as managerWorkflow from '../../../dist/Mission/Manager/WorkflowSurface.js'
import * as quiescence from '../../../dist/OpenCode/Host/QuiescenceSurface.js'

// 通过真实插件/日志组合验证；Host 双件只控制通知和中断观察。
const question = 'Which deployment environment should this task target?'
const context = (session, call = 'ask-call', run = 'ask-run') => ({
  sessionID: session, agent: 'manager', callID: call, messageID: run,
})
const completedAsk = (session, call = 'ask-call', run = 'ask-run', status = 'completed') => ({
  event: {
    type: 'message.part.updated',
    properties: {
      part: {
        type: 'tool', sessionID: session, messageID: run, tool: 'ask', callID: call,
        state: { status, input: { question }, output: 'Your question has been sent.' },
      },
    },
  },
})
const humanInput = async (hooks, runtime, session, id, text) => {
  const message = { id, role: 'user', agent: 'manager', sessionID: session,
    model: { providerID: 'provider', modelID: 'manager-model' } }
  runtime.pushHostMessage(session, { info: { ...message, time: { created: 20 } }, parts: [{ type: 'text', text }] })
  await hooks['chat.message']({ sessionID: session, agent: 'manager', messageID: id }, {
    message, parts: [{ type: 'text', text }],
  })
}

test('WHAT[work-049] ask acknowledges a human question before interrupting and does not dispatch DevOps', async () => {
  await withReview(async ({ hooks, runtime, session, created, directory }) => {
    const notices = []
    runtime.client.tui = { showToast: async ({ body }) => { notices.push(body); return {} } }
    const before = projectionObservation(runtime.journal, session).activeLogicalRun
    const reply = await hooks.tool.ask.execute({ question }, context(session))
    assert.match(reply, /question has been sent/i)
    assert.equal(notices.length, 1)
    assert.equal(notices[0].message, question)
    assert.equal(created.length, 0, 'human ask must not create a DevOps session')
    assert.deepEqual(runtime.abortedIds, [], 'tool body must return before a physical interrupt')
    assert.equal(userQuestionObservation(runtime.journal, session)[0].answer, null)
    assert.deepEqual(projectionObservation(runtime.journal, session).activeLogicalRun, before)

    await hooks.event(completedAsk(session, 'ask-call', 'ask-run', 'running'))
    await hooks.event(completedAsk(session, 'another-call'))
    await hooks.event(completedAsk(session, 'ask-call', 'another-provider-run'))
    await hooks.event(completedAsk('another-session'))
    assert.deepEqual(runtime.abortedIds, [], 'an unrelated or unfinished result is not a pause witness')
    await hooks.event(completedAsk(session))
    assert.deepEqual(runtime.abortedIds, [session])
    assert.deepEqual(projectionObservation(runtime.journal, session).activeLogicalRun, before,
      'interrupt is not logical-run completion')
    assert.equal(userQuestionObservation(runtime.journal, session).length, 1)
    const gate = quiescence.create()
    quiescence.beginAttempt(gate, session)
    await managerWorkflow.observeIdle({
      SubscribeTerminal: () => ({ Dispose() {} }),
      SendPrompt: async () => { assert.fail('an explicit human wait must not be nudged') },
    }, runtime.journal, gate, quiescence.observeIdle(gate, session),
    session, 'user-root', 'user-root', 'ask-run', directory)
  }, { session: 'ask-human-confirmation' })
})

test('WHAT[work-049] exact ask replay is idempotent and waiting blocks both registered and native tools', async () => {
  await withReview(async ({ hooks, runtime, session }) => {
    let shown = 0
    runtime.client.tui = { showToast: async () => { shown += 1; return {} } }
    const first = await hooks.tool.ask.execute({ question }, context(session))
    assert.equal(await hooks.tool.ask.execute({ question }, context(session)), first)
    assert.equal(shown, 1)
    await hooks.event(completedAsk(session))
    await hooks.event(completedAsk(session))
    assert.deepEqual(runtime.abortedIds, [session])
    const conflict = await hooks.tool.ask.execute({ question: 'A different question' }, context(session))
    assert.match(conflict, /could not be acknowledged/i)
    const second = await hooks.tool.ask.execute({ question: 'One more question' }, context(session, 'ask-second'))
    assert.match(second, /already waiting/i)
    assert.equal(shown, 1)
    const denied = await hooks.tool.review.execute({ findings: [] }, context(session, 'review-during-wait'))
    assert.match(denied, /waiting for the user/i)
    await assert.rejects(
      hooks['tool.execute.before']({ sessionID: session, tool: 'read', callID: 'native-read' }, { args: {} }),
      /waiting for the user/i,
    )
  }, { session: 'ask-human-replay' })
})

test('WHAT[work-049] missing or failed notification does not create a user wait', async () => {
  await withReview(async ({ hooks, runtime, session }) => {
    const absent = await hooks.tool.ask.execute({ question }, context(session))
    assert.match(absent, /could not be acknowledged/i)
    assert.deepEqual(userQuestionObservation(runtime.journal, session), [])
    runtime.client.tui = { showToast: async () => { throw new Error('notification unavailable') } }
    const failed = await hooks.tool.ask.execute({ question }, context(session))
    assert.match(failed, /notification unavailable/)
    await hooks.event(completedAsk(session))
    assert.deepEqual(runtime.abortedIds, [])
    assert.deepEqual(userQuestionObservation(runtime.journal, session), [])
  }, { session: 'ask-human-notify-failure' })
})

test('WHAT[work-049] blank and concurrent questions do not create duplicate notifications', async () => {
  await withReview(async ({ hooks, runtime, session }) => {
    let release
    let announced
    const entered = new Promise(resolve => { announced = resolve })
    let shown = 0
    runtime.client.tui = { showToast: async () => {
      shown += 1
      announced()
      await new Promise(resolve => { release = resolve })
      return {}
    } }
    const blank = await hooks.tool.ask.execute({ question: '  ' }, context(session, 'ask-blank'))
    assert.match(blank, /could not be acknowledged/i)
    assert.equal(shown, 0)
    const first = hooks.tool.ask.execute({ question }, context(session))
    await entered
    try {
      const busy = await hooks.tool.ask.execute({ question }, context(session, 'ask-concurrent'))
      assert.match(busy, /already being sent/i)
      assert.equal(shown, 1)
    } finally { release() }
    assert.match(await first, /question has been sent/i)
    assert.equal(userQuestionObservation(runtime.journal, session).length, 1)
  }, { session: 'ask-human-concurrent' })
})

test('WHAT[work-049] a genuine user reply continues the same authority and makes late ask completion harmless', async () => {
  await withReview(async ({ hooks, runtime, session }) => {
    runtime.client.tui = { showToast: async () => ({}) }
    const before = projectionObservation(runtime.journal, session).activeLogicalRun
    await hooks.tool.ask.execute({ question }, context(session))
    await hooks.event(completedAsk(session))
    await humanInput(hooks, runtime, session, 'user-answer', 'Use the staging environment.')
    const state = userQuestionObservation(runtime.journal, session)
    assert.equal(state[0].answer, 'user-answer')
    assert.deepEqual(projectionObservation(runtime.journal, session).activeLogicalRun, before)
    await hooks.event(completedAsk(session))
    assert.deepEqual(runtime.abortedIds, [session], 'the old request cannot interrupt its answer')
    const next = await hooks.tool.ask.execute({ question: 'Which region?' }, context(session, 'ask-region', 'run-region'))
    assert.match(next, /question has been sent/i)
    await hooks.event(completedAsk(session))
    assert.deepEqual(runtime.abortedIds, [session], 'the old call cannot interrupt a later pending question')
  }, { session: 'ask-human-resume' })
})

test('WHAT[work-049] interrupt failure remains explicit and does not turn waiting into completion', async () => {
  await withReview(async ({ hooks, runtime, session }) => {
    runtime.client.tui = { showToast: async () => ({}) }
    await hooks.tool.ask.execute({ question }, context(session))
    const before = projectionObservation(runtime.journal, session).activeLogicalRun
    const abort = runtime.client.session.abort
    runtime.client.session.abort = async () => { throw new Error('interrupt unavailable') }
    try {
      await assert.rejects(hooks.event(completedAsk(session)), /interrupt failed/i)
      assert.equal(userQuestionObservation(runtime.journal, session)[0].answer, null)
      assert.deepEqual(projectionObservation(runtime.journal, session).activeLogicalRun, before)
    } finally { runtime.client.session.abort = abort }
  }, { session: 'ask-human-interrupt-failure' })
})

test('WHAT[work-049] waiting survives plugin restart and the next real input resumes without a new question', async () => {
  await withRestartablePlugin(async (start, directory, runtime) => {
    const session = 'ask-human-restart'
    let shown = 0
    runtime.client.tui = { showToast: async () => { shown += 1; return {} } }
    const first = await start()
    await runtime.withRuntime(async durable => {
      await acceptAuthorityRoot(durable, session, 'manager', 'question-root')
      await humanInput(first, runtime, session, 'question-root', 'Finish the deployment plan.')
      await openIncumbency(durable, session, 'inc-question-restart', 'S4')
      assert.match(await first.tool.ask.execute({ question }, context(session)), /question has been sent/i)
      assert.equal(userQuestionObservation(durable.journal, session)[0].answer, null)
    })
    await runtime.stop(first)
    const second = await start()
    await runtime.withRuntime(async durable => {
      assert.equal(userQuestionObservation(durable.journal, session)[0].answer, null)
      await humanInput(second, runtime, session, 'restart-answer', 'Use staging.')
      assert.equal(userQuestionObservation(durable.journal, session)[0].answer, 'restart-answer')
      await second.event(completedAsk(session))
      assert.equal(shown, 1)
    })
  })
})
