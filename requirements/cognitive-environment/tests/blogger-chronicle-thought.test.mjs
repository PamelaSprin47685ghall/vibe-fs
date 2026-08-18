import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const source = readFileSync(join(root, 'src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs'), 'utf8')

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_THOUGHT_has_exact_bilingual_craft', () => {
  assert.match(source, /对于简单的记账请求，完全不需要触发思考。让我直接调用 chronicle 工具。/)
  assert.match(
    source,
    /For simple bookkeeping requests, there is no need to trigger thinking at all\. Let me call the chronicle tool directly\./,
  )
})

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_THOUGHT_is_companion_only_ephemeral_reasoning_injection', () => {
  assert.match(source, /let private injectBloggerChronicleThought/)
  assert.match(source, /SessionAssociationProjection\.isCompanion/)
  assert.match(source, /"type", box "reasoning"/)
  assert.match(source, /"text", box text/)

  const helper = source.match(/let private bloggerChronicleThoughtText[\s\S]*?\n    let private strengthReplicaRuntime/)?.[0]
  assert.ok(helper, 'Blogger thought helper must remain local to PluginTransforms')
  assert.doesNotMatch(helper, /AgentJournal\.append|appendDurable|GuidelineProjection|tryInject/)
  assert.doesNotMatch(helper, /PairProgrammingThoughtTransform|skillContent|"tool"|"status"|"source"|"synthetic"/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_THOUGHT_is_enabled_for_step_3_5_flash_model_prefix', () => {
  assert.match(
    source,
    /let private bloggerChronicleThoughtModelPrefixes\s*:\s*string list\s*=\s*\[\s*"step-3\.5-flash"\s*\]/,
  )
  assert.match(source, /SessionExecutionBinding\.currentProviderModel/)
  assert.match(source, /model\.modelID\.StartsWith\(prefix, StringComparison\.Ordinal\)/)
  assert.match(source, /List\.exists[^\n]*bloggerChronicleThoughtModelPrefixes/)
  assert.doesNotMatch(source, /providerID[^\n]*step-3\.5-flash|Contains\([^\n]*step-3\.5-flash/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_THOUGHT_is_the_last_semantic_injection_before_sanitize', () => {
  const pairIndex = source.indexOf('do! maybeInjectPairGuideline')
  const bloggerIndex = source.indexOf('injectBloggerChronicleThought journal projectionSessionIdOpt outObj')
  const sanitizeIndex = source.indexOf('let currentMessages = unbox<obj array> outObj?messages |> Array.toList', bloggerIndex)

  assert.ok(pairIndex >= 0)
  assert.ok(bloggerIndex > pairIndex)
  assert.ok(sanitizeIndex > bloggerIndex)
})
