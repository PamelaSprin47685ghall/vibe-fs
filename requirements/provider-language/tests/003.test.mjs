import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { clearAllForTests, readGlobalPreference, parse, tryParse, label, resourceDirectory, bindOnce, inheritFromOwner, tryGet, inheritFrom, languageRootsPresent, relativePath, exists } = await import("../../../dist/Participant/Provider/LanguageSurface.js");

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

test('WHAT[provider-language-003] child inherits owner language without re-reading global', () => {
  clearAllForTests()
  const child = 'ses_child_lang'
  const zh = simplifiedChinese

  const inherited = inheritFromOwner(zh, child)
  assert.equal(inherited.ok, true)
  assert.equal(inherited.value, simplifiedChinese)
  assert.equal(tryGet(child), simplifiedChinese)
  assert.equal(inheritFrom(zh), simplifiedChinese)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { languageOfSession, substitute, ensureInherited, ensureRoot, clearAllForTests, bindOnce, nameOf, requireLanguagePair } = await import("../../../dist/Participant/Provider/LanguageSurface.js");
const { readFileSync, readdirSync, statSync } = await import("node:fs");
const { join, resolve } = await import("node:path");
const { fileURLToPath } = await import("node:url");

const ROOT = resolve(fileURLToPath(import.meta.url), '../../../..')
const SRC_ROOT = join(ROOT, 'src/Wanxiangshu')
const walk = (dir) => {
  const out = []
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name)
    if (entry.isDirectory()) out.push(...walk(full))
    else if (entry.name.endsWith('.fs')) out.push(full)
  }
  return out
}
const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'
const withPreference = async (raw, fn) => {
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  if (raw === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = raw
  try {
    return await fn()
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
  }
}
test.beforeEach(() => {
  clearAllForTests()
})

test('WHAT[provider-language-003] child inherits owner language without reading the global preference', async () => {
  const existing = 'ses_pref_existing'

  await withPreference('zh-CN', async () => {
    assert.equal(nameOf(ensureRoot(existing)), simplifiedChinese)
    // owner=zh → child=zh，即使全局已是 en。
    return withPreference('en', async () => {
      const child = 'ses_pref_child'
      assert.equal(nameOf(ensureInherited(existing, child)), simplifiedChinese)
    })
  })
})
}
