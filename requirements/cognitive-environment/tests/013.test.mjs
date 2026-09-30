import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const zh = readFileSync(new URL('../../../resources/provider/host/pair-programming-guideline/zh-CN.md', import.meta.url), 'utf8')

test('WHAT[cognitive-environment-013] Pair Hint carries native todowrite, ready frontier and abstract-then-assume disciplines', () => {
  assert.match(zh, /todowrite/)
  assert.match(zh, /retainCheckpoints/)
  assert.match(zh, /填 1/)
  assert.match(zh, /填 2/)
  assert.match(zh, /ready frontier/)
  assert.match(zh, /先抽象，再笃定/)
  assert.match(zh, /assume/)
})

test('WHAT[cognitive-environment-013] Pair Hint does not revive retired canvas or Magic Todo protocols', () => {
  assert.doesNotMatch(zh, /workingOn|planComplete|obligations|update="\."|jq 画板|NEEDHELP/)
})
