import assert from 'node:assert/strict'
import test from 'node:test'
import {
  clearAllForTests,
  ensureRoot,
  languageOfSession,
  nameOf,
  transformRoleSystem,
} from '../../../dist/Participant/Provider/LanguageSurface.js'
import {
  configure as configureManagedAgents,
  installDefaultResources,
} from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import * as promptSurface from '../../../dist/Resources/PromptSurface.js'

const hasChinese = (text) => /[\u4e00-\u9fff]/.test(text)

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

const buildConfig = () => ({
  agent: {
    manager: { model: 'manager-model' },
    orchestrator: { model: 'orchestrator-model' },
    engineer: { model: 'engineer-model' },
    devops: { model: 'devops-model' },
    blogger: { model: 'blogger-model' },
    bookkeeper: { model: 'bookkeeper-model' },
  },
})

test.beforeEach(() => {
  clearAllForTests()
})

test('WHAT[provider-language-013] zh-CN preference projects Chinese prompts onto every managed agent', async () => {
  await withPreference('zh-CN', async () => {
    installDefaultResources()
    const config = buildConfig()
    const outcome = configureManagedAgents(config)
    assert.equal(outcome.ok, true, `gate must accept the catalog: ${outcome.error}`)

    for (const name of ['manager', 'orchestrator', 'engineer', 'devops', 'blogger', 'bookkeeper']) {
      const prompt = config.agent[name]?.prompt
      assert.ok(typeof prompt === 'string' && prompt.length > 0, `${name} must carry a prompt`)
      assert.ok(
        hasChinese(prompt),
        `${name} Host-config prompt must be Chinese under a zh-CN preference, got: ${prompt.slice(0, 60)}`,
      )
    }
  })
})

test('WHAT[provider-language-013] English preference still projects English prompts', async () => {
  await withPreference('en', async () => {
    installDefaultResources()
    const config = buildConfig()
    const outcome = configureManagedAgents(config)
    assert.equal(outcome.ok, true, `gate must accept the catalog: ${outcome.error}`)

    for (const name of ['manager', 'orchestrator', 'engineer', 'devops', 'blogger', 'bookkeeper']) {
      const prompt = config.agent[name]?.prompt
      assert.ok(typeof prompt === 'string' && prompt.length > 0, `${name} must carry a prompt`)
      assert.ok(
        !hasChinese(prompt),
        `${name} Host-config prompt must stay English under an en preference, got: ${prompt.slice(0, 60)}`,
      )
    }
  })
})

test('WHAT[provider-language-013] the Manager system prompt a zh-CN session receives is Chinese', async () => {
  await withPreference('zh-CN', async () => {
    installDefaultResources()
    const config = buildConfig()
    configureManagedAgents(config)

    const sid = 'ses_provider_language_013_manager'
    assert.equal(nameOf(ensureRoot(sid)), 'SimplifiedChinese')

    // The Host assembles system[] from the config agent prompt plus its own text.
    const output = { system: [config.agent.manager.prompt, 'HOST AGENTS TEXT'] }
    await transformRoleSystem(sid, 'Manager', output.system)

    assert.ok(
      hasChinese(output.system[0]),
      `Manager system prompt must be Chinese, got: ${output.system[0].slice(0, 60)}`,
    )
    assert.equal(output.system[1], 'HOST AGENTS TEXT', 'Host-owned text must stay untouched')

    // A second pass must stay Chinese (idempotent repair).
    const second = { system: [output.system[0], 'HOST AGENTS TEXT'] }
    await transformRoleSystem(sid, 'Manager', second.system)
    assert.ok(hasChinese(second.system[0]), 'repeated transforms must keep the bound language')
  })
})

test('WHAT[provider-language-013] an English-bound session keeps English even when the preference is Chinese', async () => {
  const sid = 'ses_provider_language_013_bound_en'

  await withPreference('en', async () => {
    clearAllForTests()
    installDefaultResources()
    assert.equal(nameOf(ensureRoot(sid)), 'English')
  })

  await withPreference('zh-CN', async () => {
    const config = buildConfig()
    configureManagedAgents(config)

    const output = { system: [promptSurface.loadForLanguage('English').ManagerSystemPrompt, 'HOST TEXT'] }
    await transformRoleSystem(sid, 'Manager', output.system)

    assert.equal(
      nameOf(languageOfSession(sid)),
      'English',
      'bind-once must survive a later preference change (provider-language-002/004)',
    )
    assert.ok(
      !hasChinese(output.system[0]),
      `an English-bound session must never receive Chinese prose, got: ${output.system[0].slice(0, 60)}`,
    )
  })
})

test('WHAT[provider-language-013] companion instruction prose follows the configured language', async () => {
  await withPreference('zh-CN', async () => {
    const companion = await import('../../../dist/Context/Companion/ProjectionSurface.js')
    assert.ok(
      hasChinese(companion.normalInstruction),
      `companion normal instruction must be Chinese, got: ${companion.normalInstruction.slice(0, 60)}`,
    )
    assert.ok(
      hasChinese(companion.squashInstruction),
      `companion squash instruction must be Chinese, got: ${companion.squashInstruction.slice(0, 60)}`,
    )
    assert.ok(
      hasChinese(companion.memoryPreamble),
      `companion memory preamble must be Chinese, got: ${companion.memoryPreamble.slice(0, 60)}`,
    )
  })
})

test('WHAT[provider-language-013] horizon roster prose follows the configured language', async () => {
  await withPreference('zh-CN', async () => {
    const horizon = await import('../../../dist/Execution/Session/OpenCode/HorizonSurface.js')
    assert.ok(
      hasChinese(horizon.description()),
      `horizon description must be Chinese, got: ${horizon.description().slice(0, 60)}`,
    )

    const roster = horizon.render([{ label: 'engineer', status: 'active', work: 'none', record: '' }], [])
    assert.ok(
      hasChinese(roster),
      `horizon roster must be Chinese, got: ${roster.slice(0, 80)}`,
    )
  })
})
