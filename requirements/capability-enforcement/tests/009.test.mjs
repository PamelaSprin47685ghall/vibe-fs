// ENF-009: Tool name referential integrity
import assert from 'node:assert/strict'
import test from 'node:test'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import {
  LEGACY_FORBIDDEN_NAMES,
  extractKnownToolNames,
  scanEntries,
  scanRepo,
} from '../../../scripts/checks/tool-referential-integrity.mjs'

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

test('WHAT[ENF-009] gate_a_documents_legacy_forbidden_names', () => {
  assert.ok(LEGACY_FORBIDDEN_NAMES.includes('verdict'))
  assert.ok(LEGACY_FORBIDDEN_NAMES.includes('list'))
})

test('WHAT[ENF-009] gate_a_legacy_tool_name_is_red', () => {
  const violations = scanEntries([{ file: 'VerdictTool.fs', text: LEGACY_VERDICT }])
  assert.ok(violations.some((v) => v.code === 'legacy-tool-name' && v.detail?.includes('verdict')))
})

test('WHAT[ENF-009] gate_a_unknown_tool_not_in_static_is_red', () => {
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

test('WHAT[ENF-009] gate_a_extract_known_tool_names', () => {
  assert.deepEqual(extractKnownToolNames(STATIC_TOOLS_SNIPPET), ['fork', 'commission', 'join', 'horizon'])
})

test('WHAT[ENF-009] gate_a_repo_scan_is_green', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
})

test('WHAT[ENF-009] TOOLSPEC_review_and_finality_tools_have_owner_defined_admission', () => {
  assert.equal(rolePredicate('review', 'manager'), true)
  assert.equal(rolePredicate('review', 'coder'), false)

  assert.equal(rolePredicate('suicide', 'manager'), true)
  assert.equal(rolePredicate('suicide', 'coder'), false)
})

test('WHAT[ENF-009] TOOLSPEC_unknown_tools_and_host_natives_fail_closed', () => {
  assert.equal(rolePredicate('nonexistent-tool', 'coder'), false)
  assert.equal(rolePredicate('read', 'coder'), false)
  assert.equal(rolePredicate('write', 'coder'), false)
  assert.equal(rolePredicate('skill', 'coder'), false)
})
