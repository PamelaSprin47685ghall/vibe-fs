import assert from 'node:assert/strict'
import { existsSync, readFileSync, readdirSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = process.cwd()
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[cognitive-workspace-001] the persistent Cognition production directory is gone', () => {
  const cognitionDir = resolve(root, 'src/Wanxiangshu/Participant/Cognition')
  assert.equal(
    existsSync(cognitionDir) ? readdirSync(cognitionDir).length : 0,
    0,
    'no production Cognition files remain; an empty filesystem directory is inert and untracked',
  )
  assert.equal(
    existsSync(resolve(root, 'src/Wanxiangshu/Wanxiangshu.Owner.cognitive-workspace.participant-cognition-workspace.fsproj')),
    false,
  )
})

test('WHAT[cognitive-workspace-004] the durable projection has no Cognition canvas state', () => {
  const projection = read('src/Wanxiangshu/Composition/Durable/Projection.fs')
  assert.doesNotMatch(projection, /Cognition\s*:/)
  assert.doesNotMatch(projection, /CognitiveProjection/)
})

test('WHAT[cognitive-workspace-005] no canvas runtime or jq dependency remains', () => {
  const pkg = JSON.parse(read('package.json'))
  assert.equal(pkg.dependencies?.['jq-wasm'], undefined)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Participant/Cognition/Runtime.fs')), false)
  assert.equal(existsSync(resolve(root, 'src/Wanxiangshu/Participant/Cognition/CanvasCodec.fs')), false)
})
