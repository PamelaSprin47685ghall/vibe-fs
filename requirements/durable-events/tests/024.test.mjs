import assert from 'node:assert/strict'
import test from 'node:test'
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import * as eventCodec from '../../../dist/Persistence/EventStore/CodecSurface.js'
import { readCompileShardInventory } from '../../../scripts/lib/compile-shards.mjs'
import { buildSubsystemInventory } from '../../../scripts/checks/subsystems.mjs'
import { assertEffectIsInjected, assertFatalBoundary, assertPureContract } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

const ROOT = resolve(import.meta.dirname, '../../..')

const SOURCE_ROOT = join(ROOT, 'src/Wanxiangshu')

const event = ({
  id = '1111111111111111111111111111111111111111',
  payload = { state: 'open' },
} = {}) => ({
  id,
  stream: 'proof/canonical-codec-slice',
  type: 'JobRequested',
  parents: [],
  payload,
  payloadRefs: [],
})

function collectSourceFiles(directory) {
  const found = []
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) found.push(...collectSourceFiles(path))
    else if (/\.[fs]i?$/.test(entry.name) || entry.name.endsWith('.fs')) found.push(path)
  }
  return found
}

test('WHAT[durable-events-024] semantic cut fatal requires settlement and one injected physical fuse', () => {
  assertFatalBoundary('durable-events')
})
