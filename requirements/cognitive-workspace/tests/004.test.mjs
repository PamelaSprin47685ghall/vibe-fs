import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-004] the durable projection has no Cognition canvas state', () => {
  const projection = read('src/Wanxiangshu/Composition/Durable/Projection.fs')
  assert.doesNotMatch(projection, /Cognition\s*:/)
  assert.doesNotMatch(projection, /CognitiveProjection/)
})
