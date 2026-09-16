import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import { parse as parseToml } from 'smol-toml'
import * as joinSurface from '../../../dist/Execution/Delegation/Fork/OpenCode/JoinSurface.js'
import { FORBIDDEN_DTO_PATTERNS, scanText } from '../../../scripts/checks/provider-leak-gate.mjs'

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const LOCALES = ['en', 'zh-CN']

const LEGACY_DTO = /\b(status|count|ordinal|kind|agent|code|message)\s*=|\\[\\[result\\]\\]|\\[error\\]|work_record\s*=/
const assertClean = (wire, label) => assert.ok(!LEGACY_DTO.test(wire), `${label}: ${wire}`)

const failed = (over = {}) => ({ kind: 'failed', agentId: 'a1', agentName: 'inspector', role: 'Inspector', runId: 'run-a1', code: 'E1', message: 'boom', ...over })
const pty = (kind, over = {}) => ({ kind, ptyId: 'pty-1', terminalLabel: 'npm test', outcome: 'exit 0', code: '', message: '', ...over, ...(kind ? { kind } : {}) })
const completed = (over = {}) => ({ kind: 'completed', agentId: 'a1', agentName: 'coder', role: 'Coder', runId: 'run-a1', workRecord: '', ...over })

const LEAKY_JOIN = `
module JoinResultRenderer =
    let renderInterrupted reason =
        field "status" (str "interrupted")
        field "pty_id" (str payload.PtyId)
        SessionId.value sid
`

test('WHAT[PARTICIPANT-HORIZON-003] PH_exec_030_no_generic_state_dto_vocabulary_in_join_or_horizon_descriptions', () => {
  const dtoVocabulary = /\b(status|session_id|agent_id|pty_id|code|ordinal|kind|count)\b/i
  for (const tool of ['join', 'horizon']) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/tool/${tool}/description/${locale}.md`)
      assert.doesNotMatch(text, dtoVocabulary, `${tool}/${locale}.md carries state-machine DTO vocabulary`)
    }
  }
})

test('WHAT[PARTICIPANT-HORIZON-003] devops_join_deadline_renders_natural_language_not_timed_out_dto', () => {
  const wire = joinSurface.renderInterrupted('english', 'DeadlineExpired')
  assert.match(wire, /No return reached you before your waiting ended/)
  assert.equal(parseToml(wire).status, undefined)
  assert.equal(parseToml(wire).error, undefined)
})

test('WHAT[PARTICIPANT-HORIZON-003] devops_join_timed_out_fork_error_also_natural_language', () => {
  const wire = joinSurface.renderForkError('english', 'TimedOut')
  assert.match(wire, /No return reached you before your waiting ended/)
  assert.equal(parseToml(wire).status, undefined)
})

test('WHAT[PARTICIPANT-HORIZON-003] MISC_join_render_batch_pty_aborted_natural_language', () => {
  const wire = joinSurface.renderBatch('english', [pty('pty-aborted', { ptyId: 'pty-3', outcome: 'interrupted', code: 'AB', message: 'esc' })])
  assert.match(wire, /# npm test was interrupted\./)
  assert.match(wire, /output = "esc"/)
  assert.ok(!wire.includes('pty_id'))
})

test('WHAT[PARTICIPANT-HORIZON-003] MISC_join_render_batch_multiple_items_stable_order', () => {
  const wire = joinSurface.renderBatch('english', [
    failed({ message: 'boom' }),
    pty('pty-aborted', { ptyId: 'p', terminalLabel: 'Terminal', outcome: 'x', code: 'C', message: 'm' }),
    completed({ workRecord: 'done' }),
  ])
  assert.equal([...wire.matchAll(/could not complete/g)].length, 1)
  assert.equal([...wire.matchAll(/has returned\./g)].length, 1)
  assert.equal([...wire.matchAll(/was interrupted\./g)].length, 1)
  assertClean(wire, 'multiple')
})

test('WHAT[PARTICIPANT-HORIZON-003] MISC_join_render_interrupted_natural_language', () => {
  const operatorWire = joinSurface.renderInterrupted('english', 'OperatorAbort')
  assert.match(operatorWire, /# Your waiting was interrupted\./)
  assertClean(operatorWire, 'operator')

  const userWire = joinSurface.renderInterrupted('english', 'UserMessageArrived')
  assert.match(userWire, /# Something nearer has arrived\./)
  assertClean(userWire, 'user')

  const deadlineWire = joinSurface.renderInterrupted('english', 'DeadlineExpired')
  assert.match(deadlineWire, /# No return reached you before your waiting ended\./)
  assertClean(deadlineWire, 'deadline')
})

test('WHAT[PARTICIPANT-HORIZON-003] MISC_join_render_fork_error_natural_language', () => {
  const cases = [
    ['Empty', /nothing away to receive/],
    ['NothingToJoin', /nothing away to receive/],
    ['Cancelled', /wait was cancelled/],
    ['JoinInProgress', /already in progress/],
    ['Abandoned', /did not return from this charge/],
    ['NotFound', /No one by that name is away/],
    ['TimedOut', /waiting ended/],
    ['TerminalMaterializationFailed', /return could not be gathered/],
  ]
  for (const [error, pattern] of cases) {
    const wire = joinSurface.renderForkError('english', error)
    assert.match(wire, pattern, error)
    assertClean(wire, error)
    assert.equal(parseToml(wire).status, undefined, error)
  }
})

test('WHAT[PARTICIPANT-HORIZON-003] JOIN_SURFACE_interrupt_and_fork_error_are_natural_language_only', () => {
  assertClean(joinSurface.renderInterrupted('english', 'OperatorAbort'), 'operator abort')
  assertClean(joinSurface.renderForkError('english', 'NothingToJoin'), 'nothing to join')
  assertClean(joinSurface.renderForkError('english', 'TimedOut'), 'timed out')
})

test('WHAT[PARTICIPANT-HORIZON-003] gate_b_documents_forbidden_dto_patterns', () => {
  assert.ok(FORBIDDEN_DTO_PATTERNS.some((p) => p.id === 'field-status'))
})

test('WHAT[PARTICIPANT-HORIZON-003] gate_b_leaky_renderer_fixture_is_red_for_dto_fields', () => {
  const hits = scanText('JoinResultRenderer.fs', LEAKY_JOIN)
  assert.ok(hits.some((h) => h.id === 'field-status'))
})
