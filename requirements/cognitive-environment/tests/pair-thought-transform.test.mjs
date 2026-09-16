// Split from tests/unit/host/pair-thought-transform.test.mjs (cutover Wave 2a);
// owner: cognitive-environment. CE_013: PAIR_HINT marker 正文 craft ——
// canonical pair-thought text 鼓励持续 ready-frontier 并发，且不含全局并发数字。
// anchor/replay 机制断言归 prefix-stability；Cursor wire 渲染归 provider-projection。

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

// The canonical pair-hint craft text lives at the provider resource
// resources/provider/host/pair-programming-guideline/en.md; PairProgrammingThoughtTransform
// renders that exact file (no {{placeholder}} substitution) for the English language.
// Reading the resource directly is the JS-native surface: same bytes, no dist import.
const text = readFileSync(
  join(dirname(fileURLToPath(import.meta.url)), '../../../resources/provider/host/pair-programming-guideline/en.md'),
  'utf8',
)

test('WHAT[COGNITIVE-ENVIRONMENT-013] PAIR_HINT_canonical_text_encourages_continuous_ready_frontier_without_global_N', () => {
  assert.match(text, /parallel|concurr|并行|并发/i)
  assert.match(text, /ready frontier/i)
  assert.match(text, /A1/)
  assert.match(text, /sibling|兄弟/i)
  assert.match(text, /execution schedule|执行日程/i)
  assert.match(text, /依赖|dependenc/i)
  assert.doesNotMatch(text, /最多\s*\d+|max(?:imum)?\s+\d+/i)
})

test('WHAT[COGNITIVE-ENVIRONMENT-013] PAIR_HINT_canonical_text_teaches_abstract_then_commit', () => {
  assert.match(text, /abstract|抽象/i)
  assert.match(text, /commit|笃定/i)
  assert.match(text, /`assume`/)
  assert.match(text, /no new knowledge|不产生新知识/i)
  assert.doesNotMatch(
    text,
    /domino|多米诺/i,
    'the repeated Pair Hint should leave the long psychological reinforcement to assume',
  )
})
