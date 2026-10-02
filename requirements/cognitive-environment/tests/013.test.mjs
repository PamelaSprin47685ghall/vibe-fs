import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(new URL(path, import.meta.url), 'utf8')
const zh = read('../../../resources/provider/host/pair-programming-guideline/zh-CN.md')
const en = read('../../../resources/provider/host/pair-programming-guideline/en.md')
const commonZh = read('../../../resources/provider/world/common-law/zh-CN.md')
const commonEn = read('../../../resources/provider/world/common-law/en.md')

test('WHAT[cognitive-environment-013] Pair Hint carries the bound-language anchor in both locales', () => {
  assert.match(zh, /简体中文/)
  assert.match(zh, /我……/)
  assert.match(en, /English/)
  assert.match(en, /"I\.\.\."/)
})

test('WHAT[cognitive-environment-013] work disciplines live in Common Law, not in the per-turn Pair Hint', () => {
  for (const heading of ['## 小步快跑', '## 不要吝啬', '## 笃定', '## 先想清楚再讲', '## 内省']) {
    assert.ok(commonZh.includes(heading), heading)
  }
  for (const heading of [
    '## Small quick steps',
    '## Do not skimp',
    '## Be certain',
    '## Think before you speak',
    '## Introspection',
  ]) {
    assert.ok(commonEn.includes(heading), heading)
  }
  assert.match(commonZh, /会砸烂东西的搅扰/)
  assert.match(commonEn, /interference that would wreck/)
  assert.doesNotMatch(zh, /todowrite|retainCheckpoints|assume|就绪前沿|军令状|画板/)
  assert.doesNotMatch(en, /todowrite|retainCheckpoints|assume|ready frontier/)
})