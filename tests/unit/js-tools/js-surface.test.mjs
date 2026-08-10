// tests/unit/js-tools/js-surface.test.mjs — G5 Phase A: capability algebra,
// four-layer exactness, deterministic generation, generated-name gate.
//
// JS-001 no second permission matrix: the surface is projected from
// ToolPermission only. JS-002 deterministic. JS-004 four-layer exactness:
// capability → member → description → example → runtime binding.

import assert from 'node:assert/strict'
import test from 'node:test'

import { ToolPermission } from '../../../dist/Kernel/Roles.js'
import {
  JsToolGenerator_generate as generate,
  JsToolGenerator_isGeneratedToolName as isGeneratedToolName,
  JsToolGenerator_memberBinding as memberBinding,
} from '../../../dist/Domain/JsTools.js'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'
import { isNone, isSome, listItems, roles } from '../support/domain.mjs'

const permissionComparer = { Compare: (a, b) => a.CompareTo(b) }
const caps = (...permissions) => ofArray(permissions, permissionComparer)
// surface(role, permissionNameArray) — names only; conversion happens here so
// call sites can never drift from the ToolPermission vocabulary.
const surface = (role, permissionNames) => generate(role, caps(...permsOf(permissionNames)))
const memberNames = (s) => listItems(s.Members).map((fragment) => fragment.MemberName)

const PERMISSION_NAMES = [
  'Fork', 'Join', 'List', 'Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move',
  'Remove', 'Inspector', 'Coder', 'Exec', 'Pty', 'Network', 'Verdict', 'Blog',
  'Return', 'Finality', 'BashHoneypot',
]
const toolPermissionByName = Object.fromEntries(PERMISSION_NAMES.map((n) => [n, ToolPermission[n]]))
const permsOf = (names) => names.map((n) => toolPermissionByName[n])
const fsPermissionsOf = (role) =>
  roles.permissions(roles.of(role)).filter((n) => ['Read', 'Write', 'Edit', 'Glob', 'Grep'].includes(n))

const MEMBER_BY_PERMISSION = { Read: 'file', Glob: 'glob', Grep: 'grep', Edit: 'rewrite', Write: 'write' }
const BINDING_BY_MEMBER = { file: 'js.read', glob: 'js.glob', grep: 'js.grep', rewrite: 'js.edit', write: 'js.write' }

// The four layers a capability must light up: member, description, example,
// runtime binding, base class. A member missing from any layer is exactly a
// lying generator (JS-004).
const layersOf = (s) =>
  Object.fromEntries(
    listItems(s.Members).map((fragment) => [
      fragment.MemberName,
      {
        description: fragment.Description,
        example: fragment.CanonicalExample,
        binding: fragment.RuntimeBindingKey,
        inBaseClass: s.BaseClassSource.includes(`this._api.${fragment.RuntimeBindingKey}`),
        inDescription: s.Description.includes(fragment.MemberName),
        inExamples: listItems(s.Examples).some((example) => example.includes(fragment.MemberName)),
      },
    ]),
  )

test('JS001_generate_none_when_no_filesystem_capability', () => {
  for (const role of ['Manager', 'Orchestrator', 'Meditator', 'Executor', 'Blogger']) {
    const perms = caps(...permsOf(roles.permissions(roles.of(role))))
    assert.equal(isNone(surface(role, roles.permissions(roles.of(role)))), true, `${role} must get no js-* surface`)
    assert.equal(isGeneratedToolName(role, perms, `js-${role.toLowerCase()}`), false)
  }
})

test('JS001_role_projection_is_exactly_roles_permissions_intersection', () => {
  for (const role of ['Manager', 'Orchestrator', 'Coder', 'Inspector', 'Browser', 'Meditator', 'Reviewer', 'DevOps', 'Executor', 'Blogger']) {
    const fsPerms = fsPermissionsOf(role)
    const result = surface(role, roles.permissions(roles.of(role)))
    if (fsPerms.length === 0) {
      assert.equal(isNone(result), true, `${role} has no fs capability`)
    } else {
      assert.equal(isSome(result), true, `${role} must get a js-* surface`)
      const expected = fsPerms.map((name) => MEMBER_BY_PERMISSION[name]).sort()
      assert.deepEqual(memberNames(result), expected, `${role} member set`)
    }
  }
})

