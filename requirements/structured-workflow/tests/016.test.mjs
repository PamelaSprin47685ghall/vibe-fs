import assert from 'node:assert/strict'
import test from 'node:test'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { checkSubsystems } from '../../../scripts/checks/subsystems.mjs'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[STRUCTURED-WORKFLOW-016] release architecture has one subsystem authority', () => {
  const report = checkSubsystems(repoRoot)
  assert.equal(report.ok, true)
})
