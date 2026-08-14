// Split from tests/unit/host/pair-thought-transform.test.mjs (cutover Wave 2a);
// owner: cognitive-environment. CE_013: PAIR_HINT marker 正文 craft ——
// canonical pair-thought text 鼓励 NEEDHELP 与并行 wave，且不含全局并发数字。
// anchor/replay 机制断言归 prefix-stability；Cursor wire 渲染归 provider-projection。

import assert from 'node:assert/strict'
import test from 'node:test'

const { text } = await import('../../../dist/Infrastructure/OpenCode/Host/PairProgrammingThoughtTransform.js')

test('PAIR_HINT_canonical_text_encourages_needhelp_and_parallel_wave_without_global_N', () => {
  assert.match(text, /\[NEEDHELP\]/)
  assert.match(text, /并行|parallel/i)
  assert.match(text, /依赖|dependenc/i)
  assert.doesNotMatch(text, /最多\s*\d+|max(?:imum)?\s+\d+/i)
})
