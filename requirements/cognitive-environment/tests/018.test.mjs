import assert from 'node:assert/strict'
import test from 'node:test'
import { planRequest, bindDelegation, askNegotiation, prepareNegotiationReply, confirmNegotiationSteer, resetScopeState, onTurnStart, clearSessionState } from '../../../dist/Context/Scope/Surface.js'
import { refreshGlobalLanguage } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { withPreference } from '../../provider-language/tests/support/language-fixtures.mjs'

const user = (key, time = 1) => ({ kind: 'user', operator: true, key, timestamp: time, parts: [] })
const assistant = (id, tool = 'read', field = 'output') => ({ kind: 'assistant', operator: false, parts: [{ id, tool, terminalField: field }] })

test('WHAT[cognitive-environment-018] the production HNI passes live Chinese preference to the one PairHint owner', async () => {
  const session = 'scope-hni-zh-language'
  try {
    await withPreference('zh-CN', async () => {
      refreshGlobalLanguage()
      const before = planRequest(session, [user('current'), assistant('read')])
      const spoken = before.hints.find(h => h.kind === 'user').tail
      const toolReminder = before.hints.find(h => h.kind === 'tool').tail
      assert.equal(spoken, toolReminder)
      assert.match(spoken, /估计第一个细粒度任务/)
      assert.doesNotMatch(spoken, /\[System:|Then call resetScope immediately|Estimate the turnBudget/)
      resetScopeState(session, 1)
      onTurnStart(session)
      const noBudget = planRequest(session, [user('current'), assistant('reset', 'resetScope')])
      assert.match(noBudget.weak, /当前 scope 的 turnBudget 已用尽/)
      assert.doesNotMatch(noBudget.weak, /If you are not busy|when convenient|\[System:/)
    })
  } finally {
    clearSessionState(session)
    refreshGlobalLanguage()
  }
})

test('WHAT[cognitive-environment-018] Chinese delegated-first steer is actually localized and not accepted before physical confirmation', async () => {
  const session = 'scope-hni-zh-negotiation'
  try {
    await withPreference('zh-CN', async () => {
      refreshGlobalLanguage()
      const rejected = askNegotiation('unmanaged-no-delegator', '核实细节')
      assert.equal(rejected.ok, false)
      assert.match(rejected.reason, /不是受委托会话/)
      assert.doesNotMatch(rejected.reason, /not a delegated session/)
      bindDelegation(session, 'genuine-delegator')
      const first = planRequest(session, [user('delegated-task'), assistant('read')])
      assert.equal(first.negotiationRequired, true)
      assert.match(first.hints[0].tail, /先调用 negotiate/)
      assert.doesNotMatch(first.hints[0].tail, /Ask the delegator|\[System:/)
      assert.equal(first.hints.some(x => x.kind === 'tool'), false)

      const asked = askNegotiation(session, '之前为什么决定放弃方案 B？')
      assert.equal(asked.ok, true)
      const reply = prepareNegotiationReply(asked.receipt, '此前观察到反例。')
      assert.equal(reply.ok, true)
      assert.match(reply.steer, /^委托者已回答 negotiate\(questions\)/)
      assert.doesNotMatch(reply.steer, /^Delegator answered/)
      assert.equal(confirmNegotiationSteer(asked.receipt, 'other-session'), false)
      assert.equal(confirmNegotiationSteer(asked.receipt, session), true)
      const subsequent = planRequest(session, [user('delegated-task'), user('answer', 2)])
      assert.equal(subsequent.negotiationRequired, false)
      assert.match(subsequent.hints.at(-1).tail, /估计第一个细粒度任务/)
    })
  } finally {
    clearSessionState(session)
    refreshGlobalLanguage()
  }
})

test('WHAT[cognitive-environment-018] a live language change selects new hints and cannot rewrite pinned tool result bytes', async () => {
  const session = 'scope-hni-switch-language'
  try {
    await withPreference('zh-CN', async () => {
      refreshGlobalLanguage()
      planRequest(session, [user('old')])
      resetScopeState(session, 5)
      const first = planRequest(session, [user('old'), assistant('reset', 'resetScope'), assistant('read')])
      const pinned = first.hints.find(h => h.index === 1).tail
      assert.equal(pinned, '[Remaining turnBudget: 5]')
    })
    await withPreference('en', async () => {
      refreshGlobalLanguage()
      onTurnStart(session)
      const later = planRequest(session, [user('old'), assistant('reset', 'resetScope'), assistant('read'), assistant('next')])
      assert.equal(later.hints.find(h => h.index === 1).tail, '[Remaining turnBudget: 5]')
      assert.equal(later.hints.find(h => h.index === 3).tail, '[Remaining turnBudget: 4]')
    })
  } finally {
    clearSessionState(session)
    refreshGlobalLanguage()
  }
})
