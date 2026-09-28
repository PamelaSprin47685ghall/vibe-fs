// Real durable Blogger request ownership (context-compression-024), built only
// through production writers: a HumanRoot profile on the Blogger session, a
// plugin dispatch that lands as one exact physical message, and the owner
// binding that landed dispatch to the durable open request.
import assert from 'node:assert/strict'

import * as blog from '../../../../dist/Enforcer/BlogSurface.js'
import * as dispatch from '../../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../../dist/Persistence/Journal/Surface.js'

// Every Blogger Main dispatch carries the same fixed instruction text, so all
// of them share one payload digest (dispatch-protocol-006).
export const BLOGGER_INSTRUCTION = '# Call the chronicle tool exactly once.'

const dispatchPort = () => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SubscribeFutureTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: async () => dispatch.admittedWithReceipt('accepted-owner-dispatch'),
})

/** Root the Blogger session's authority at `rootPhysical`; returns the durable profile. */
export const rootBlogger = async (handle, bloggerSession, rootPhysical) => {
  const accepted = await dispatch.acceptHumanRoot(handle, bloggerSession, rootPhysical, 'blogger')
  assert.equal(accepted.ok, true, accepted.ok ? '' : JSON.stringify(accepted.error))
  return accepted.profile
}

/** Durable main ↔ Blogger association, the link the continuation transform reads. */
export const linkBlogger = async (handle, mainSession, bloggerSession) => {
  const linked = await journal.JournalSurface_appendAgent(
    handle,
    { kind: 'Session', session: mainSession },
    null,
    {
      family: 'Companion',
      case: 'CompanionBloggerLinked',
      payload: { SessionId: mainSession, BloggerSessionId: bloggerSession, BloggerAgent: 'blogger' },
    },
  )
  assert.equal(linked.ok, true, linked.ok ? '' : linked.error)
}

/** Physically land one already-claimed prompt as `physical`. */
export const landClaim = async (handle, bloggerSession, promptKey, physical) => {
  const landed = await dispatch.acceptManagedPromptClaim(handle, bloggerSession, physical, promptKey, 'blogger')
  assert.equal(landed.ok, true, landed.ok ? '' : JSON.stringify(landed))
}

/** One plugin dispatch that physically landed as `physical`; returns its PromptKey. */
export const landDispatch = async (handle, bloggerSession, profile, physical, text = BLOGGER_INSTRUCTION) => {
  const sent = await dispatch.sendContinuation(
    dispatchPort(),
    handle,
    bloggerSession,
    text,
    'ManagedDelegationAssignment',
    profile,
    'Detached',
  )
  assert.equal(sent.ok, true, sent.ok ? '' : sent.error)
  await landClaim(handle, bloggerSession, sent.key, physical)
  return sent.key
}

/** Land a dispatch for `request` and bind it as the request's durable open PromptKey. */
export const ownRequest = async ({ handle, durable, scope, bloggerSession, profile, request, physical, text }) => {
  const promptKey = await landDispatch(handle, bloggerSession, profile, physical, text)
  const bound = await blog.bindRequestDispatch(scope, durable, request, promptKey)
  assert.equal(bound.ok, true, bound.ok ? '' : String(bound.error))
  return promptKey
}

/** Host-shaped transcript rows, as `messages.transform` receives them. */
export const userMessage = (id, text = BLOGGER_INSTRUCTION) => ({
  info: { id, role: 'user', time: { created: 1 } },
  parts: [{ type: 'text', text }],
})

export const assistantMessage = (id, parentID, parts = [], { completed = true, error } = {}) => ({
  info: {
    ...(id === undefined ? {} : { id }),
    role: 'assistant',
    parentID,
    time: completed ? { created: 2, completed: 3 } : { created: 2 },
    ...(error === undefined ? {} : { error: { name: error, data: { message: 'Aborted' } } }),
  },
  parts,
})

export const chroniclePart = (callID, tip, text) => ({
  type: 'tool',
  tool: 'chronicle',
  callID,
  state: { status: 'completed', input: { tip, entry: text } },
})
