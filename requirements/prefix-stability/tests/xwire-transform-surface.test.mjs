// requirements/prefix-stability/tests/xwire-transform-surface.test.mjs — WHAT[PREFIX-STABILITY-001]
//
// Verifies that XWire owns applyTransform entry point for provider transform.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'
import * as XWireSurface from '../../../dist/Context/Prefix/XWireSurface.js'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[PREFIX-STABILITY-001] XWire owns applyTransform entry point for provider transform', () => {
  const wire = read('src/Wanxiangshu/Context/Prefix/Wire.fs')
  const surface = read('src/Wanxiangshu/Context/Prefix/XWireSurface.fs')
  const pt = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(wire, /let\s+applyTransform/)
  assert.match(wire, /applySessionTransform/)
  assert.match(pt, /XWire\.applyTransform/)
  for (const decision of [
    'selectProbe',
    'presentationHorizonForProbe',
    'retryTransportRetirement',
    'reconciliationDecision',
  ]) {
    assert.match(wire, new RegExp(`let ${decision}`))
    assert.match(surface, new RegExp(`XWire\\.${decision}`))
  }
  assert.doesNotMatch(surface, /List\.truncate/)
})

test('WHAT[PREFIX-STABILITY-001] compiled XWireSurface exposes the production horizon decision', () => {
  assert.equal(XWireSurface.presentationHorizon(false), 'Current')
  assert.equal(XWireSurface.presentationHorizon(true), 'TentativeCold')
})

test('WHAT[PREFIX-STABILITY-001] compiled XWireSurface reconciles completion and failure', () => {
  assert.deepEqual(
    XWireSurface.reconcile({
      hasPlan: true,
      outcome: 'completed',
      hasProbe: true,
      currentEpoch: 4,
      probeEpoch: 4,
    }),
    { promoted: true, cleared: true, keptPlan: false },
  )

  assert.deepEqual(
    XWireSurface.reconcile({ hasPlan: true, outcome: 'failed', hasProbe: true }),
    { promoted: false, cleared: true, keptPlan: false },
  )
})
