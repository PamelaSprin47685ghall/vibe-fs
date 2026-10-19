// Superseded by [context-compression-031] ANCHOR view declarations: the Blogger squash
// compression loop (ProviderRequestKind.BloggerSquash, CompanionRequestKind.Squash,
// BlogObservationsSquashed, BlogProjection.applySquash, resources/provider/lifecycle/companion/squash)
// is retired; Companion projections build Normal requests only over committed Entry frames + RecentTips.
import assert from 'node:assert/strict'
import { existsSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'
import * as blog from '../../../dist/Context/Companion/Blogger/FrameSurface.js'
import * as companionProj from '../../../dist/Context/Companion/ProjectionSurface.js'
import * as runtime from '../../../dist/Context/Companion/RuntimeSurface.js'
import * as algebra from '../../../dist/Participant/Provider/Projection/Surface.js'

const ROOT = resolve(import.meta.dirname, '../../..')

test('WHAT[context-compression-014] Blogger squash loop and resources are retired in favour of Normal Companion projection', () => {
  assert.equal(existsSync(resolve(ROOT, 'resources/provider/lifecycle/companion/squash')), false)
  assert.equal('applySquash' in blog, false)
  assert.equal('squashWidth' in blog, false)
  assert.equal('squash' in companionProj, false)
  assert.equal('squashInstruction' in companionProj, false)
  assert.equal('squash' in runtime, false)
})

test('WHAT[context-compression-014] PROJ_008_Companion_owner_normal_rows_render_through_generic_projection', () => {
  const spy = (input) => `«${input}»`
  const frames = [
    { digest: 'sha-f0', body: 'frame body 0' },
    { digest: 'sha-f1', body: 'frame body 1' },
  ]
  const dataItems = [{ role: 'user', kind: 'text', text: 'work', truncated: false }]
  const input = {
    blogger: 'ses_y',
    epoch: 0,
    kind: companionProj.normal,
    frames,
    delta: { messageId: 'msg_delta', items: dataItems },
    previousTips: [{ tipName: 'primitive-obsession', cycleId: 'c1' }],
  }

  const intent = companionProj.projectionIntent(spy, input)
  const builderPlan = companionProj.build(spy, input)

  assert.equal(intent.kind, 'ReplaceMessageBase')
  assert.deepEqual(
    intent.rows.map((row) => [row.message.role, row.message.parts[0]?.text]),
    builderPlan.messages.map((message) => [message.role, message.text]),
  )

  const snapshot = algebra.projectionSnapshot({
    providerId: null,
    modelId: null,
    variant: null,
    tools: [],
    system: [],
    messages: [],
  })
  const rendered = algebra.renderMessages(snapshot, [], [intent])
  assert.deepEqual(
    rendered.map((message) => [message.role, message.parts[0]?.text]),
    builderPlan.messages.map((message) => [message.role, message.text]),
  )
})
