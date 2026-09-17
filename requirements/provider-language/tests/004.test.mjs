import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { clearAllForTests, readGlobalPreference, parse, tryParse, label, resourceDirectory, bindOnce, inheritFromOwner, tryGet, inheritFrom, languageRootsPresent, relativePath, exists } = await import("../../../dist/Participant/Provider/LanguageSurface.js");

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

test('WHAT[PROVIDER-LANGUAGE-004] global preference defaults to English when env unset', () => {
  const previous = process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
  try {
    assert.equal(readGlobalPreference(), english)
  } finally {
    if (previous === undefined) delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    else process.env.WANXIANGSHU_PROVIDER_LANGUAGE = previous
  }
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

test('WHAT[PROVIDER-LANGUAGE-004] unbound session language is English (first touch)', () => {
  const sid = 'ses_prose_unbound'
  assert.equal(nameOf(languageOfSession(sid)), english)
})
test('WHAT[PROVIDER-LANGUAGE-004] preference change only affects future sessions', async () => {
  const existing = 'ses_pref_existing'
  const fresh = 'ses_pref_fresh'

  await withPreference('zh-CN', async () => {
    assert.equal(nameOf(ensureRoot(existing)), simplifiedChinese)
    // 全局切到 en：已绑 session 不重绑（bind-once 拒绝异值）。
    return withPreference('en', async () => {
      assert.equal(nameOf(ensureRoot(existing)), simplifiedChinese)
      // 新 session 首触达 → 取新偏好。
      assert.equal(nameOf(ensureRoot(fresh)), english)
    })
  })
})
}
