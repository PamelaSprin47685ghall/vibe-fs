import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const zh = readFileSync(new URL('../../../resources/provider/host/pair-programming-guideline/zh-CN.md', import.meta.url), 'utf8')
const en = readFileSync(new URL('../../../resources/provider/host/pair-programming-guideline/en.md', import.meta.url), 'utf8')

test('WHAT[cognitive-environment-013] Pair Hint carries the seven numbered disciplines in both locales', () => {
  for (const tag of ['使用中文', '小步快跑', '不要吝啬', '进度更新', '极高并发', '超越常识', '善于内省']) {
    assert.match(zh, new RegExp(`\\[${tag}\\]`), tag)
  }
  for (const tag of ['Use English', 'Small quick steps', 'Do not skimp', 'Update the ledger', 'Very high concurrency', 'Beyond common sense', 'Introspect well']) {
    assert.match(en, new RegExp(`\\[${tag}\\]`), tag)
  }
})

test('WHAT[cognitive-environment-013] discipline bodies keep native todowrite, ready-frontier and assume anchors', () => {
  assert.match(zh, /todowrite/)
  assert.match(zh, /retainCheckpoints/)
  assert.match(zh, /填 1/)
  assert.match(zh, /填 2/)
  assert.match(zh, /就绪前沿/)
  assert.match(zh, /assume/)
  assert.match(en, /todowrite/)
  assert.match(en, /retainCheckpoints/)
  assert.match(en, /ready frontier/)
  assert.match(en, /assume/)
})

test('WHAT[cognitive-environment-013] Pair Hint does not revive retired canvas or Magic Todo protocols', () => {
  assert.doesNotMatch(zh, /workingOn|planComplete|obligations|update="\."|jq 画板|NEEDHELP/)
})
