import assert from 'node:assert/strict'
import test from 'node:test'
import { acceptAuthorityRoot, grantWorkOwned, withExecutablePlugin } from '../../verification-system/tests/support/plugin-fixture.mjs'
import { textDelta } from '../../../dist/OpenCode/Host/LoopSensorSurface.js'

test('WHAT[HOST-BOUNDARY-013] bootstrap interrupts owned physical children but exempts roots and foreign sessions', async () => {
  let observe
  await withExecutablePlugin(async (hooks, _directory, createdIds, runtime) => {
    const root = 'ses-loop-root'
    await acceptAuthorityRoot(runtime, root, 'manager')
    await grantWorkOwned(runtime, root)
    const result = await hooks.tool.fork.execute(
      { calling: 'coder', name: 'Ada', charge: 'inspect the repository' },
      { sessionID: root, agent: 'manager' },
    )
    assert.equal(createdIds.length, 1, result)
    const child = createdIds[0]
    const repetitive = ' retry'.repeat(2000)
    observe(textDelta(root, repetitive, 'root-run'))
    observe(textDelta('ses-foreign', repetitive, 'foreign-run'))
    observe(textDelta(child, repetitive, 'child-run'))
    await new Promise((resolve) => setImmediate(resolve))
    assert.deepEqual(runtime.abortedIds, [child])
    observe(textDelta(child, repetitive, 'child-run'))
    await new Promise((resolve) => setImmediate(resolve))
    assert.deepEqual(runtime.abortedIds, [child], 'one physical attempt is interrupted only once')
  }, { events: { listen: (callback) => { observe = callback; return () => {} } } })
})
