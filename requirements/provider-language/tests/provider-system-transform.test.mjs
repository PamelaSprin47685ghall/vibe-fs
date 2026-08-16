// Moved from tests/unit/prompt/provider-system-transform.test.mjs (cutover Wave 2a);
// owner: provider-language（未认领判定：断言 = PROMPT-017 ProviderLanguage 的运行时应用
// —— session 语言本地化 WXS 自有 system 段、host 段字节不动、English session 稳定。
// 证据链：provider-language WHAT PROVIDER-LANGUAGE-001/005 证据 历史 PROMPT 条款
// PROMPT-017；PROOF-MAP prompt/ family 含 provider-language。provider-projection 只拥有
// 投影确定性、cognitive-environment 只拥有内容组织，均不拥有语言轴。）

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  clearAllForTests,
  bindOnce,
  loadBookkeeperSystem,
  transformBookkeeperSystem,
} from '../../../dist/Participant/Provider/LanguageSurface.js'

const SID = 'provider-system-i18n-bookkeeper'

test.beforeEach(() => clearAllForTests())
test.afterEach(() => clearAllForTests())

test('WHAT[PROVIDER-LANGUAGE-005] system transform localizes only the wanxiangshu-owned segment', async () => {
  assert.equal(bindOnce(SID, 'SimplifiedChinese').ok, true)
  const english = loadBookkeeperSystem('English')
  const chinese = loadBookkeeperSystem('SimplifiedChinese')
  const hostOwned = 'HOST-OWNED-SYSTEM-BYTES'
  const output = await transformBookkeeperSystem(SID, [english, hostOwned])

  assert.deepEqual(output.system, [chinese, hostOwned])
  assert.match(output.system[0], /^# 共同法/)
})

test('WHAT[PROVIDER-LANGUAGE-001] system transform is stable for an English session', async () => {
  assert.equal(bindOnce(SID, 'English').ok, true)
  const english = loadBookkeeperSystem('English')
  const output = await transformBookkeeperSystem(SID, [english, 'OTHER'])
  assert.deepEqual(output.system, [english, 'OTHER'])
})
