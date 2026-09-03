/**
 * G9 capability-isomorphism static ratchet unit tests (no dist).
 */
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  FORBIDDEN_ROLE_TOKENS,
  REQUIRED_FRAGMENT_CAPS,
  REQUIRED_SURFACE_TEST_TOKENS,
  scanJsFragmentRegistry,
  scanJsSurfaceTest,
  scanRepo,
  scanRoles,
  scanTexts,
  scanToolRegistry,
} from '../../../scripts/checks/capability-isomorphism-gate.mjs'

const GOOD_TOOL_REGISTRY = `
module ToolRegistry
let build factory workspaceDirectory =
    [ for role in RoleDefinitions.all do
          match JsToolGenerator.generate (string role) (Roles.permissions role) with
          | Some surface -> yield JsToolSpec.create factory surface "" None
          | None -> () ]
`

const GOOD_JS_TOOLS = `
module JsFragmentRegistry =
    let read: JsCapabilityFragment = Unchecked.defaultof<_>
    let glob: JsCapabilityFragment = Unchecked.defaultof<_>
    let grep: JsCapabilityFragment = Unchecked.defaultof<_>
    let edit: JsCapabilityFragment = Unchecked.defaultof<_>
    let rewrite: JsCapabilityFragment = Unchecked.defaultof<_>
    let write: JsCapabilityFragment = Unchecked.defaultof<_>
    let all: JsCapabilityFragment list = [ read; glob; grep; edit; rewrite; write ]
`

const GOOD_SURFACE_TEST = `
const layersOf = (s) => ({})
test('JS004_four_layer_exactness', () => {
  assert.equal(memberBinding('Inspector', perms, 'file'), 'js.read')
})
`

const GOOD_ROLES = `
module Roles
type Role =
    | Coder
    | Inspector
    | Reviewer
`

test('WHAT[ENF-008] capability_iso_documents_required_tokens', () => {
  assert.deepEqual([...REQUIRED_FRAGMENT_CAPS], ['read', 'glob', 'grep', 'edit', 'rewrite', 'write'])
  assert.deepEqual([...REQUIRED_SURFACE_TEST_TOKENS], ['JS004', 'layersOf', 'memberBinding'])
  assert.deepEqual([...FORBIDDEN_ROLE_TOKENS], ['Student', 'Teacher'])
})

test('WHAT[ENF-012] capability_iso_tool_registry_requires_generator', () => {
  const ok = scanToolRegistry(GOOD_TOOL_REGISTRY)
  assert.equal(ok.length, 0)

  const missing = scanToolRegistry('module ToolRegistry\nlet x = 1\n')
  assert.ok(missing.some((v) => v.code === 'missing-js-tool-generator'))

  const handwritten = scanToolRegistry(
    'JsToolGenerator.generate\nlet t = ToolSpec.create factory "js-coder"\n',
  )
  assert.ok(handwritten.some((v) => v.code === 'handwritten-js-tool-spec'))
})

test('WHAT[ENF-008] capability_iso_js_fragment_registry_requires_member_caps', () => {
  assert.equal(scanJsFragmentRegistry(GOOD_JS_TOOLS).length, 0)

  const noModule = scanJsFragmentRegistry('module Other\nlet read: int = 1\n')
  assert.ok(noModule.some((v) => v.code === 'missing-js-fragment-registry'))

  const missingWrite = scanJsFragmentRegistry(`
module JsFragmentRegistry =
    let read: JsCapabilityFragment = Unchecked.defaultof<_>
    let glob: JsCapabilityFragment = Unchecked.defaultof<_>
    let rewrite: JsCapabilityFragment = Unchecked.defaultof<_>
    let all: JsCapabilityFragment list = [ read; glob; rewrite ]
`)
  assert.ok(missingWrite.some((v) => v.code === 'missing-fragment-cap'))
  assert.ok(missingWrite.some((v) => v.detail?.includes('write')))
  assert.ok(missingWrite.some((v) => v.code === 'fragment-all-incomplete'))
})

test('WHAT[ENF-008] capability_iso_js_surface_test_requires_layer_tokens', () => {
  assert.equal(scanJsSurfaceTest(GOOD_SURFACE_TEST).length, 0)

  const bare = scanJsSurfaceTest('test("something", () => {})\n')
  const codes = bare.map((v) => v.code)
  assert.equal(bare.length, 3)
  assert.ok(codes.every((c) => c === 'missing-surface-token'))
  for (const token of REQUIRED_SURFACE_TEST_TOKENS) {
    assert.ok(bare.some((v) => v.detail?.includes(token)), token)
  }
})

test('WHAT[ENF-008] capability_iso_roles_forbids_student_teacher', () => {
  assert.equal(scanRoles(GOOD_ROLES).length, 0)

  const cases = scanRoles('type Role =\n    | Student\n    | Teacher\n')
  assert.ok(cases.some((v) => v.code === 'forbidden-role' && v.detail?.includes('Student')))
  assert.ok(cases.some((v) => v.code === 'forbidden-role' && v.detail?.includes('Teacher')))

  const dotted = scanRoles('let r = Role.Student\n')
  assert.ok(dotted.some((v) => v.code === 'forbidden-role'))
})

test('WHAT[ENF-008] capability_iso_scan_texts_aggregates', () => {
  const green = scanTexts({
    toolRegistry: GOOD_TOOL_REGISTRY,
    jsTools: GOOD_JS_TOOLS,
    jsSurfaceTest: GOOD_SURFACE_TEST,
    roles: GOOD_ROLES,
  })
  assert.equal(green.ok, true)
  assert.equal(green.violations.length, 0)

  const red = scanTexts({
    toolRegistry: 'no generator here',
    jsTools: GOOD_JS_TOOLS,
    jsSurfaceTest: GOOD_SURFACE_TEST,
    roles: GOOD_ROLES,
  })
  assert.equal(red.ok, false)
  assert.ok(red.violations.some((v) => v.code === 'missing-js-tool-generator'))
})

test('WHAT[ENF-008] capability_iso_repo_scan_is_green', () => {
  const result = scanRepo()
  assert.equal(result.ok, true, JSON.stringify(result.violations, null, 2))
  assert.equal(result.violations.length, 0)
})
