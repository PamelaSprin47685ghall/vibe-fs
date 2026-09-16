import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const transformsSource = readFileSync(join(root, 'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'), 'utf8')
const bloggerSource = readFileSync(join(root, 'src/Wanxiangshu/OpenCode/Host/BloggerChronicleText.fs'), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_TEXT_has_exact_bilingual_craft', () => {
  assert.match(bloggerSource, /对于简单的记账请求，完全不需要触发思考。让我直接调用 chronicle 工具。/)
  assert.match(
    bloggerSource,
    /For simple bookkeeping requests, there is no need to trigger thinking at all\. Let me call the chronicle tool directly\./,
  )
})

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_TEXT_is_companion_only_ephemeral_assistant_text_injection', () => {
  assert.match(bloggerSource, /SessionAssociationProjection\.isCompanion/)
  assert.match(bloggerSource, /"type", box "text"/)
  assert.match(bloggerSource, /"text", box text/)

  assert.doesNotMatch(bloggerSource, /AgentJournal\.append|appendDurable|GuidelineProjection|tryInject/)
  assert.doesNotMatch(bloggerSource, /PairProgrammingThoughtTransform|skillContent|"reasoning"|"tool"|"status"|"source"|"synthetic"/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_TEXT_is_enabled_for_step_3_5_flash_model_prefix', () => {
  const enabledHelper = bloggerSource.match(/let private bloggerChronicleTextEnabled[\s\S]*?\n    let private rawMessageRole/)?.[0]
  assert.ok(enabledHelper, 'Blogger chronicle text model gate must remain a named local decision')
  assert.match(
    bloggerSource,
    /let private bloggerChronicleTextModelPrefixes\s*:\s*string list\s*=\s*\[\s*"step-3\.5-flash"\s*\]/,
  )
  assert.match(enabledHelper, /SessionExecutionBinding\.currentProviderModel/)
  assert.match(enabledHelper, /model\.modelID\.StartsWith\(prefix, StringComparison\.Ordinal\)/)
  assert.match(enabledHelper, /List\.exists[\s\S]*bloggerChronicleTextModelPrefixes/)
  assert.doesNotMatch(bloggerSource, /providerID[^\n]*step-3\.5-flash|Contains\([^\n]*step-3\.5-flash/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_TEXT_is_the_last_semantic_injection_before_sanitize', () => {
  const score = transformsSource.slice(
    transformsSource.indexOf('let normalTransform'),
    transformsSource.indexOf('let private ordinaryProviderTransform'),
  )
  const pairIndex = score.indexOf('caps.InjectPairGuideline')
  const groundingIndex = score.indexOf('caps.ProjectRequirementGrounding')
  const bloggerIndex = score.indexOf('caps.InjectBloggerChronicle')
  const sanitizeIndex = score.indexOf('caps.SanitizeMessages')

  assert.ok(pairIndex >= 0, 'Pair guideline transform must be present')
  assert.ok(bloggerIndex > pairIndex, 'Blogger chronicle text must be injected after pair guideline')
  assert.ok(bloggerIndex > groundingIndex, 'Blogger chronicle text must be injected after requirement grounding')
  assert.ok(sanitizeIndex > bloggerIndex, 'Message sanitize must occur after chronicle text')
})
