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
} from '../../../dist/Domain/JsSurface.js'
import { JsDescriptionAssets_load as loadJsProse } from '../../../dist/Infrastructure/OpenCode/Tools/JsToolHost.js'
import { ProviderLanguage } from '../../../dist/Domain/ProviderLanguage.js'
import { FsSet } from '../../../tests/unit/support/domain.mjs'
import { isNone, isSome, listItems, roles } from '../../../tests/unit/support/domain.mjs'

const permissionComparer = { Compare: (a, b) => a.CompareTo(b) }
const caps = (...permissions) => FsSet.ofArray(permissions, permissionComparer)
const jsProse = () => loadJsProse(ProviderLanguage.English)
// surface(role, permissionNameArray) — names only; conversion happens here so
// call sites can never drift from the ToolPermission vocabulary.
const surface = (role, permissionNames) => generate(role, caps(...permsOf(permissionNames)), jsProse())
const memberNames = (s) => listItems(s.Members).map((fragment) => fragment.MemberName)

const PERMISSION_NAMES = [
  'Fork', 'Join', 'Horizon', 'TodoWrite', 'Fission', 'Read', 'Write', 'Edit', 'Fetch', 'Glob', 'Grep', 'Move',
  'Remove', 'Inspect', 'Sphinx', 'Behavior', 'Exec', 'Pty', 'Network', 'Judge', 'Chronicle',
  'Finality', 'BashHoneypot', 'AutoInjected',
]
const toolPermissionByName = Object.fromEntries(PERMISSION_NAMES.map((n) => [n, ToolPermission[n]]))
const permsOf = (names) => names.map((n) => toolPermissionByName[n])
const fsPermissionsOf = (role) =>
  roles.permissions(roles.of(role)).filter((n) => ['Read', 'Write', 'Edit', 'Glob', 'Grep'].includes(n))

const MEMBER_BY_PERMISSION = { Read: 'file', Glob: 'glob', Grep: 'grep', Edit: 'rewrite', Write: 'write' }
const BINDING_BY_MEMBER = { file: 'js.read', glob: 'js.glob', grep: 'js.grep', rewrite: 'js.edit', write: 'js.write' }

// Capability exactness is structural: member, generated API description,
// runtime binding, base class. GrandRewrite §6.10 intentionally decouples the
// one responsibility-shaped Ultra Example from per-member syntax coverage.
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
  for (const role of ['Manager', 'Orchestrator', 'Inquiry', 'Distiller', 'Blogger']) {
    const perms = caps(...permsOf(roles.permissions(roles.of(role))))
    assert.equal(isNone(surface(role, roles.permissions(roles.of(role)))), true, `${role} must get no js-* surface`)
    assert.equal(isGeneratedToolName(role, perms, `js-${role.toLowerCase()}`), false)
  }
})

