import test from 'node:test'

test.todo('WHAT[context-compression-001] compile and audit the actual compression dependency boundary for absence of model-capacity authority; vocabulary scans cannot establish this property; GAP-104')

test('WHAT[context-compression-001] the real OpenCode hooks share one provider calibration owner and clear it on deletion', async () => {
  const assert = (await import('node:assert/strict')).default
  const calibration = (await import('wanxiangli/provider-calibration')).ProviderCalibration
  const params = await import('../../../dist/OpenCode/Host/ChatParamsSurface.js')
  const { withExecutablePlugin } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  const session = 'calibration-extracted-owner'
  calibration.clearAll()

  const seen = params.apply(
    { sessionID: session, model: { limit: { context: 32000 } } },
    { model: { modelID: 'host-model' } },
  )
  assert.equal(seen.ok, true, seen.error)
  assert.equal(calibration.limit(session), 32000)
  assert.equal(calibration.usage(session), null)

  await withExecutablePlugin(async (hooks) => {
    await hooks.event({
      event: {
        type: 'message.updated',
        properties: {
          info: {
            id: 'calibration-real-assistant',
            sessionID: session,
            role: 'assistant',
            tokens: { cache: { read: 456 }, input: 123 },
          },
        },
      },
    })
    assert.deepEqual(calibration.usage(session), { cached: 456, uncached: 123 })
    assert.equal(calibration.limit(session), 32000)

    await hooks.event({
      event: { type: 'session.deleted', properties: { sessionID: session } },
    })
    assert.equal(calibration.usage(session), null)
    assert.equal(calibration.limit(session), null)
  })
  calibration.clearAll()
})
