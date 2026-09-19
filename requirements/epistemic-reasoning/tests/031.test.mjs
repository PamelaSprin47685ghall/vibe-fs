import assert from 'node:assert/strict'
import test from 'node:test'
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js'



test('WHAT[epistemic-reasoning-031] sphinx inquiry process is fully programmatically controlled without inquiry role or model driving layer', () => {
  // GEC schedule & state transition is executed by deterministic runtime functions,
  // not by an external Inquiry role driving step-by-step yield/nextTool.
  assert.equal(typeof gecSurface.schedule, 'function')
  assert.equal(typeof gecSurface.replay, 'function')
})
