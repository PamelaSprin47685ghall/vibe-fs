import test from 'node:test'

{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const {
    clearAllForTests,
    readGlobalPreference,
    setHostConfigPreference,
  } = await import('../../../dist/Participant/Provider/LanguageSurface.js')

  const english = 'English'
  const simplifiedChinese = 'SimplifiedChinese'

  const withCleanLocale = (fn) => {
    const envSnapshot = {
      WANXIANGSHU_PROVIDER_LANGUAGE: process.env.WANXIANGSHU_PROVIDER_LANGUAGE,
      VSCODE_NLS_CONFIG: process.env.VSCODE_NLS_CONFIG,
      LC_ALL: process.env.LC_ALL,
      LC_MESSAGES: process.env.LC_MESSAGES,
      LANG: process.env.LANG,
    }
    delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    delete process.env.VSCODE_NLS_CONFIG
    delete process.env.LC_ALL
    delete process.env.LC_MESSAGES
    delete process.env.LANG
    try {
      return fn()
    } finally {
      for (const [key, val] of Object.entries(envSnapshot)) {
        if (val === undefined) delete process.env[key]
        else process.env[key] = val
      }
    }
  }

  test.beforeEach(() => {
    clearAllForTests()
  })

  test('WHAT[provider-language-004] global preference defaults to English when env unset and clean locale', () => {
    withCleanLocale(() => {
      assert.equal(readGlobalPreference(), english)
    })
  })

  test('WHAT[provider-language-004] system/IDE locale detects SimplifiedChinese automatically', () => {
    withCleanLocale(() => {
      process.env.VSCODE_NLS_CONFIG = JSON.stringify({ locale: 'zh-cn', osLocale: 'zh-cn' })
      assert.equal(readGlobalPreference(), simplifiedChinese)
    })

    withCleanLocale(() => {
      process.env.LANG = 'zh_CN.UTF-8'
      assert.equal(readGlobalPreference(), simplifiedChinese)
    })
  })

  test('WHAT[provider-language-004] explicit environment variable overrides Chinese system locale', () => {
    withCleanLocale(() => {
      process.env.VSCODE_NLS_CONFIG = JSON.stringify({ locale: 'zh-cn' })
      process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'English'
      assert.equal(readGlobalPreference(), english)
    })
  })

  test('WHAT[provider-language-004] host configuration takes precedence over clean locale', () => {
    withCleanLocale(() => {
      setHostConfigPreference('zh-CN')
      assert.equal(readGlobalPreference(), simplifiedChinese)
    })
  })
}

{
  const { default: assert } = await import('node:assert/strict')
  const { default: test } = await import('node:test')
  const {
    languageOfSession,
    ensureRoot,
    clearAllForTests,
    nameOf,
  } = await import('../../../dist/Participant/Provider/LanguageSurface.js')

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

  const withCleanLocale = async (fn) => {
    const envSnapshot = {
      WANXIANGSHU_PROVIDER_LANGUAGE: process.env.WANXIANGSHU_PROVIDER_LANGUAGE,
      VSCODE_NLS_CONFIG: process.env.VSCODE_NLS_CONFIG,
      LC_ALL: process.env.LC_ALL,
      LC_MESSAGES: process.env.LC_MESSAGES,
      LANG: process.env.LANG,
    }
    delete process.env.WANXIANGSHU_PROVIDER_LANGUAGE
    delete process.env.VSCODE_NLS_CONFIG
    delete process.env.LC_ALL
    delete process.env.LC_MESSAGES
    delete process.env.LANG
    try {
      return await fn()
    } finally {
      for (const [key, val] of Object.entries(envSnapshot)) {
        if (val === undefined) delete process.env[key]
        else process.env[key] = val
      }
    }
  }

  test.beforeEach(() => {
    clearAllForTests()
  })

  test('WHAT[provider-language-004] unbound session language is English in clean locale', async () => {
    await withCleanLocale(async () => {
      const sid = 'ses_prose_unbound'
      assert.equal(nameOf(languageOfSession(sid)), english)
    })
  })

  test('WHAT[provider-language-004] preference change only affects future sessions', async () => {
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
