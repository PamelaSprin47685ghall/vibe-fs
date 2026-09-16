import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')

const text = readFileSync(
  join(dirname(fileURLToPath(import.meta.url)), '../../../resources/provider/host/pair-programming-guideline/en.md'),
  'utf8',
)

test('WHAT[COGNITIVE-ENVIRONMENT-013] CE_pair_hint_requires_immediate_todowrite_refresh_when_the_account_becomes_stale', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/host/pair-programming-guideline/${locale}.md`)
    assert.match(text, /todowrite/i)
    assert.match(text, /完整最新 account|complete current account|complete latest account/i)
    assert.match(text, /继续.*之前|before (?:continuing|you continue)/i)
    assert.match(text, /阶段结束|最后.*补记|end of (?:a )?phase|batch.*later/i)
    assert.match(text, /不算更新|does not count as an update/i)
    assert.match(text, /workingOn/i)
    assert.match(text, /焦点|active focus/i)
    assert.match(text, /无变化|�准确|still accurate|has not changed/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-013] CE_pair_hint_teaches_continuous_ready_frontier_without_batch_barriers', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/host/pair-programming-guideline/${locale}.md`)
    assert.match(text, /parallel|concurr|并行|并发/i)
    assert.match(text, /ready frontier/i)
    assert.match(text, /A1/)
    assert.match(text, /解锁|unlock/i)
    assert.match(text, /兄弟|sibling/i)
    assert.match(text, /快照|snapshot/i)
    assert.match(text, /执行日程|execution schedule/i)
    assert.match(text, /wall-clock/i)
    assert.match(text, /dependenc|依赖/i)
    assert.doesNotMatch(text, /最多\s*\d+|max(?:imum)?\s+\d+/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-013] CE_pair_hint_encourages_filling_concurrency_slots', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/host/pair-programming-guideline/${locale}.md`)
    assert.match(text, /并发槽|concurrency slot/i)
    assert.match(text, /十个|ten/i)
    assert.match(text, /空着|empty/i)
    assert.match(text, /ready/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-013] CE_pair_hint_points_non_linear_work_to_assume_without_repeating_the_manual', () => {
  for (const locale of ['en', 'zh-CN']) {
    const hint = read(`resources/provider/host/pair-programming-guideline/${locale}.md`)
    const description = read(`resources/provider/tool/assume/description/${locale}.md`)

    assert.match(hint, /`assume`/)
    assert.match(hint, /jq/i)
    assert.match(hint, /非线性|non-linear/i)
    assert.match(hint, /复杂|complex/i)
    assert.match(hint, /抽象|abstract/i)
    assert.match(hint, /执行.*验证|execute.*verify/is)
    assert.match(hint, /犹豫不产生新知识|hesitation produces no new knowledge/i)
    assert.doesNotMatch(hint, /map\(|select\(|setpath|delpaths/i, 'the repeated hint must leave jq mechanics to the tool description')

    assert.match(description, /update.*query/is)
    assert.match(description, /持久.*JSON.*画板|persistent.*JSON.*canvas/is)
    assert.match(description, /schema/i)
    assert.match(description, /犹豫不产生新知识|hesitation produces no new knowledge/i)
  }
})

test('WHAT[COGNITIVE-ENVIRONMENT-013] CE_pair_hint_rides_cursor_suffix_without_pseudo_skill_wire', () => {
  for (const locale of ['en', 'zh-CN']) {
    const text = read(`resources/provider/host/pair-programming-guideline/${locale}.md`)
    assert.match(text, /附加于工具输出尾部|appended to tool output/i)
    assert.doesNotMatch(text, /skill\(\{ name: "" \}\)/)
    assert.doesNotMatch(text, /skill\(""\)/)
    assert.doesNotMatch(text, /This prompt is injected automatically|本提示由系统自动注入/)
  }
})

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
