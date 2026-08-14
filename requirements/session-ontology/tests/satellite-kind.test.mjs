// Split from tests/unit/session/satellite-runtime.test.mjs (cutover Wave 2a); owner: session-ontology.
//
// SESSION-ONTOLOGY-014：SatelliteKind 只有 Companion 一个 case（Teacher 不是
// SatelliteKind）。恢复/reuse/replacement 断言已随 SPLIT@cutover 迁
// requirements/managed-session-lifecycle/tests/satellite-runtime.test.mjs。

import assert from 'node:assert/strict'
import test from 'node:test'

import { SatelliteKind } from '../../../dist/Journal/SessionAssociation.js'

test('HOST_014_SatelliteKind_is_Companion_only', () => {
  assert.deepEqual(SatelliteKind.Companion.cases(), ['Companion'])
  assert.equal('Teacher' in SatelliteKind, false)
})
