import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

// Superseded note: AttentionToolSurface is retired alongside defer; abandon remains retired and its release semantics live in assume.
test('WHAT[attention-regulation-002] the retired abandon tool is gone and its release semantics live in assume', () => {
  const staticTools = read('src/Wanxiangshu/OpenCode/Tools/StaticTools.fs')
  const toolRegistry = read('src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs')
  const toolMap = JSON.parse(read('resources/ablation/tool-map.json'))
  assert.doesNotMatch(staticTools, /"abandon"/)
  assert.doesNotMatch(toolRegistry, /Name\s*=\s*"abandon"/)
  assert.equal('abandon' in (toolMap.tools ?? {}), false)

  const assumeZh = read('resources/provider/tool/assume/description/zh-CN.md')
  const assumeEn = read('resources/provider/tool/assume/description/en.md')
  assert.match(assumeZh, /放下/)
  assert.match(assumeEn, /releasing a self-created plan/)
})
