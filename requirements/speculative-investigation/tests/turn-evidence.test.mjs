import assert from 'node:assert/strict'
import test from 'node:test'

import * as Evidence from '../../../dist/Application/Strength/StrengthTurnEvidence.js'
import { MessagePart } from '../../../dist/Infrastructure/OpenCode/Codec/HostMessageCodec.js'

const caseOf = (value) => value.cases()[value.tag]
const text = (value) => new MessagePart(0, [value])
const reasoning = (value) => new MessagePart(1, [value])
const call = (id, name, args) => new MessagePart(2, [id, name, args])
const result = (id, value) => new MessagePart(3, [id, value])
const activity = (kind) => new MessagePart(4, [kind])

test('STRENGTH_007_provider_output_evidence_is_not_host_bookkeeping', () => {
  assert.equal(caseOf(Evidence.classifyParts([])), 'NoOutput')
  assert.equal(caseOf(Evidence.classifyParts([activity('step-start')])), 'TransportOnly')
  assert.equal(caseOf(Evidence.classifyParts([result('c1', 'result')])), 'TransportOnly')
  assert.equal(caseOf(Evidence.classifyParts([text('answer')])), 'RealOutput')
  assert.equal(caseOf(Evidence.classifyParts([reasoning('thought')])), 'RealOutput')
  assert.equal(caseOf(Evidence.classifyParts([call('c1', 'read', '{}')])), 'RealOutput')
})
