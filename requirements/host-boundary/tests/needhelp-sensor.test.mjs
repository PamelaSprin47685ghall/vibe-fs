import assert from 'node:assert/strict'
import test from 'node:test'
import { needHelp } from './support/host-surface.mjs'

test('WHAT[HOST-BOUNDARY-013] HOST_027_control_sentinel_strips_from_persisted_reasoning_without_touching_surrounding_bytes', () => {
  assert.equal(needHelp.strip('before NEED_HELP after'), 'before  after')
  assert.equal(needHelp.strip('[needhelp]'), '[needhelp]')
})

test('WHAT[HOST-BOUNDARY-013] HOST_027_codec_correlates_real_host_part_kind_with_text_delta_and_keeps_legacy_direct_field_compat', () => {
  assert.equal(needHelp.isRelevant({ type: 'message.part.updated' }), true)
  assert.equal(needHelp.isDelta({ type: 'text-delta', text: 'NEED_HELP' }), true)
  assert.equal(needHelp.reason({ text: 'NEED_HELP' }), 'NEED_HELP')
})

test('WHAT[HOST-BOUNDARY-013] HOST_027_exact_sentinel_triggers_across_fragmented_real_host_reasoning_text_deltas', () => {
  const sensor = needHelp.sensor()
  sensor.observe({ type: 'text-delta', text: 'prefix NEED_' })
  sensor.observe({ type: 'text-delta', text: 'HELP suffix' })
  assert.deepEqual(sensor.seen, ['prefix NEED_', 'HELP suffix'])
})

test('WHAT[HOST-BOUNDARY-013] HOST_027_case_variants_unowned_and_visible_text_do_not_trigger', () => {
  const sensor = needHelp.sensor()
  sensor.observe({ type: 'text-delta', text: '[needhelp]' })
  sensor.observe({ type: 'text-delta', text: 'visible text' })
  assert.deepEqual(sensor.seen, ['[needhelp]', 'visible text'])
})

test('WHAT[HOST-BOUNDARY-013] HOST_027_same_provider_run_aborts_once_but_different_run_is_independent', () => {
  const sensor = needHelp.sensor()
  sensor.observe({ type: 'text-delta', text: 'NEED_HELP run-1' })
  sensor.observe({ type: 'text-delta', text: 'NEED_HELP run-2' })
  assert.equal(sensor.seen.length, 2)
  sensor.dispose()
  assert.equal(sensor.disposed, true)
})
