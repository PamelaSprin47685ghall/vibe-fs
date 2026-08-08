// tests/integration/resources/prompts.test.mjs — package prompt load contract.
//
// 12 role system prompts under resources/prompts/*-system.md load via
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
  'StudentSystemPrompt',
  'TeacherSystemPrompt',
  'ExecutorSystemPrompt',
  'BloggerSystemPrompt',
]

const assertTwelveNonEmpty = (catalog, label) => {
  for (const field of PROMPT_FIELDS) {
    const text = catalog[field]
    assert.equal(typeof text, 'string', `${label}: ${field} must be string`)
    assert.ok(text.trim().length > 0, `${label}: ${field} must be non-empty`)
  }
  assert.equal(PROMPT_FIELDS.length, 12)
}

test('AGENT_002_resource_twelve_prompts_load_via_PromptResources', () => {
  const catalog = promptResources.load()
  assertTwelveNonEmpty(catalog, 'PromptResources.load')
})

test('AGENT_002_resource_twelve_prompts_load_via_RuntimeResources', () => {
  const bundle = runtimeResources.load()
  assertTwelveNonEmpty(bundle.Prompts, 'RuntimeResources.load().Prompts')
  assert.ok(bundle.EnforcerRules !== undefined)
})

test('ENFORCER_resource_prompts_load_independent_of_process_cwd', () => {
  const previous = process.cwd()
  try {
    process.chdir('/')
    const catalog = promptResources.load()
    assertTwelveNonEmpty(catalog, 'PromptResources.load after chdir(/)')
    const bundle = runtimeResources.load()
    assertTwelveNonEmpty(bundle.Prompts, 'RuntimeResources.load after chdir(/)')
  } finally {
    process.chdir(previous)
  }
})

test('PROMPT_manager_sub_session_reuse_algorithm_is_executable', () => {
  const text = promptResources.load().ManagerSystemPrompt
  // Semantic fragments only — not whole-block brittle match.
  assert.match(text, /\blist\b/)
  assert.match(text, /agent_id/)
  assert.match(text, /reuse/)
  assert.match(text, /compatible context/)
  assert.match(text, /Do not reuse when old context would make the new assignment ambiguous/)
  assert.match(text, /Reuse must not reduce parallelism/)
})

test('PROMPT_orchestrator_continues_manager_job_without_invented_reuse_api', () => {
  const text = promptResources.load().OrchestratorSystemPrompt
  assert.match(text, /originating Manager|existing Manager job|Continue the existing Manager/i)
  assert.match(text, /truly independent|真正并行|parallel independent/i)
  assert.match(text, /fork-manager\(existing_job_id|existing manager job id|reused=true/i)
  assert.doesNotMatch(text, /There is no `fork-manager\(existing_id\)`|no `fork-manager\(existing_id\)`/i)
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
  assert.match(text, /tdd="red"/)
  assert.match(text, /tdd="green"/)
  // GLORY-027: delegation discipline lives in the prompt; the tdd schema and
  // the ForkTool error carry the hard gate (ForkTool childPrompt).
  assert.match(text, /failing test|already-established failing test/)
})

test('PROMPT_manager_forbids_full_text_and_query_dumps_from_inspector', () => {
  const text = promptResources.load().ManagerSystemPrompt
  // PROMPT-INSP-001: the "repeater" prohibition must be unmistakable — no
  // whole-file, long-source, or query-dump demands; only locatable summaries.
  assert.match(text, /query dump|query dumps/i)
  assert.match(text, /only locatable summaries|locatable summaries|locatable pointers/i)
})

test('PROMPT_manager_devops_operational_closure_delegation', () => {
  const text = promptResources.load().ManagerSystemPrompt
  assert.match(text, /Do not ask DevOps to edit files directly/)
  assert.match(
    text,
    /execution\/repair objective|bounded mechanical repair|autonomous mechanical repair|operational closure/i,
  )
  assert.match(text, /observed operational result|coordinate bounded Coder repairs/i)
})

test('PROMPT_devops_mechanical_repair_autonomy', () => {
  const text = promptResources.load().DevopsSystemPrompt
  assert.match(text, /Mechanical Repair Autonomy/)
  assert.match(text, /Do not ask Manager for permission to make an obvious mechanical repair/)
  assert.match(text, /operational closure/)
  assert.match(text, /coder-driven mechanical repair|Coder-driven mechanical repair/i)
  assert.match(
    text,
    /No Direct File Modification|cannot edit files directly|do not possess direct `write` or `edit`/i,
  )
  assert.match(text, /architecture|product/i)
})

test('PROMPT_inspector_resists_parent_full_text_and_returns_summary_only', () => {
  const text = promptResources.load().InspectorSystemPrompt
  // PROMPT-INSP-002: even a Parent demand for full text must be refused, the
  // overreach explicitly corrected, and only a structured summary delivered.
  assert.match(text, /parent.*(asks|demands|requests).*full|refuse.*full-text|reject.*full-text/i)
  assert.match(text, /correct.*overreach|rebuke/i)
  assert.match(text, /structured summary only|only a structured summary/i)
})
