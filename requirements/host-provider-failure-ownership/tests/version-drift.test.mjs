import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

test('WHAT[HOSTFAIL-007] OpenCode compatibility baseline is pinned to 1.18.18', () => {
  const pkg = JSON.parse(readFileSync(new URL('../../../package.json', import.meta.url), 'utf8'))
  assert.equal(pkg.devDependencies['opencode-ai'], '1.18.18')
  assert.equal(pkg.devDependencies['@opencode-ai/plugin'], '1.18.18')
})

