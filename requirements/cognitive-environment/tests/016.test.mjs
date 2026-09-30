import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

const zh = readFileSync(new URL('../../../resources/provider/host/pair-programming-guideline/zh-CN.md', import.meta.url), 'utf8')
const assume = readFileSync(new URL('../../../resources/provider/tool/assume/description/zh-CN.md', import.meta.url), 'utf8')

test('WHAT[cognitive-environment-016] Pair Hint keeps assume brief while the tool owns its full commitment meaning', () => {
  assert.match(zh, /先抽象，再笃定/)
  assert.match(assume, /不是求证/)
  assert.match(assume, /执行并验证/)
  assert.doesNotMatch(zh, /jq|canvas|画板 schema|update, todos/)
})
