import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
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
  languageOfSession,
  substitute,
  ensureInherited,
  ensureRoot,
  nameOf,
  requireLanguagePair,
} from '../../../dist/Participant/Provider/LanguageSurface.js'

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

test('WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without re-reading global', () => {
  clearAllForTests()
  const child = 'ses_child_lang'
  const zh = simplifiedChinese

  const inherited = inheritFromOwner(zh, child)
  assert.equal(inherited.ok, true)
  assert.equal(inherited.value, simplifiedChinese)
  assert.equal(tryGet(child), simplifiedChinese)
  assert.equal(inheritFrom(zh), simplifiedChinese)
})

test('WHAT[PROVIDER-LANGUAGE-003] child inherits owner language without reading the global preference', async () => {
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
