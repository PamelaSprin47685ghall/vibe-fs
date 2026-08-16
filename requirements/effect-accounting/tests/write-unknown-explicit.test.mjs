// EFFECT-ACCOUNTING-006 contract test（本包 NEW）：
// append/effect 结局未知（写失败、writer poisoned/disposed）必须以显式分型表达
// （WriteUnknown / CommitUnknown），不得假装成功、不得假装未发生。

import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentFact,
  agentJournal,
  sessionId,
  stream,
} from '../../verification-system/tests/support/domain.mjs'

test('WHAT[EFFECT-ACCOUNTING-006] write_after_dispose_returns_explicit_unknown_not_pretended_commit', async () => {
  const created = await agentJournal.create({ runtime: 'rt_006_unknown' })
  assert.equal(created.ok, true, created.ok ? '' : JSON.stringify(created.error))
  const fact = agentFact('CompanionBloggerClosed', { SessionId: sessionId('ses_006') })

  // 正常 append 成功（对照基线）。
  const healthy = await agentJournal.appendAgent(
    stream.session(sessionId('ses_006')),
    undefined,
    fact,
    created.journal,
  )
  assert.equal(healthy.ok, true, healthy.ok ? '' : JSON.stringify(healthy.error))

  // writer 已 dispose：append 结局未知必须显式分型（WriteUnknown / CommitUnknown），
  // 绝不假装 committed、绝不静默吞掉。
  created.dispose()
  const result = await agentJournal.appendAgent(
    stream.session(sessionId('ses_006')),
    undefined,
    fact,
    created.journal,
  )
  assert.equal(result.ok, false, 'disposed writer append must not pretend success')
  assert.match(String(result.error), /WriteUnknown|poisoned|disposed|CommitUnknown/i)
})
