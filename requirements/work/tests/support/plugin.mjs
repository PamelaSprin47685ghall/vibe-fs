import { withExecutablePlugin, acceptAuthorityRoot, openIncumbency } from '../../../verification-system/tests/support/plugin-fixture.mjs'

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

// A non-empty findings array is the only failing shape; an empty array passes.
export const findings = grade => grade === 'REVISE'
  ? { findings: [{ acceptance_criteria: 'the target state is not yet reached', work_plan: 'close the remaining gap' }] }
  : { findings: [] }
export const scores = findings
export const withReview = async body => withExecutablePlugin(async (hooks, directory, created, runtime) => {
  const session = 'review-manager'
  await acceptAuthorityRoot(runtime, session, 'manager', 'user-root')
  await hooks['chat.message']({sessionID: session, agent: 'manager'}, {
    message: {id: 'user-root', role: 'user', agent: 'manager', model: {providerID: 'provider', modelID: 'manager-model'}},
    parts: [{type: 'text', text: 'Deliver the requested behavior and verification.'}],
  })
  await openIncumbency(runtime, session)
  runtime.pushHostMessage(session, {info: {id: 'user-root', role: 'user', sessionID: session, time: {created: 1}}, parts: [{type: 'text', text: 'Deliver the requested behavior and verification.'}]})
  let createdAt = 1
  const execute = async (input, {call = 'review-call', run = 'review-run', before = 'I independently inspected the current workspace.', after = 'after review'} = {}) => {
    runtime.pushHostMessage(session, {
      info: {id: run, role: 'assistant', sessionID: session, parentID: 'user-root', time: {created: ++createdAt}},
      parts: [
        {type: 'reasoning', text: 'private reasoning is not public evidence'},
        {type: 'text', text: before},
        {type: 'tool', tool: 'review', callID: call, state: {status: 'pending', input}},
        {type: 'text', text: after},
      ],
    })
    const result = await hooks.tool.review.execute(input, {sessionID: session, callID: call, messageID: run, agent: 'manager'})
    // The review run is finished once its tool returns. A later provider request
    // of the same user message must not see it as a second unsealed assistant.
    const pushed = runtime.messages.find(message => (message.info?.id ?? message.id) === run)
    if (pushed?.info) pushed.info.time = { ...pushed.info.time, completed: createdAt }
    return result
  }
  await body({hooks, directory, runtime, session, execute, created})
})
