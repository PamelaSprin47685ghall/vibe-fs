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

// Superseded by the programming surface (this.ask, this.deliver, this.fork, this.join): publish is retired and its provider resources are deleted.
test('WHAT[concern-routing-003] publish is retired from ToolRegistry and provider resources, superseded by this.ask/this.deliver/this.fork/this.join', () => {
  const staticTools = read('src/Wanxiangshu/OpenCode/Tools/StaticTools.fs')
  const toolRegistry = read('src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs')
  const toolMap = JSON.parse(read('resources/ablation/tool-map.json'))
  assert.doesNotMatch(staticTools, /"publish"/)
  assert.doesNotMatch(toolRegistry, /ConcernTools/)
  assert.doesNotMatch(toolRegistry, /"publish"/)
  assert.equal('publish' in (toolMap.tools ?? {}), false)

  const providerDir = resolve(root, 'resources/provider/concern-routing')
  assert.equal(
    listFiles(providerDir).length,
    0,
    'resources/provider/concern-routing must be deleted',
  )

  assert.match(read('resources/provider/tool/js-plan/en.md'), /\bjs\.ask\(question\)/)
  assert.match(read('resources/provider/tool/js-program/deliver/en.md'), /\bdeliver\(value\)/)
  const delegationEn = read('resources/provider/tool/js-program/delegation-rules/en.md')
  assert.match(delegationEn, /\bjs\.fork\(/)
  assert.match(delegationEn, /\bjs\.join\(/)
})