test('JS002_generation_is_deterministic_and_names_js_role', () => {
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep, ToolPermission.Edit, ToolPermission.Write)
  const a = generate('Coder', perms)
  const b = generate('Coder', perms)
  assert.equal(isSome(a) && isSome(b), true)
  assert.equal(a.ToolName, 'js-coder')
  assert.equal(a.Description, b.Description)
  assert.equal(a.BaseClassSource, b.BaseClassSource)
  assert.deepEqual(a.Examples, b.Examples)
  assert.equal(a.Capabilities.size, 5)
})

test('JS004_four_layer_exactness_coder', () => {
  const result = surface('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  assert.equal(isSome(result), true)
  const layers = layersOf(result)
  assert.deepEqual(Object.keys(layers).sort(), ['file', 'glob', 'grep', 'rewrite', 'write'])
  for (const [member, layer] of Object.entries(layers)) {
    assert.equal(layer.inBaseClass, true, `${member} in base class`)
    assert.equal(layer.inDescription, true, `${member} in description`)
    assert.equal(layer.inExamples, true, `${member} in examples`)
    assert.equal(layer.binding, BINDING_BY_MEMBER[member], `${member} binding`)
  }
})

test('JS004_absent_capability_is_absent_in_all_four_layers', () => {
  const result = surface('Inspector', ['Read', 'Glob', 'Grep']) // no Edit / Write
  assert.equal(isSome(result), true)
  assert.deepEqual(memberNames(result), ['file', 'glob', 'grep'])
  // description lists only present members; the builtin-name substring
  // 'read/edit/write/glob/grep' appears in the recommendation line, so assert
  // on the Available-methods clause instead.
  assert.equal(result.Description.includes('Available methods: file, glob, grep'), true)
  assert.equal(result.Description.includes('Available methods: file, glob, grep, rewrite, write'), false)
  assert.equal(result.BaseClassSource.includes('js.edit'), false)
  assert.equal(result.BaseClassSource.includes('js.write'), false)
  assert.equal(listItems(result.Examples).length, 3)
})

test('JS001_generated_name_gate_rejects_forged_names', () => {
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep)
  assert.equal(isGeneratedToolName('Inspector', perms, 'js-inspector'), true)
  assert.equal(isGeneratedToolName('Inspector', perms, 'js-coder'), false)
  assert.equal(isGeneratedToolName('Inspector', perms, 'read'), false)
  // a role without the capability set never admits its own name
  assert.equal(isGeneratedToolName('Coder', caps(ToolPermission.Fork), 'js-coder'), false)
})

test('JS004_member_gate_binds_present_members_only', () => {
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep)
  assert.equal(memberBinding('Inspector', perms, 'file'), 'js.read')
  assert.equal(memberBinding('Inspector', perms, 'glob'), 'js.glob')
  assert.equal(memberBinding('Inspector', perms, 'rewrite'), undefined)
  assert.equal(memberBinding('Inspector', perms, 'write'), undefined)
  assert.equal(memberBinding('Meditator', caps(ToolPermission.Inspector), 'file'), undefined)
})

test('JS002_same_capabilities_same_surface_across_roles', () => {
  // Every role surface is a pure projection: same capabilities → same members,
  // base class, and bindings regardless of role name (only tool name differs).
  const a = generate('Coder', caps(ToolPermission.Read))
  const b = generate('Reviewer', caps(ToolPermission.Read))
  assert.equal(a.BaseClassSource, b.BaseClassSource)
  assert.equal(a.Description.includes('Coder'), true)
  assert.equal(b.Description.includes('Reviewer'), true)
  assert.equal(a.ToolName, 'js-coder')
  assert.equal(b.ToolName, 'js-reviewer')
})

test('JS001_non_fs_permissions_never_produce_members', () => {
  for (const name of PERMISSION_NAMES.filter((n) => !['Read', 'Write', 'Edit', 'Glob', 'Grep'].includes(n))) {
    const result = generate('Coder', caps(toolPermissionByName[name]))
    assert.equal(isNone(result), true, `${name} alone must not generate a surface`)
  }
})

test('JS004_fast_deep_profiles_generate_identical_surfaces', () => {
  // Tier never reaches the generator: capability is role-only (AGENT-001).
  // The same capability set from a deep Coder yields byte-identical output.
  const fast = generate('Coder', caps(ToolPermission.Read, ToolPermission.Glob))
  const deep = generate('Coder', caps(ToolPermission.Read, ToolPermission.Glob))
  assert.equal(fast.BaseClassSource, deep.BaseClassSource)
  assert.equal(fast.Description, deep.Description)
})
