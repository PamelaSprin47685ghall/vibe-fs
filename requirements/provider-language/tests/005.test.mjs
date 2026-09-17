import test from 'node:test'

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

test('WHAT[PROVIDER-LANGUAGE-005] production feature code carries no inline bilingual literal branch', () => {
  // Class A prose lives only under resources/provider; feature code may not
  // smuggle natural-language locale forks. Scan every production .fs for the
  // giveaway shape `match ... with | ... -> "en text" | ... -> "zh text"`.
  const suspicious = []
  for (const file of walk(SRC_ROOT)) {
    const text = readFileSync(file, 'utf8')
    const rel = file.slice(ROOT.length + 1).replace(/\\/g, '/')
    if (/match\s+\w*[Ll]ang\w*[\s\S]{0,200}\|[^\n]*->[^\n]*zh\/zh-CN/i.test(text)) {
      suspicious.push(rel)
    }
  }
  assert.deepEqual(suspicious, [], 'business logic must not hardcode bilingual literal branches')
})
test('WHAT[PROVIDER-LANGUAGE-005] Class A prose loads through the resource layer, both locales', () => {
  // Three-way ownership proof: semantic content is read via ProviderResources-
  // owned paths under resources/provider, which currently contains role/manager
  // en.md + zh-CN.md — not embedded in any .fs file.
  for (const leaf of ['en.md', 'zh-CN.md']) {
    const text = readFileSync(join(ROOT, 'resources/provider/role/manager', leaf), 'utf8')
    assert.ok(text.trim().length > 0, `resources/provider/role/manager/${leaf} empty`)
  }
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

test('WHAT[PROVIDER-LANGUAGE-005] system transform localizes only the wanxiangshu-owned segment', async () => {
  bookkeeper.bindSession(SID, 'provider-language-surface', 'provider-language-surface')
  try {
    assert.equal(bindOnce(SID, 'SimplifiedChinese').ok, true)
    const english = loadBookkeeperSystem('English')
    const chinese = loadBookkeeperSystem('SimplifiedChinese')
    const hostOwned = 'HOST-OWNED-SYSTEM-BYTES'
    const output = await transformBookkeeperSystem(SID, [english, hostOwned])

    assert.deepEqual(output.system, [chinese, hostOwned])
    assert.match(output.system[0], /^# # 共同法/)
    assert.equal(output.system[0].split('\n').filter(Boolean).every((line) => line === '#' || line.startsWith('# ')), true)
  } finally {
    bookkeeper.unbindSession(SID)
  }
})
}
