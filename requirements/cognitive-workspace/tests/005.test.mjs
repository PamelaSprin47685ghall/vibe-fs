import assert from 'node:assert/strict'
import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-005] no canvas runtime or jq dependency remains', () => {
  const pkg = JSON.parse(read('package.json'))
  assert.equal(pkg.dependencies?.['jq-wasm'], undefined)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Participant/Cognition/Runtime.fs')), false)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Participant/Cognition/CanvasCodec.fs')), false)
})