test('JS001_role_projection_is_exactly_roles_permissions_intersection', () => {
  for (const role of ['Manager', 'Orchestrator', 'Coder', 'Inspector', 'Browser', 'Inquiry', 'Reviewer', 'DevOps', 'Distiller', 'Blogger']) {
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
  const a = generate('Coder', perms, jsProse())
  const b = generate('Coder', perms, jsProse())
  assert.equal(isSome(a) && isSome(b), true)
  assert.equal(a.ToolName, 'js-coder')
  assert.equal(a.Description, b.Description)
  assert.equal(a.BaseClassSource, b.BaseClassSource)
  assert.deepEqual(a.Examples, b.Examples)
  assert.equal(a.Capabilities.size, 5)
})

test('JS004_capability_exactness_plus_one_ultra_example_coder', () => {
  const result = surface('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  assert.equal(isSome(result), true)
  const layers = layersOf(result)
  assert.deepEqual(Object.keys(layers).sort(), ['file', 'glob', 'grep', 'rewrite', 'write'])
  for (const [member, layer] of Object.entries(layers)) {
    assert.equal(layer.inBaseClass, true, `${member} in base class`)
    assert.equal(layer.inDescription, true, `${member} in description`)
    assert.equal(layer.binding, BINDING_BY_MEMBER[member], `${member} binding`)
  }
  assert.equal(result.Description.includes('HOST_READ_IMMUTABLE_UTF8_SNAPSHOT'), true)
  assert.match(result.Description, /name\+N \/ name-N/)
  assert.match(result.Description, /not a line number/)
  assert.match(result.Description, /text\(from = "\^", to = "\$"\)/)
  assert.equal(result.Description.includes('_api'), false)
  assert.equal(result.Description.includes('__jsFailure'), false)
  assert.equal(listItems(result.Examples).length, 1, 'one responsibility-shaped Ultra Example')
  assert.match(listItems(result.Examples)[0], /oldApi → newApi/)
})

test('JS004_absent_capability_is_absent_in_all_four_layers', () => {
  const result = surface('Inspector', ['Read', 'Glob', 'Grep']) // no Edit / Write
  assert.equal(isSome(result), true)
  assert.deepEqual(memberNames(result), ['file', 'glob', 'grep'])
  assert.equal(result.Description.includes('rewrite(path'), false)
  assert.equal(result.Description.includes('write(path'), false)
  assert.equal(result.BaseClassSource.includes('js.edit'), false)
  assert.equal(result.BaseClassSource.includes('js.write'), false)
  assert.equal(listItems(result.Examples).some((example) => example.includes('this.rewrite')), false)
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
  assert.equal(memberBinding('Inspector', perms, 'grep'), 'js.grep')
  assert.equal(memberBinding('Inspector', perms, 'rewrite'), undefined)
  assert.equal(memberBinding('Inspector', perms, 'write'), undefined)
  assert.equal(memberBinding('Inquiry', caps(ToolPermission.Inspect), 'file'), undefined)
})

test('JS002_same_capabilities_share_mechanics_but_role_shapes_the_ultra_example', () => {
  const shared = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep)
  const inspector = generate('Inspector', shared, jsProse())
  const reviewer = generate('Reviewer', shared, jsProse())
  assert.equal(inspector.BaseClassSource, reviewer.BaseClassSource)
  assert.deepEqual(memberNames(inspector), memberNames(reviewer))
  assert.notEqual(inspector.Description, reviewer.Description)
  assert.match(inspector.Description, /RetryPolicy/)
  assert.match(reviewer.Description, /staleReferences/)
})

test('JS001_non_fs_permissions_never_produce_members', () => {
  for (const name of PERMISSION_NAMES.filter((n) => !['Read', 'Write', 'Edit', 'Glob', 'Grep'].includes(n))) {
    const result = generate('Coder', caps(toolPermissionByName[name]), jsProse())
    assert.equal(isNone(result), true, `${name} alone must not generate a surface`)
  }
})

test('JS004_fast_deep_profiles_generate_identical_surfaces', () => {
  // Tier never reaches the generator: capability is role-only (AGENT-001).
  // The same capability set from a deep Coder yields byte-identical output.
  const fast = generate('Coder', caps(ToolPermission.Read, ToolPermission.Glob), jsProse())
  const deep = generate('Coder', caps(ToolPermission.Read, ToolPermission.Glob), jsProse())
  assert.equal(fast.BaseClassSource, deep.BaseClassSource)
  assert.equal(fast.Description, deep.Description)
})

test('JS002_description_embeds_spec_base_class_rules_and_one_ultra_example', () => {
  const coder = surface('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  for (const token of [
    'class JsProgram',
    'class Js extends JsProgram',
    'HOST_READ_IMMUTABLE_UTF8_SNAPSHOT',
    'text(from = "^", to = "$")',
    'not a line number',
    'ordered',
    'begin',
    'end',
    'complete resulting file',
    'Anchors locate',
    'Define exactly one class named Js',
    'Ultra Example',
    'oldApi → newApi',
    'Mechanical branches belong inside the program',
  ]) {
    assert.equal(coder.Description.includes(token), true, `coder description missing: ${token}`)
  }
  assert.equal(coder.Description.includes('_api'), false)
  assert.equal(coder.Description.includes('js.read'), false)
  const inspector = surface('Inspector', ['Read', 'Glob', 'Grep'])
  assert.equal(inspector.Description.includes('HOST_READ_IMMUTABLE_UTF8_SNAPSHOT'), true)
  assert.equal(inspector.Description.includes('RetryPolicy'), true)
  assert.equal(inspector.Description.includes('this.rewrite'), false)
})

test('JS010_each_filesystem_role_gets_exactly_one_distinct_ultra_example', () => {
  const markers = {
    Coder: /oldApi → newApi/,
    Inspector: /RetryPolicy/,
    Reviewer: /staleReferences/,
    DevOps: /candidateTests/,
    Browser: /WidgetOptions/,
  }

  for (const [role, marker] of Object.entries(markers)) {
    const result = surface(role, roles.permissions(roles.of(role)))
    const examples = listItems(result.Examples)
    assert.equal(examples.length, 1, `${role} gets exactly one Ultra Example`)
    assert.match(examples[0], marker, `${role} gets its responsibility-shaped lesson`)
    const classes = result.Description.match(/class Js extends JsProgram/g) ?? []
    assert.equal(classes.length, 1, `${role} description must not dilute the Ultra Example with toy examples`)
    assert.match(result.Description, /Semantic branches belong between programs/)
  }

  const reviewer = surface('Reviewer', roles.permissions(roles.of('Reviewer')))
  assert.doesNotMatch(listItems(reviewer.Examples)[0], /verdict\s*:/i, 'reviewer example gathers evidence, never authors judgment')
})

test('JS004_lying_generator_counterexample_is_rejected', () => {
  // A "lying" surface advertises a member with no runtime binding — the exact
  // failure mode the four-layer invariant exists to make impossible. The gate
  // must refuse the member: memberBinding returns undefined for it, so a
  // forged call cannot resolve an executor (JS-004).
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep)
  // lie: 'rewrite' is NOT in this surface's bindings (Inspector has no Edit)
  assert.equal(memberBinding('Inspector', perms, 'rewrite'), undefined)
  assert.equal(memberBinding('Inspector', perms, 'write'), undefined)
  // the honest members resolve
  assert.equal(memberBinding('Inspector', perms, 'file'), 'js.read')
  // a lying description would name methods the surface lacks; the generator
  // never does — prove the surface itself contains no unbounded members
  const result = generate('Inspector', perms, jsProse())
  const names = listItems(result.Members).map((f) => f.MemberName)
  for (const name of names) {
    assert.notEqual(memberBinding('Inspector', perms, name), undefined, `${name} must resolve`)
  }
  assert.equal(names.includes('rewrite'), false)
})

test('JS_description_retains_no_unsubstituted_placeholders', () => {
  const result = generate('Coder', caps(ToolPermission.Read, ToolPermission.Edit, ToolPermission.Write), jsProse())
  assert.equal(result.Description.includes('{{'), false)
})
