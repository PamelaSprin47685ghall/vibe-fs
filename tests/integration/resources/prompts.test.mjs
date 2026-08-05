// tests/integration/resources/prompts.test.mjs — package prompt load contract.
//
// 10 role system prompts under resources/prompts/*-system.md load via
// PromptResources / RuntimeResources; path is import.meta.url-relative, not cwd.
//
// Not discovered by tests/unit/runner.mjs. Run standalone:
//   node --test tests/integration/resources/prompts.test.mjs
// (requires dist/ built; import through tests/unit/support/domain.mjs facade)

import assert from 'node:assert/strict'
import test from 'node:test'
import { promptResources, runtimeResources } from '../../unit/support/domain.mjs'

const PROMPT_FIELDS = [
  'ManagerSystemPrompt',
  'CoderSystemPrompt',
  'DevopsSystemPrompt',
  'InspectorSystemPrompt',
  'ReviewerSystemPrompt',
  'BrowserSystemPrompt',
  'MeditatorSystemPrompt',
  'OrchestratorSystemPrompt',
  'ExecutorSystemPrompt',
  'BloggerSystemPrompt',
]

const assertTenNonEmpty = (catalog, label) => {
  for (const field of PROMPT_FIELDS) {
    const text = catalog[field]
    assert.equal(typeof text, 'string', `${label}: ${field} must be string`)
    assert.ok(text.trim().length > 0, `${label}: ${field} must be non-empty`)
  }
  assert.equal(PROMPT_FIELDS.length, 10)
}

test('ENFORCER_resource_ten_prompts_load_via_PromptResources', () => {
  const catalog = promptResources.load()
  assertTenNonEmpty(catalog, 'PromptResources.load')
})

test('ENFORCER_resource_ten_prompts_load_via_RuntimeResources', () => {
  const bundle = runtimeResources.load()
  assertTenNonEmpty(bundle.Prompts, 'RuntimeResources.load().Prompts')
  assert.ok(bundle.EnforcerRules !== undefined)
})

test('ENFORCER_resource_prompts_load_independent_of_process_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    const catalog = promptResources.load()
    assertTenNonEmpty(catalog, 'PromptResources.load after chdir(/)')
    const bundle = runtimeResources.load()
    assertTenNonEmpty(bundle.Prompts, 'RuntimeResources.load after chdir(/)')
  } finally {
    process.chdir(previous)
  }
})

test('PROMPT_manager_sub_session_reuse_algorithm_is_executable', () => {
  const text = promptResources.load().ManagerSystemPrompt
  // Semantic fragments only — not whole-block brittle match.
  assert.match(text, /sub-session 复用/)
  assert.match(text, /\blist\b/)
  assert.match(text, /agent_id/)
  assert.match(text, /优先复用/)
  assert.match(text, /不得再次传 managed agent 名称/)
  assert.match(text, /向同一 handle 发送 nudge/)
  assert.match(text, /prefix cache/)
  // Wrong vs right call shape (managed name vs existing agent_id).
  assert.match(text, /fork\("fast-coder"/)
  assert.match(text, /fork\("[a-z0-9]{6}"/)
})

test('PROMPT_orchestrator_continues_manager_job_without_invented_reuse_api', () => {
  const text = promptResources.load().OrchestratorSystemPrompt
  assert.match(text, /originating Manager|existing Manager job|Continue the existing Manager/i)
  assert.match(text, /truly independent|真正并行|parallel independent/i)
  assert.match(text, /There is no `fork-manager\(existing_id\)`|no `fork-manager\(existing_id\)`/i)
})

test('PROMPT_coder_tdd_phase_discipline_and_scope', () => {
  const text = promptResources.load().CoderSystemPrompt
  assert.match(text, /red → green → refactor|red → green/)
  assert.match(text, /tdd/)
  assert.match(text, /Do not delete, skip, loosen, or rewrite/)
  assert.match(text, /schema-required|schema-optional/)
  assert.match(text, /Manager `fork` of a Coder role|prompt-required/)
  assert.match(text, /DevOps or the parent agent must run the targeted suite/)
})

test('PROMPT_devops_coder_tdd_workflow_requires_observed_red', () => {
  const text = promptResources.load().DevopsSystemPrompt
  assert.match(text, /tdd="red"/)
  assert.match(text, /tdd="green"/)
  assert.match(text, /Confirm true red\/green|confirm.*red.*green|true red\/green/i)
  assert.match(text, /named `coder` tool|synchronous `coder` tool/)
  assert.match(text, /schema optional `tdd`|prompt-required for `fast-coder`|Manager `fork` of a Coder role/)
  assert.match(text, /verbal claim is not enough|actually observe/)
})

test('PROMPT_manager_fork_coder_requires_tdd', () => {
  const text = promptResources.load().ManagerSystemPrompt
  assert.match(text, /tdd/)
  assert.match(text, /Required when the target is a coder role|fork a coder role without `tdd`/)
  assert.match(text, /fast-coder.*deep-coder|coder role/i)
  assert.match(text, /tdd="red"/)
  assert.match(text, /tdd="green"/)
  // Reuse path also requires tdd for coder handles.
  assert.match(text, /fork\("a1b2c3", tdd=/)
})
