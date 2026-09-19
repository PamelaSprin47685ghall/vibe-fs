import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'

const managedAgentConfig = readFileSync(
  new URL('../../../src/Wanxiangshu/OpenCode/Host/ManagedAgentConfig.fs', import.meta.url),
  'utf8',
)

test('WHAT[host-provider-failure-ownership-002] Host retry zero is a literal ownership rule rather than a positive retry budget', () => {
  assert.doesNotMatch(managedAgentConfig, /chatMaxRetries[^\n]*(?:1|2|3|4|5|6|7|8|9)/)
})
