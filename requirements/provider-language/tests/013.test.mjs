import assert from 'node:assert/strict'
import test from 'node:test'
import { clearAllForTests, ensureRoot, languageOfSession, transformRoleSystem } from '../../../dist/Participant/Provider/LanguageSurface.js'
import { configure, installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
import * as prompts from '../../../dist/Resources/PromptSurface.js'
import { withPreference } from './support/language-fixtures.mjs'

const fields = {
  manager: 'ManagerSystemPrompt', orchestrator: 'OrchestratorSystemPrompt',
  engineer: 'EngineerSystemPrompt', devops: 'DevopsSystemPrompt', blogger: 'BloggerSystemPrompt',
}
const buildConfig = () => ({
  agent: Object.fromEntries([...Object.keys(fields), 'bookkeeper'].map((name) => [name, { model: `${name}-model` }])),
})

test.beforeEach(clearAllForTests)
test.afterEach(clearAllForTests)

test('WHAT[provider-language-013] config projection selects the complete catalog for each global language', async () => {
  for (const [preference, language] of [['zh-CN', 'SimplifiedChinese'], ['en', 'English']]) {
    await withPreference(preference, () => {
      installDefaultResources()
      const config = buildConfig()
      assert.equal(configure(config).ok, true)
      const expected = prompts.loadForLanguage(language)
      for (const [name, field] of Object.entries(fields)) {
        assert.equal(config.agent[name].prompt, expected[field], `${name}/${language}`)
      }
      assert.equal(config.agent.bookkeeper.prompt, prompts.loadBookkeeperSystemFor(language))
    })
  }
})

test('WHAT[provider-language-013] system repair follows the existing binding despite an opposite preference and installed view', async () => {
  for (const [initial, language, changed, otherLanguage] of [
    ['en', 'English', 'zh-CN', 'SimplifiedChinese'],
    ['zh-CN', 'SimplifiedChinese', 'en', 'English'],
  ]) {
    const session = `manager-${language}`
    await withPreference(initial, () => assert.equal(ensureRoot(session), language))
    await withPreference(changed, async () => {
      installDefaultResources()
      const expected = prompts.loadForLanguage(language).ManagerSystemPrompt
      const foreign = 'HOST-owned bytes: exit_code 中文'
      for (const input of [
        prompts.loadForLanguage(otherLanguage).ManagerSystemPrompt,
        prompts.loadForLanguage(language).ManagerSystemPrompt,
      ]) {
        const output = await transformRoleSystem(session, 'Manager', [input, foreign])
        assert.deepEqual(output.system, [expected, foreign])
        const repeated = await transformRoleSystem(session, 'Manager', output.system)
        assert.deepEqual(repeated.system, output.system)
      }
      assert.equal(languageOfSession(session), language)
    })
  }
})

test('WHAT[provider-language-013] companion instruction exports contain Chinese prose under a Chinese preference', async () => {
  await withPreference('zh-CN', async () => {
    const companion = await import('../../../dist/Context/Companion/ProjectionSurface.js')
    for (const text of [companion.normalInstruction, companion.squashInstruction, companion.memoryPreamble]) {
      assert.match(text, /[\u4e00-\u9fff]/)
    }
  })
})

test('WHAT[provider-language-013] horizon prose contains Chinese while the technical participant label survives', async () => {
  await withPreference('zh-CN', async () => {
    const horizon = await import('../../../dist/Execution/Session/OpenCode/HorizonSurface.js')
    assert.match(horizon.description(), /[\u4e00-\u9fff]/)
    const roster = horizon.render([{ label: 'engineer', status: 'active', work: 'none', record: '' }], [])
    assert.match(roster, /[\u4e00-\u9fff]/)
    assert.match(roster, /engineer/)
  })
})
