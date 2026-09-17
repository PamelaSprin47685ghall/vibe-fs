import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { acceptAuthorityRoot, grantWorkOwned, withExecutablePlugin, withPlugin } = await import("../../verification-system/tests/support/plugin-fixture.mjs");


test('WHAT[ENF-009] FORK_specs_expose_expected_names_and_only_manager_fork_carries_keywords', async () => {
  await withPlugin(async (hooks) => {
    assert.equal(hooks.tool.fork !== undefined, true)
    assert.equal(hooks.tool.commission !== undefined, true)
    const forkArgs = ['calling', 'name', 'charge', 'keywords', 'attach', 'expected_tool_calls']
    const commissionArgs = ['calling', 'name', 'charge', 'expected_tool_calls']
    for (const name of forkArgs) assert.equal(typeof hooks.tool.fork.args[name]?.safeParse, 'function', `fork.${name}`)
    for (const name of commissionArgs) assert.equal(typeof hooks.tool.commission.args[name]?.safeParse, 'function', `commission.${name}`)
    assert.equal(hooks.tool.commission.args.keywords, undefined, 'commission must not carry warm-start keywords')
  })
})
test('WHAT[ENF-009] FORK_disposed_or_unbound_execution_surfaces_natural_execution_consequence', async () => {
  await withExecutablePlugin(async (hooks) => {
    const result = await hooks.tool.fork.execute(
      { calling: 'engineer', name: 'Ada', charge: 'do work' },
      { sessionID: '', agent: 'engineer' },
    )
    assert.match(result, /cannot be placed from this execution context|caller's authority is established|调用方权威确立之前/i)
    assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  })
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { rolePredicate } = await import("../../../dist/OpenCode/Tools/ToolRegistrySurface.js");
const { generateRole } = await import("../../../dist/Repository/Programming/Js/GeneratorSurface.js");
const { allRoleLabels } = await import("../../../dist/Foundation/RolesSurface.js");
const { LEGACY_FORBIDDEN_NAMES, extractKnownToolNames, scanEntries, scanRepo } = await import("../../../scripts/checks/tool-referential-integrity.mjs");

const LEGACY_VERDICT = `
module VerdictTool =
    let spec factory scope =
        { Name = "verdict"
          Description = "legacy"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`
const STATIC_TOOLS_SNIPPET = `
module StaticTools =
    let knownToolNames =
        [ "fork"
          "resume"
          "commission"
          "join"
          "horizon" ]
`
const REGISTRY_SNIPPET = `
module ToolRegistry =
    let rolePredicate specName parkedHost sessionId =
        match specName with
        | "fork" -> fun _ -> true
        | "join" -> fun _ -> true
        | _ -> fun _ -> false
`

test('WHAT[ENF-009] gate_a_documents_legacy_forbidden_names', () => {
  assert.ok(LEGACY_FORBIDDEN_NAMES.includes('verdict'))
  assert.ok(LEGACY_FORBIDDEN_NAMES.includes('list'))
  assert.ok(LEGACY_FORBIDDEN_NAMES.includes('inspect'))
})
test('WHAT[ENF-009] gate_a_legacy_tool_name_is_red', () => {
  const violations = scanEntries([{ file: 'VerdictTool.fs', text: LEGACY_VERDICT }])
  assert.ok(violations.some((v) => v.code === 'legacy-tool-name' && v.detail?.includes('verdict')))
})
test('WHAT[ENF-009] gate_a_unknown_tool_not_in_static_is_red', () => {
  const customSpec = `
module CustomTool =
    let spec factory scope syncDelegate =
        { Name = "custom-unknown"
          Description = "custom"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`
  const violations = scanEntries([{ file: 'CustomTool.fs', text: customSpec }], {
    staticTools: STATIC_TOOLS_SNIPPET,
    toolRegistry: REGISTRY_SNIPPET,
  })
  assert.ok(violations.some((v) => v.code === 'unknown-tool-not-in-static' && v.detail?.includes('custom-unknown')))
})
test('WHAT[ENF-009] gate_a_extract_known_tool_names', () => {
  assert.deepEqual(extractKnownToolNames(STATIC_TOOLS_SNIPPET), ['fork', 'resume', 'commission', 'join', 'horizon'])
})
test('WHAT[ENF-009] gate_a_repo_scan_is_green', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { rolePredicate } = await import("../../../dist/OpenCode/Tools/ToolRegistrySurface.js");


test('WHAT[ENF-009] TOOLSPEC_review_and_finality_tools_have_owner_defined_admission', () => {
  // review: Manager only (Relay)
  assert.equal(rolePredicate('review', 'manager'), true)
  assert.equal(rolePredicate('review', 'engineer'), false)

  // suicide: Manager only
  assert.equal(rolePredicate('suicide', 'manager'), true)
  assert.equal(rolePredicate('suicide', 'engineer'), false)
})
test('WHAT[ENF-009] TOOLSPEC_unknown_tools_and_host_natives_fail_closed', () => {
  assert.equal(rolePredicate('nonexistent-tool', 'engineer'), false)
  assert.equal(rolePredicate('read', 'engineer'), false)
  assert.equal(rolePredicate('write', 'engineer'), false)
  assert.equal(rolePredicate('skill', 'engineer'), false)
})
}
