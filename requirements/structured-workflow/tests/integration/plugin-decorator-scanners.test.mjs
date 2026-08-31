import assert from 'node:assert/strict'
import test from 'node:test'

import { scanRepo as scanDecoratorRepo } from '../../../../scripts/checks/semantic-decorator-invariant.mjs'
import { scanRepo as scanPluginRepo } from '../../../../scripts/checks/plugin-transforms-invariant.mjs'

test('WHAT[STRUCTURED-WORKFLOW-004] real_plugin_and_decorator_scanners_are_GREEN', () => {
  assert.deepEqual(scanPluginRepo(), [])
  assert.deepEqual(scanDecoratorRepo(), [])
})
