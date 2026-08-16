// Moved from tests/unit/prompt/provider-system-transform.test.mjs (cutover Wave 2a);
// owner: provider-language（未认领判定：断言 = PROMPT-017 ProviderLanguage 的运行时应用
// —— session 语言本地化 WXS 自有 system 段、host 段字节不动、English session 稳定。
// 证据链：provider-language WHAT PROVIDER-LANGUAGE-001/005 证据 历史 PROMPT 条款
// PROMPT-017；PROOF-MAP prompt/ family 含 provider-language。provider-projection 只拥有
// 投影确定性、cognitive-environment 只拥有内容组织，均不拥有语言轴。）

import assert from 'node:assert/strict'
import test from 'node:test'

import { create as transformSystem } from '../../../dist/OpenCode/Host/ProviderSystemTransform.js'
import {
  BookkeeperRuntime_bindSession as bindBookkeeper,
  BookkeeperRuntime_unbindSession as unbindBookkeeper,
} from '../../../dist/Repository/Knowledge/Casebook/BookkeeperRuntime.js'
import { promptResources, providerLanguage, sessionId } from '../../verification-system/tests/support/domain.mjs'

const SID = 'provider-system-i18n-bookkeeper'

test.beforeEach(() => providerLanguage.clearAllForTests())
test.afterEach(() => {
  unbindBookkeeper(SID)
  providerLanguage.clearAllForTests()
})

test('WHAT[PROVIDER-LANGUAGE-005] system transform localizes only the wanxiangshu-owned segment', async () => {
  const sid = sessionId(SID)
  assert.equal(providerLanguage.bindOnce(sid, providerLanguage.simplifiedChinese).ok, true)
  bindBookkeeper(SID, 'tx-i18n', 'owner-i18n')

  const english = promptResources.loadBookkeeperSystemFor(providerLanguage.english)
  const chinese = promptResources.loadBookkeeperSystemFor(providerLanguage.simplifiedChinese)
  const hostOwned = 'HOST-OWNED-SYSTEM-BYTES'
  const output = { system: [english, hostOwned] }

  await transformSystem(undefined, { sessionID: SID, model: {} }, output)

  assert.equal(output.system[0], chinese)
  assert.equal(output.system[1], hostOwned)
  assert.match(output.system[0], /^# 共同法/)
})

test('WHAT[PROVIDER-LANGUAGE-001] system transform is stable for an English session', async () => {
  const sid = sessionId(SID)
  assert.equal(providerLanguage.bindOnce(sid, providerLanguage.english).ok, true)
  bindBookkeeper(SID, 'tx-i18n', 'owner-i18n')

  const english = promptResources.loadBookkeeperSystemFor(providerLanguage.english)
  const output = { system: [english, 'OTHER'] }
  await transformSystem(undefined, { sessionID: SID, model: {} }, output)

  assert.deepEqual(output.system, [english, 'OTHER'])
})
