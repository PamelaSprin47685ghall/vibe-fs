import assert from 'node:assert/strict'
import test from 'node:test'

import { chronicleExecutionContract } from '../../../dist/Enforcer/Surface.js'
import {
  bindManagedChild,
  withExecutablePlugin,
} from '../../verification-system/tests/support/plugin-fixture.mjs'

test('WHAT[BD-006] CHRONICLE_live_cycle_decision_is_typed_completed', () => {
  assert.deepEqual(chronicleExecutionContract(true), { kind: 'Completed', value: 'provider-result' })
})

test('WHAT[BD-006] CHRONICLE_no_live_cycle_decision_is_typed_before_host_encoding', () => {
  assert.deepEqual(chronicleExecutionContract(false), { kind: 'NoLiveCycle' })
})

test('WHAT[BD-006] CHRONICLE_no_live_cycle_aborts_then_host_adapter_exposes_sdk_error', async () => {
  await withExecutablePlugin(async (hooks, _directory, _createdIds, runtime) => {
    const parentID = 'ses-manager'
    const sessionID = 'blogger-no-live-cycle'
    bindManagedChild(parentID, sessionID, 'fast-blogger')
    await hooks['chat.message'](
      { sessionID, agent: 'fast-blogger' },
      {
        message: {
          id: `root-${sessionID}`,
          role: 'user',
          sessionID,
          agent: 'fast-blogger',
          model: { providerID: 'host', modelID: 'placeholder' },
        },
        parts: [],
      },
    )

    await assert.rejects(
      () => hooks.tool.chronicle.execute(
        { entry: 'work', tip: 'primitive-obsession' },
        { sessionID, agent: 'fast-blogger' },
      ),
      (error) => error?.message === 'CHRONICLE_NO_LIVE_CYCLE',
    )
    assert.deepEqual(runtime.abortedIds, [sessionID])
  })
})
