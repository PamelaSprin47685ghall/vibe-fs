import assert from 'node:assert/strict'
import test from 'node:test'
import { ordinaryEffects } from '../../../dist/OpenCode/Host/PluginTransformSurface.js'

test('WHAT[host-boundary-019] the ordinary transform runs the full static stage sequence in contract order', async () => {
  const current = await ordinaryEffects()

  assert.deepEqual(current, [
    'begin',
    'session-time',
    'deferred',
    'relay',
    'tenure-isolation',
    'capture',
    'refresh-companion',
    'companion',
    'prefix',
    'freeze-plan',
    'pair',
    'deferred',
    'sanitize',
  ])
  assert.equal(
    current.filter((effect) => effect === 'deferred').length,
    2,
    'deferred inspections must be settled before relay and reapplied after current auxiliaries',
  )
  assert.equal(current.at(-1), 'sanitize', 'sanitize must close the ordinary pipeline')
})

// 边界：第 3 步的 RetiredAttemptStopped 早退（relay 切分后立即结束、后续 stage 不执行）
// 无法经 ordinaryEffects 观察，该 Surface 的 ApplyRelayProjection 固定返回 CurrentTenure。
// 此路径需要真实 Host canary 或专门的早退注入端口，不在此硬造旁路。

test('WHAT[host-boundary-019] ordinary transform runs companion and prefix compression without suppression', async () => {
  const current = await ordinaryEffects()
  assert.ok(current.includes('companion'), 'ordinary material must execute companion')
  assert.ok(current.includes('prefix'), 'ordinary material must execute XWire to apply prefix compression')
  const companionIndex = current.indexOf('companion')
  const prefixIndex = current.indexOf('prefix')
  assert.ok(companionIndex < prefixIndex, 'companion runs before prefix compression')
})
test.todo('WHAT[host-boundary-019] every required Host capability needs a real supported-Host canary; injected transform ports prove only composition')
