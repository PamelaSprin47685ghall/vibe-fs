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

test('WHAT[PROVIDER-LANGUAGE-002] bind once is immutable and conflicting rebind fails closed', () => {
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

test('WHAT[PROVIDER-LANGUAGE-002] bound session language follows the session binding', () => {
  const sid = 'ses_prose_bound'
  const bound = bindOnce(sid, simplifiedChinese)
  assert.equal(bound.ok, true)
  assert.equal(nameOf(languageOfSession(sid)), simplifiedChinese)
})
