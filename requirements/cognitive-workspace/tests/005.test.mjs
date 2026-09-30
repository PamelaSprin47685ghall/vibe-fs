import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-009] assume owns no session cache or mutable workspace state', () => {
  const assume = read('src/Wanxiangshu/OpenCode/Tools/AssumeTool.fs')
  assert.doesNotMatch(assume, /Dictionary|mutable|workspace|canvasJson|CurrentCanvas/)
})

test('WHAT[cognitive-workspace-010] assume prose denies proof and authority semantics', () => {
  const zh = read('resources/provider/tool/assume/description/zh-CN.md')
  assert.match(zh, /不是求证/)
  assert.match(zh, /不授予权限/)
  assert.match(zh, /执行并验证/)
})
