import assert from 'node:assert/strict'
import { withPlugin } from '../plugin-fixture.mjs'
import { integrationTest } from '../tier-gate.mjs'

process.stdout.write('plugin fixture imported\n')

const mode = process.argv[2]
if (mode === 'skip') {
  integrationTest('unused plugin fixture', async () => {
    await withPlugin(() => assert.fail('disabled integration tier must not run'))
  })
} else if (mode === 'create') {
  await withPlugin(async (hooks) => {
    assert.equal(typeof hooks.tool.defer.execute, 'function')
    assert.equal(typeof hooks.dispose, 'function')
    process.stdout.write('plugin fixture created\n')
  })
} else {
  assert.equal(mode, 'import')
}
