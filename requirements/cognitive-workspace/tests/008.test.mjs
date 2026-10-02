import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-008] jq is absent from runtime dependencies and assume implementation', () => {
  assert.doesNotMatch(read('package.json'), /jq-wasm/)
  assert.doesNotMatch(read('src/Wanxiangshu/OpenCode/Tools/AssumeTool.fs'), /\bjq\b/i)
})
