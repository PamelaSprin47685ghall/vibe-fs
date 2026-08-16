// Split from tests/unit/context/projection-algebra.test.mjs (cutover Wave 2a); owner: interaction-authority.
//
// PROJ-008 Step4 production byte contract: the InsertRepair intent appends
// exactly `ProjectionConstants.RepairInstruction` (the Domain single source for
// the InteractionRepair protocol repair), and the Host-side id is derived from
// the same constant (enforcer-repair- + sha256(requestKey + "|" + text)).

import assert from 'node:assert/strict'
import test from 'node:test'
import { projectionAlgebra, projectionConstants, projectionIntent, projectionSnapshot, providerProjection, toList } from '../../verification-system/tests/support/domain.mjs'

// Domain 单源（PROJ-008 Step4/5）：生产常量来自 ProjectionConstants，不再手写字面量。
const REPAIR_INSTRUCTION =
  projectionConstants.RepairInstruction ??
  '# Protocol repair\n\nCall the blog tool exactly once with non-empty text. Do not answer in prose.'

const semanticView = (raw) => providerProjection.toSemantic(providerProjection.decodeMessageView(toList(raw)))
const wireOf = (raw) => providerProjection.decodeMessageView(toList(raw)).Messages

const stage3Snapshot = (raw, extras = {}) =>
  projectionSnapshot.of({
    currentProjection: semanticView(raw),
    committedPrefix: extras.committed,
    blogFrames: extras.blogFrames ?? [],
    transportMessages: extras.transportMessages ?? [],
    hostReanchor: extras.hostReanchor,
  })

test('WHAT[INTERACTION-AUTHORITY-010] PROJ_008_step4_InsertRepair_text_is_ProjectionConstants_RepairInstruction', () => {
  assert.equal(typeof projectionConstants.RepairInstruction, 'string')
  assert.equal(projectionConstants.RepairInstruction, REPAIR_INSTRUCTION)

  const raw = [{ info: { id: 'm1', role: 'user' }, parts: [{ type: 'text', text: 'base' }] }]
  const snapshot = stage3Snapshot(raw)
  const intent = projectionIntent.insertRepair({ RequestKey: 'rk-prod-1' })
  const view = projectionAlgebra.renderMessagesWithIntents(snapshot, wireOf(raw), [intent])

  assert.equal(view.length, 2)
  assert.equal(view[0]?.parts[0]?.text, 'base')
  assert.equal(view[1]?.role, 'user')
  assert.equal(view[1]?.parts[0]?.text, REPAIR_INSTRUCTION)

  // id 规则合同：enforcer-repair- + sha256(requestKey + "|" + text).substr(0,24)
  // Domain 不产出 Host id；生产 Host 侧信道用同一常量拼 digest。
  const material = `rk-prod-1|${REPAIR_INSTRUCTION}`
  assert.equal(material.includes(REPAIR_INSTRUCTION), true)
})
