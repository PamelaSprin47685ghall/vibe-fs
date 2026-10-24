import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const read = (path) => readFileSync(new URL(path, import.meta.url), 'utf8')
const zh = read('../../../resources/provider/host/pair-programming-guideline/zh-CN.md')
const en = read('../../../resources/provider/host/pair-programming-guideline/en.md')
const commonZh = read('../../../resources/provider/world/common-law/zh-CN.md')
const commonEn = read('../../../resources/provider/world/common-law/en.md')

test('WHAT[cognitive-environment-013] Pair Hint carries the bound-language anchor, resetScope context boundary and Common Law mnemonic without 80/20 iron rule in both locales', () => {
  assert.match(zh, /简体中文/)
  assert.match(zh, /「我」/)
  assert.match(en, /English/)
  assert.match(en, /"I"/)
  assert.doesNotMatch(zh, /80\/20|80%|20%|强制铁律|重构上下文/)
  assert.doesNotMatch(en, /80\/20|80%|20%|mandatory|iron rule|restructure the context/)
  assert.match(zh, /resetScope/)
  assert.match(zh, /状态闭环/)
  assert.match(en, /resetScope/)
  assert.match(en, /state closed-loop/)
  assert.match(zh, /落盘的因果/)
  assert.match(en, /committed cause and effect/)
})

test('WHAT[cognitive-environment-013] both Pair Hints repeat the exact resetScope initialization order only for uninitialized user scopes', () => {
  const reminder = '[System: Estimate the turnBudget for the first fine-grained task block (state closed-loop). Then call resetScope immediately, before doing anything else.]'
  for (const text of [en, zh]) {
    assert.ok(text.includes(reminder), 'the full instruction must reach the Pair Hint unchanged')
  }
  assert.match(en, /After each new operator-authored user message, until a \x60resetScope\x60 call follows it/)
  assert.match(zh, /新的操作员用户消息.*尚未调用 \x60resetScope\x60/)
})

test('WHAT[cognitive-environment-013] full work disciplines live in Common Law; Pair Hint keeps only the mnemonic', () => {
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