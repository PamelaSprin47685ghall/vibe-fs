// requirements/host-boundary/tests/host-message-sanitize-surface.test.mjs — WHAT[HOST-BOUNDARY-011]
//
// Verifies that HostMessageProjection owns sanitizeOutputMessages entry point.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[HOST-BOUNDARY-011] HostMessageProjection owns sanitizeOutputMessages entry point', () => {
  const projection = read('src/Wanxiangshu/OpenCode/Host/HostMessageProjection.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(projection, /let\s+sanitizeOutputMessages/)
  assert.match(projection, /replaceMessagesInPlace/)
  assert.match(pt, /HostMessageProjection\.sanitizeOutputMessages/)
})
