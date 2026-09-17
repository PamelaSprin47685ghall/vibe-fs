import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { clearAllForTests, readGlobalPreference, parse, tryParse, label, resourceDirectory, bindOnce, inheritFromOwner, tryGet, inheritFrom, languageRootsPresent, relativePath, exists } = await import("../../../dist/Participant/Provider/LanguageSurface.js");

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { clearAllForTests, bindOnce, loadBookkeeperSystem, transformBookkeeperSystem } = await import("../../../dist/Participant/Provider/LanguageSurface.js");
const bookkeeper = await import("../../../dist/Repository/Knowledge/Casebook/BookkeeperSurface.js");

const SID = 'provider-system-i18n-bookkeeper'
test.beforeEach(() => clearAllForTests())
test.afterEach(() => clearAllForTests())

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
}
