// requirements/time-capability/tests/session-started-at-bind-surface.test.mjs — WHAT[TIME-007]
//
// Verifies that SessionStartedAtLedger owns bindSessionStartedAt entry point for transform boundary.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[TIME-007] SessionStartedAtLedger owns bindSessionStartedAt entry point for transform boundary', () => {
  const ledger = read('src/Wanxiangshu/Execution/Session/SessionStartedAtLedger.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(ledger, /let\s+bindSessionStartedAt/)
  assert.match(ledger, /tryBindOrAbort\s+journal\s+projectionSessionIdOpt/)
  assert.match(pt, /SessionStartedAtLedger\.bindSessionStartedAt/)
})
