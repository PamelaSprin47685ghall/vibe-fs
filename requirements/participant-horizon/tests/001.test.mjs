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

test('WHAT[PARTICIPANT-HORIZON-001] PH_exec_005_horizon_description_declares_pull_only_and_hides_machinery', () => {
  for (const locale of LOCALES) {
    const text = read(`resources/provider/tool/horizon/description/${locale}.md`)
    assert.match(text, /pull-only|只在调用时主动读取一次|不?轮询|do not poll/i)
    assert.match(text, /hidden machinery|隐藏机|hidden machinery|不 dump 隐藏/i)
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

test('WHAT[PARTICIPANT-HORIZON-001] gate_b_clean_horizon_fixture_is_green', () => {
  assert.equal(scanText('HorizonTool.fs', CLEAN_HORIZON).length, 0)
})
}
