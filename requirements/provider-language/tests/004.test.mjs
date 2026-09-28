import assert from 'node:assert/strict'
import test from 'node:test'
import { clearAllForTests, readGlobalPreference, setHostConfigPreference, ensureRoot, languageOfSession } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { withPreference } from './support/language-fixtures.mjs'

const localeKeys = ['WANXIANGSHU_PROVIDER_LANGUAGE', 'VSCODE_NLS_CONFIG', 'LC_ALL', 'LC_MESSAGES', 'LANG']

const withLocale = async (environment, intlLocale, action) => {
  const saved = Object.fromEntries(localeKeys.map((key) => [key, process.env[key]]))
  const dateTimeFormat = Intl.DateTimeFormat
  clearAllForTests()
  try {
    for (const key of localeKeys) {
      if (environment[key] === undefined) delete process.env[key]
      else process.env[key] = environment[key]
    }
    Intl.DateTimeFormat = () => ({ resolvedOptions: () => ({ locale: intlLocale }) })
    await action()
  } finally {
    Intl.DateTimeFormat = dateTimeFormat
    for (const key of localeKeys) {
      if (saved[key] === undefined) delete process.env[key]
      else process.env[key] = saved[key]
    }
    clearAllForTests()
  }
}

test('WHAT[provider-language-004] English is the final fallback after all locale sources are non-Chinese', async () => {
  await withLocale({}, 'en-US', () => {
    assert.equal(readGlobalPreference(), 'English')
    assert.equal(ensureRoot('fallback-root'), 'English')
  })
})

test('WHAT[provider-language-004] every declared locale source can select SimplifiedChinese', async () => {
  for (const environment of [
    { VSCODE_NLS_CONFIG: JSON.stringify({ locale: 'zh-cn' }) },
    { LC_ALL: 'zh_CN.UTF-8' },
    { LC_MESSAGES: 'zh_CN.UTF-8' },
    { LANG: 'zh_CN.UTF-8' },
  ]) {
    await withLocale(environment, 'en-US', () => assert.equal(readGlobalPreference(), 'SimplifiedChinese'))
  }
  await withLocale({}, 'zh-CN', () => assert.equal(readGlobalPreference(), 'SimplifiedChinese'))
})

test('WHAT[provider-language-004] explicit environment outranks host configuration which outranks locale detection', async () => {
  await withLocale({ LANG: 'zh_CN.UTF-8' }, 'zh-CN', async () => {
    setHostConfigPreference('en')
    assert.equal(readGlobalPreference(), 'English')
    await withPreference('zh-CN', () => assert.equal(readGlobalPreference(), 'SimplifiedChinese'))
    setHostConfigPreference('zh-CN')
    await withPreference('en', () => assert.equal(readGlobalPreference(), 'English'))
  })
})

test('WHAT[provider-language-004] preference changes affect only future roots in either direction', async () => {
  await withLocale({}, 'en-US', async () => {
    await withPreference('zh-CN', () => assert.equal(ensureRoot('first'), 'SimplifiedChinese'))
    await withPreference('en', () => {
      assert.equal(ensureRoot('first'), 'SimplifiedChinese')
      assert.equal(ensureRoot('second'), 'English')
    })
    await withPreference('zh-CN', () => {
      assert.equal(ensureRoot('second'), 'English')
      assert.equal(ensureRoot('third'), 'SimplifiedChinese')
      assert.equal(languageOfSession('first'), 'SimplifiedChinese')
    })
  })
})
