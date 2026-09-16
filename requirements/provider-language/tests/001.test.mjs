import assert from 'node:assert/strict'
import test from 'node:test'
import * as bookkeeper from '../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js'
import {

// tests/unit/prompt/provider-language.test.mjs — PROMPT-017 / HOST-026 Phase 2.
//
// ProviderLanguage parse + SessionProviderLanguage bind-once inherit.
// Does not migrate bilingual prose (Phase 17).

  clearAllForTests,
  readGlobalPreference,
  parse,
  tryParse,
  label,
  resourceDirectory,
  bindOnce,
  inheritFromOwner,
  tryGet,
  inheritFrom,
  languageRootsPresent,
  relativePath,
  exists,
} from '../../../dist/Participant/Provider/LanguageSurface.js'

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

// Moved from tests/unit/prompt/provider-system-transform.test.mjs (cutover Wave 2a);
// owner: provider-language（未认领判定：断言 = PROMPT-017 ProviderLanguage 的运行时应用
// —— session 语言本地化 WXS 自有 system 段、host 段字节不动、English session 稳定。
// 证据链：provider-language WHAT PROVIDER-LANGUAGE-001/005 证据 历史 PROMPT 条款
// PROMPT-017；PROOF-MAP prompt/ family 含 provider-language。provider-projection 只拥有
// 投影确定性、cognitive-environment 只拥有内容组织，均不拥有语言轴。）
import {
  loadBookkeeperSystem,
  transformBookkeeperSystem,
} from '../../../dist/Participant/Provider/LanguageSurface.js'

const SID = 'provider-system-i18n-bookkeeper'

test.beforeEach(() => clearAllForTests())
test.afterEach(() => clearAllForTests())

test('WHAT[PROVIDER-LANGUAGE-001] ProviderLanguage parses en and zh-CN with locale mapping', () => {
  assert.equal(parse('en'), english)
  assert.equal(parse('english'), english)
  assert.equal(parse('zh-CN'), simplifiedChinese)
  assert.equal(parse('zh'), simplifiedChinese)
  assert.equal(tryParse('nope'), null)
  assert.equal(label(english), 'en')
  assert.equal(resourceDirectory(simplifiedChinese), 'zh-CN')
})

test('WHAT[PROVIDER-LANGUAGE-001] provider resource language roots map en.md and zh-CN.md', () => {
  assert.equal(languageRootsPresent(), true)
  assert.equal(
    relativePath(english, 'role/manager'),
    'provider/role/manager/en.md',
  )
  assert.equal(
    relativePath(simplifiedChinese, 'role/manager'),
    'provider/role/manager/zh-CN.md',
  )
})

test('WHAT[PROVIDER-LANGUAGE-001] system transform is stable for an English session', async () => {
  bookkeeper.bindSession(SID, 'provider-language-surface', 'provider-language-surface')
  try {
    assert.equal(bindOnce(SID, 'English').ok, true)
    const english = loadBookkeeperSystem('English')
    const output = await transformBookkeeperSystem(SID, [english, 'OTHER'])
    assert.deepEqual(output.system, [english, 'OTHER'])
  } finally {
    bookkeeper.unbindSession(SID)
  }
})
