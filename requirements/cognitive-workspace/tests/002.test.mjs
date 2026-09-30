import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-002] assume exposes no canvas or todo protocol in production source', () => {
  const assume = read('src/Wanxiangshu/OpenCode/Tools/AssumeTool.fs')
  assert.match(assume, /"assumption"/)
  assert.doesNotMatch(assume, /ArgUpdate|ArgTodos|jqJson|CognitiveRuntime|TodoSink/)
})

test('WHAT[cognitive-workspace-007] no TodoSink compatibility bridge survives', () => {
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Participant/Cognition/TodoSink.fs')), false)
  const hooks = read('src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs')
  assert.match(hooks, /TodoWriteCompressionContract/)
  assert.doesNotMatch(hooks, /MagicTodo/)
})

test('WHAT[cognitive-workspace-008] jq is absent from runtime dependencies and assume implementation', () => {
  assert.doesNotMatch(read('package.json'), /jq-wasm/)
  assert.doesNotMatch(read('src/Wanxiangshu/OpenCode/Tools/AssumeTool.fs'), /\bjq\b/i)
})
