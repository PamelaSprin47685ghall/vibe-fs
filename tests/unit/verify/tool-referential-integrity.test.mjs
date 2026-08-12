/**
 * ARCH-016 Gate A — tool referential integrity (no dist).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  LEGACY_FORBIDDEN_NAMES,
  extractKnownToolNames,
  extractToolSpecNames,
  scanEntries,
  scanRepo,
} from '../../../scripts/checks/tool-referential-integrity.mjs'

const GOOD_FORK = `
module ForkTool =
    let managerSpec factory scope =
        { Name = "fork"
          Description = "fork"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`

const GOOD_COMMISSION = `
module ForkTool =
    let orchestratorSpec factory scope =
        { Name = "commission"
          Description = "commission"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`

const DUPLICATE_OWNERS = `
module AlphaTool =
    let spec scope =
        { Name = "join"
          Description = "alpha"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }

module BetaTool =
    let spec scope =
        { Name = "join"
          Description = "beta"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`

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

test('gate_a_documents_legacy_forbidden_names', () => {
  assert.ok(LEGACY_FORBIDDEN_NAMES.includes('verdict'))
  assert.ok(LEGACY_FORBIDDEN_NAMES.includes('list'))
})

test('gate_a_extracts_tool_spec_record_names', () => {
  const names = extractToolSpecNames('ForkTool.fs', GOOD_FORK)
  assert.deepEqual(names.map((n) => n.name), ['fork'])
})

test('gate_a_duplicate_tool_name_is_red', () => {
  const entries = [
    { file: 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/AlphaTool.fs', text: DUPLICATE_OWNERS.split('\n\n')[0] + '\n' },
    { file: 'src/Wanxiangshu/Infrastructure/OpenCode/Tools/BetaTool.fs', text: DUPLICATE_OWNERS.split('\n\n')[1] + '\n' },
  ]
  const violations = scanEntries(entries)
  assert.ok(violations.some((v) => v.code === 'duplicate-tool-owner' && v.detail?.includes("'join'")))
})

test('gate_a_legacy_tool_name_is_red', () => {
  const violations = scanEntries([{ file: 'VerdictTool.fs', text: LEGACY_VERDICT }])
  assert.ok(violations.some((v) => v.code === 'legacy-tool-name' && v.detail?.includes('verdict')))
})

test('gate_a_unknown_tool_not_in_static_is_red', () => {
  const inspectSpec = `
module InspectorTool =
    let spec factory scope syncDelegate =
        { Name = "inspect"
          Description = "inspect"
          Arguments = []
          Execute = fun _ _ -> task { return "" } }
`
  const violations = scanEntries([{ file: 'InspectorTool.fs', text: inspectSpec }], {
    staticTools: STATIC_TOOLS_SNIPPET,
    toolRegistry: REGISTRY_SNIPPET,
  })
  assert.ok(violations.some((v) => v.code === 'unknown-tool-not-in-static' && v.detail?.includes('inspect')))
})

test('gate_a_extract_known_tool_names', () => {
  assert.deepEqual(extractKnownToolNames(STATIC_TOOLS_SNIPPET), ['fork', 'commission', 'join', 'horizon'])
})

test('gate_a_repo_scan_is_green', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})
