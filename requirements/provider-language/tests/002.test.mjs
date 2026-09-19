import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { clearAllForTests, readGlobalPreference, parse, tryParse, label, resourceDirectory, bindOnce, inheritFromOwner, tryGet, inheritFrom, languageRootsPresent, relativePath, exists } = await import("../../../dist/Participant/Provider/LanguageSurface.js");

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

test('WHAT[provider-language-002] bind once is immutable and conflicting rebind fails closed', () => {
  clearAllForTests()
  const root = 'ses_root_lang'
  const zh = simplifiedChinese

  const bound = bindOnce(root, zh)
  assert.equal(bound.ok, true)
  assert.equal(bound.value, simplifiedChinese)

  const again = bindOnce(root, zh)
  assert.equal(again.ok, true)

  const conflict = bindOnce(root, english)
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /already bound/)
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

test('WHAT[provider-language-002] bound session language follows the session binding', () => {
  const sid = 'ses_prose_bound'
  const bound = bindOnce(sid, simplifiedChinese)
  assert.equal(bound.ok, true)
  assert.equal(nameOf(languageOfSession(sid)), simplifiedChinese)
})
}
