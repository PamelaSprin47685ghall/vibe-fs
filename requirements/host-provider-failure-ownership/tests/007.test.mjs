import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

test('WHAT[host-provider-failure-ownership-007] dependency metadata pins the required Host and plugin versions', () => {
  const pkg = JSON.parse(readFileSync(new URL('../../../package.json', import.meta.url), 'utf8'))
  assert.equal(pkg.devDependencies['opencode-ai'], '1.18.29')
  assert.equal(pkg.devDependencies['@opencode-ai/plugin'], '1.18.29')
})

test.todo('WHAT[host-provider-failure-ownership-007] compatibility gate rejects consumer and error presentation owner drift through SDK and Desktop CLI (GAP-143)')
