import assert from 'node:assert/strict'
import test from 'node:test'
import * as promptResources from '../../../../dist/Resources/PromptSurface.js'

const english = 'English'
const simplifiedChinese = 'SimplifiedChinese'

const inOrder = (text, needles) => {
  let cursor = -1
  for (const needle of needles) {
    const next = text.indexOf(needle, cursor + 1)
    assert.ok(next > cursor, `expected ${JSON.stringify(needle)} after offset ${cursor}`)
    cursor = next
  }
}

test('WHAT[COGNITIVE-ENVIRONMENT-003] PROMPT_composition_common_law_role_law_then_inherited_library', () => {
  const prompts = promptResources.load()
  inOrder(prompts.ManagerSystemPrompt, ['# # Common Law', '# # Management', '# # Office Library', '# # The Kolmogorov Book', '# # The Book of Scarcity'])
  inOrder(prompts.CoderSystemPrompt, ['# # Common Law', '# # Mutation', '# # Office Library', '# # The Kolmogorov Book'])
  inOrder(prompts.InspectorSystemPrompt, ['# # Common Law', '# # Evidence', '# # Office Library', '# # The Book of Scarcity'])
  inOrder(prompts.DevopsSystemPrompt, ['# # Common Law', '# # The Engine Room', '# # Office Library', '# # The Book of Scarcity'])

  for (const field of ['OrchestratorSystemPrompt', 'BrowserSystemPrompt', 'InquirySystemPrompt', 'DistillerSystemPrompt', 'BloggerSystemPrompt']) {
    assert.match(prompts[field], /^# # Common Law/)
    assert.doesNotMatch(prompts[field], /# # Office Library/)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-003] PROMPT_bookkeeper_inherits_common_law_and_casebook_role_law', () => {
  const en = promptResources.loadBookkeeperSystemFor(english)
  const zh = promptResources.loadBookkeeperSystemFor(simplifiedChinese)
  inOrder(en, ['# # Common Law', '# # The Casebook'])
  assert.match(zh, /^# # 共同法/)
  assert.match(zh, /# # Casebook/)
})
