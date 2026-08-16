import assert from 'node:assert/strict'
import test from 'node:test'
import * as needHelp from '../../../dist/Interaction/Dispatch/OpenCode/NeedHelpSurface.js'

const partUpdated = (partType = 'reasoning') => ({
  type: 'message.part.updated',
  properties: {
    sessionID: 'ses_help',
    part: { id: 'prt_1', messageID: 'msg_1', type: partType },
  },
})

const partDelta = (field, delta) => ({
  type: 'message.part.delta',
  properties: {
    sessionID: 'ses_help',
    messageID: 'msg_1',
    partID: 'prt_1',
    field,
    delta,
  },
})

test('WHAT[HOST-BOUNDARY-013] HOST_027_control_sentinel_strips_from_persisted_reasoning_without_touching_surrounding_bytes', () => {
  assert.equal(needHelp.sentinel, '[NEEDHELP]')
  assert.equal(needHelp.strip(`before ${needHelp.sentinel} after`), 'before  after')
  assert.equal(needHelp.strip('[needhelp]'), '[needhelp]')
})

test('WHAT[HOST-BOUNDARY-013] HOST_027_codec_correlates_real_host_part_kind_with_text_delta_and_keeps_legacy_direct_field_compat', () => {
  assert.equal(needHelp.isRelevant(partUpdated()), true)
  assert.equal(needHelp.isRelevant(partDelta('text', needHelp.sentinel)), true)
  assert.equal(needHelp.isLegacyDelta(partDelta('reasoning', needHelp.sentinel)), true)
  assert.equal(needHelp.isLegacyDelta(partDelta('text', needHelp.sentinel)), false)
})

test('WHAT[HOST-BOUNDARY-013] HOST_027_exact_sentinel_triggers_across_fragmented_real_host_reasoning_text_deltas', () => {
  const first = partDelta('text', '[NEED')
  const second = partDelta('text', 'HELP]')
  assert.equal(needHelp.isRelevant(first), true)
  assert.equal(needHelp.isRelevant(second), true)
  assert.equal(`${first.properties.delta}${second.properties.delta}`, needHelp.sentinel)
})

test('WHAT[HOST-BOUNDARY-013] HOST_027_case_variants_unowned_and_visible_text_do_not_trigger', () => {
  assert.equal(needHelp.isLegacyDelta(partDelta('text', '[needhelp]')), false)
  assert.equal(needHelp.isLegacyDelta(partDelta('text', 'visible text')), false)
  assert.notEqual(needHelp.strip('[needhelp]'), '')
})
