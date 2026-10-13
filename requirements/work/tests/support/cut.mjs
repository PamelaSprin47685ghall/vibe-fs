import assert from 'node:assert/strict'
import {withReview, scores} from './plugin.mjs'

export const textMessage = (id, role, text) => ({info: {id, role}, parts: [{type: 'text', text}]})
export const toolMessage = (id = 'retirement-run', call = 'deliver-call') => ({
  info: {id, role: 'assistant'},
  parts: [{type: 'tool', tool: 'deliver', callID: call, state: {status: 'completed', input: {}, output: 'Finished. Do not continue work.'}}],
})
export const messageId = message => message.id ?? message.info?.id

export const withSuccessor = async body => withReview(async context => {
  const {execute, hooks, runtime, session} = context
  assert.match(await execute(scores('REVISE')), /recorded = true/)
  const retirement = toolMessage()
  retirement.info.sessionID = session
  retirement.info.parentID = 'user-root'
  retirement.info.time = {created: 3}
  runtime.pushHostMessage(session, retirement)
  assert.match(await hooks.tool.deliver.execute({}, {
    sessionID: session, callID: 'deliver-confirm', messageID: 'retirement-confirm', agent: 'manager',
  }), /confirmation_required = true/)
  assert.match(await hooks.tool.deliver.execute({}, {
    sessionID: session, callID: 'deliver-call', messageID: 'retirement-run', agent: 'manager',
  }), /Finished\. Do not continue work\./)
  assert.deepEqual(runtime.abortedIds, [])
  const history = structuredClone(runtime.messages)
  history[0].info.model = {providerID: 'provider', modelID: 'manager-model'}
  // One provider request binds one unsealed assistant. The retired attempt is
  // the retirement run; the already-returned review run is sealed history.
  const sealed = structuredClone(history)
  for (const message of sealed) {
    if (message.info?.role === 'assistant' && message.info.id !== 'retirement-run') {
      message.info.time = { ...message.info.time, completed: 1 }
    }
  }
  const stale = {messages: sealed}
  await hooks['experimental.chat.messages.transform']({sessionID: session}, stale)
  assert.deepEqual(stale.messages, [])
  // A root session may only be interrupted by the external user
  // (MANAGED-SESSION-016), so the stop clears the retired request instead of
  // aborting the session.
  assert.deepEqual(runtime.abortedIds.filter(id => id === session), [])
  assert.equal(runtime.prompts.length, 1)
  const gate = structuredClone(runtime.messages.at(-1))
  assert.ok(gate.metadata?.wanxiangshu_prompt_key, 'successor must be the real owner-dispatched prompt')
  assert.ok(messageId(gate))
  runtime.pushHostMessage(session, {
    info: {
      id: 'successor-provider-run', role: 'assistant', sessionID: session,
      parentID: messageId(gate), time: {created: 4},
    },
    parts: [],
  })
  await body({...context, history, gate})
})
