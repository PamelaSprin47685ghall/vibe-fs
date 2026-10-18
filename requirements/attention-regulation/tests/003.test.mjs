import assert from 'node:assert/strict'
import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')
const listFiles = (dir) => {
  if (!existsSync(dir)) return []
  return readdirSync(dir).flatMap((entry) => {
    const full = resolve(dir, entry)
    return statSync(full).isDirectory() ? listFiles(full) : [full]
  })
}

// Superseded by the programming surface (todowrite-for-<role>), ANCHOR view.extra prose, and assume.
test('WHAT[attention-regulation-003] defer is retired from tool registration, Pair Hint, and provider resources, superseded by ANCHOR view.extra and assume', () => {
  const staticTools = read('src/Wanxiangshu/OpenCode/Tools/StaticTools.fs')
  const toolRegistry = read('src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs')
  const toolMap = JSON.parse(read('resources/ablation/tool-map.json'))
  assert.doesNotMatch(staticTools, /"defer"/)
  assert.doesNotMatch(toolRegistry, /AttentionTools/)
  assert.doesNotMatch(toolRegistry, /"defer"/)
  assert.equal('defer' in (toolMap.tools ?? {}), false)

  const pairEn = read('resources/provider/host/pair-programming-guideline/en.md')
  const pairZh = read('resources/provider/host/pair-programming-guideline/zh-CN.md')
  assert.doesNotMatch(pairEn, /\bdefer\b/i)
  assert.doesNotMatch(pairZh, /\bdefer\b/i)

  const providerDir = resolve(root, 'resources/provider/attention-regulation')
  assert.equal(
    listFiles(providerDir).length,
    0,
    'resources/provider/attention-regulation must be deleted',
  )

  const viewExtraEn = read('resources/provider/tool/js-program/view-declaration-extra/en.md')
  const viewExtraZh = read('resources/provider/tool/js-program/view-declaration-extra/zh-CN.md')
  assert.match(viewExtraEn, /prose value/)
  assert.match(viewExtraZh, /散文值/)
})
