import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { join } = await import("node:path");
const { fileURLToPath } = await import("node:url");
const { default: test } = await import("node:test");

const ROOT = join(fileURLToPath(new URL('.', import.meta.url)), '../../..')
const read = (rel) => readFileSync(join(ROOT, rel), 'utf8')
const LOCALES = ['en', 'zh-CN']
const HIDDEN_ORCHESTRATION = /\b(reviewer|witness|barrier|cohort|2N|confirmation rounds?)\b|见证|屏障|评审者/i
const INTERNAL_PARTICIPANTS = /\b(blogger|distiller|bookkeeper)\b/i
const MACHINE_BINDING = /\b(fast|deep)-[a-z]+/
const MANAGER_VISIBLE_SURFACES = [
  'role/manager',
  'tool/fork/description',
  'tool/commission/description',
  'tool/horizon/description',
  'tool/join/description',
  'tool/suicide/description',
  'lifecycle/magic-todo/todowrite-description',
  'lifecycle/magic-todo/manager-guideline',
]

test('WHAT[participant-horizon-002] PH_agent_008_machine_binding_names_absent_from_provider_visible_surfaces', () => {
  for (const surface of MANAGER_VISIBLE_SURFACES) {
    for (const locale of LOCALES) {
      const text = read(`resources/provider/${surface}/${locale}.md`)
      assert.doesNotMatch(text, MACHINE_BINDING, `${surface}/${locale}.md leaks fast-/deep- binding`)
    }
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { FORBIDDEN_TOKENS } = await import("../../../scripts/checks/provider-leak-gate.mjs");


test('WHAT[participant-horizon-002] PROVIDER_IDENTITY_LEAK_gate_b_forbids_agent_and_session_ids', () => {
  for (const token of ['AgentId', 'SessionId', 'ManagerJobId', 'PtyId', 'agent_id', 'session_id', 'pty_id']) {
    assert.ok(FORBIDDEN_TOKENS.includes(token), `missing forbidden token: ${token}`)
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { FORBIDDEN_DTO_PATTERNS, FORBIDDEN_TOKENS, scanEntries, scanRepo, scanText } = await import("../../../scripts/checks/provider-leak-gate.mjs");

const CLEAN_HORIZON = `
module HorizonTool =
    let private lineForHandle handle _ =
        sprintf "# %s is still away." "Coder"

    let spec scope =
        { Name = "horizon"
          Description = "Orient to what remains at your horizon."
          Arguments = []
          Execute = fun _ _ _ -> task { return ToolHostCodec.tomlObjectWithInstructions ["# Nothing"] [] } }
`
const LEAKY_JOIN = `
module JoinResultRenderer =
    let renderInterrupted reason =
        field "status" (str "interrupted")
        field "pty_id" (str payload.PtyId)
        SessionId.value sid
`

test('WHAT[participant-horizon-002] gate_b_documents_forbidden_machine_tokens', () => {
  assert.ok(FORBIDDEN_TOKENS.includes('SessionId'))
  assert.ok(FORBIDDEN_TOKENS.includes('pty_id'))
})
test('WHAT[participant-horizon-002] gate_b_leaky_renderer_fixture_is_red_for_machine_tokens', () => {
  const hits = scanText('JoinResultRenderer.fs', LEAKY_JOIN)
  assert.ok(hits.some((h) => h.id.startsWith('token:SessionId') || h.id === 'token:pty_id'))
})
test('WHAT[participant-horizon-002] gate_b_scan_entries_aggregates', () => {
  const hits = scanEntries([
    { file: 'HorizonTool.fs', text: CLEAN_HORIZON },
    { file: 'JoinResultRenderer.fs', text: LEAKY_JOIN },
  ])
  assert.ok(hits.length >= 2)
})
test('WHAT[participant-horizon-002] gate_b_repo_scan_without_baseline_is_zero', () => {
  const result = scanRepo(process.cwd())
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.deepEqual(result.counts, {})
})
}
