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

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_THOUGHT_is_companion_only_ephemeral_completed_injection', () => {
  assert.match(source, /let private injectBloggerChronicleThought/)
  assert.match(source, /SessionAssociationProjection\.isCompanion/)
  assert.match(source, /blogger-chronicle-thought/)
  assert.match(source, /"status", box "completed"/)
  assert.match(source, /"tool", box PairProgrammingThoughtTransform\.toolName/)
  assert.match(source, /"name", box PairProgrammingThoughtTransform\.skillName/)

  const helper = source.match(/let private injectBloggerChronicleThought[\s\S]*?\n    let private strengthReplicaRuntime/)?.[0]
  assert.ok(helper, 'Blogger thought helper must remain local to PluginTransforms')
  assert.doesNotMatch(helper, /AgentJournal\.append|appendDurable|GuidelineProjection|tryInject/)
})

test('WHAT[COGNITIVE-ENVIRONMENT-015] BLOGGER_CHRONICLE_THOUGHT_is_the_last_semantic_injection_before_sanitize', () => {
  const pairIndex = source.indexOf('do! maybeInjectPairGuideline')
  const bloggerIndex = source.indexOf('injectBloggerChronicleThought journal projectionSessionIdOpt outObj')
  const sanitizeIndex = source.indexOf('let currentMessages = unbox<obj array> outObj?messages |> Array.toList', bloggerIndex)

  assert.ok(pairIndex >= 0)
  assert.ok(bloggerIndex > pairIndex)
  assert.ok(sanitizeIndex > bloggerIndex)
})
