import assert from 'node:assert/strict'
import test from 'node:test'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { generateRole } from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'
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

test('WHAT[ENF-012] registered_js_tools_reject_cross_role_execution', () => {
  for (const caller of allRoleLabels) {
    for (const target of allRoleLabels) {
      if (caller === target) continue
      const toolName = `js-${target}`
      assert.equal(
        rolePredicate(toolName, caller),
        false,
        `ToolRegistry must deny cross-role execution: ${caller} calling ${toolName}`,
      )
    }
  }
})
