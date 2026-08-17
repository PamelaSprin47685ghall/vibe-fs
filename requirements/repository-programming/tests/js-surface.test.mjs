// tests/unit/js-tools/js-surface.test.mjs — G5 Phase A: capability algebra,
// four-layer exactness, deterministic generation, generated-name gate.
//
// JS-001 no second permission matrix: the surface is projected from
// ToolPermission only. JS-002 deterministic. JS-004 four-layer exactness:
// capability → member → description → example → runtime binding.

import assert from 'node:assert/strict'
import test from 'node:test'

import {
  generate,
  isGeneratedToolName,
  memberBinding,
} from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { permissions as rolePermissions } from '../../../dist/Foundation/RolesSurface.js'

const caps = (...permissions) => permissions
const surface = (role, permissionNames) => generate(role, caps(...permsOf(permissionNames)), 'en')
const memberNames = (s) => s.members.map((fragment) => fragment.memberName)
const isNone = (value) => value === null
const isSome = (value) => value !== null

const PERMISSION_NAMES = [
  'Fork', 'Join', 'Horizon', 'TodoWrite', 'Fission', 'Read', 'Write', 'Edit', 'Fetch', 'Glob', 'Grep', 'Move',
  'Remove', 'Inspect', 'Sphinx', 'Behavior', 'Exec', 'Pty', 'Network', 'Judge', 'Chronicle',
  'Finality', 'BashHoneypot',
]
const toolPermissionByName = Object.fromEntries(PERMISSION_NAMES.map((n) => [n, n]))
const ToolPermission = toolPermissionByName
const permsOf = (names) => names.map((n) => toolPermissionByName[n])
const fsPermissionsOf = (role) =>
  rolePermissions(role.toLowerCase()).filter((n) => ['Read', 'Write', 'Edit', 'Glob', 'Grep'].includes(n))

const MEMBER_BY_PERMISSION = { Read: 'file', Glob: 'glob', Grep: 'grep', Edit: 'rewrite', Write: 'write' }
const BINDING_BY_MEMBER = { file: 'js.read', glob: 'js.glob', grep: 'js.grep', rewrite: 'js.edit', write: 'js.write' }

// Capability exactness is structural: member, generated API description,
// runtime binding, base class. GrandRewrite §6.10 intentionally decouples the
// one responsibility-shaped Ultra Example from per-member syntax coverage.
const layersOf = (s) =>
  Object.fromEntries(
    s.members.map((fragment) => [
      fragment.memberName,
      {
        description: fragment.description,
        example: fragment.canonicalExample,
        binding: fragment.runtimeBindingKey,
        inBaseClass: s.baseClassSource.includes(`this._api.${fragment.runtimeBindingKey}`),
        inDescription: s.description.includes(fragment.memberName),
        inExamples: s.examples.some((example) => example.includes(fragment.memberName)),
      },
    ]),
  )

test('WHAT[REPOSITORY-PROGRAMMING-001] JS001_generate_none_when_no_filesystem_capability', () => {
  for (const role of ['Manager', 'Orchestrator', 'Inquiry', 'Distiller', 'Blogger']) {
    const perms = caps(...permsOf(rolePermissions(role.toLowerCase())))
    assert.equal(isNone(surface(role, rolePermissions(role.toLowerCase()))), true, `${role} must get no js-* surface`)
    assert.equal(isGeneratedToolName(role, perms, `js-${role.toLowerCase()}`), false)
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-001] JS001_role_projection_is_exactly_roles_permissions_intersection', () => {
  for (const role of ['Manager', 'Orchestrator', 'Coder', 'Inspector', 'Browser', 'Inquiry', 'Reviewer', 'DevOps', 'Distiller', 'Blogger']) {
    const fsPerms = fsPermissionsOf(role)
    const result = surface(role, rolePermissions(role.toLowerCase()))
    if (fsPerms.length === 0) {
      assert.equal(isNone(result), true, `${role} has no fs capability`)
    } else {
      assert.equal(isSome(result), true, `${role} must get a js-* surface`)
      const expected = fsPerms.map((name) => MEMBER_BY_PERMISSION[name]).sort()
      assert.deepEqual(memberNames(result), expected, `${role} member set`)
    }
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-003] JS002_generation_is_deterministic_and_names_js_role', () => {
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep, ToolPermission.Edit, ToolPermission.Write)
  const a = generate('Coder', perms, 'en')
  const b = generate('Coder', perms, 'en')
  assert.equal(isSome(a) && isSome(b), true)
  assert.equal(a.toolName, 'js-coder')
  assert.equal(a.description, b.description)
  assert.equal(a.baseClassSource, b.baseClassSource)
  assert.deepEqual(a.examples, b.examples)
  assert.equal(a.capabilities.length, 5)
})

test('WHAT[REPOSITORY-PROGRAMMING-002] JS004_capability_exactness_plus_one_ultra_example_coder', () => {
  const result = surface('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  assert.equal(isSome(result), true)
  const layers = layersOf(result)
  assert.deepEqual(Object.keys(layers).sort(), ['file', 'glob', 'grep', 'rewrite', 'write'])
  for (const [member, layer] of Object.entries(layers)) {
    assert.equal(layer.inBaseClass, true, `${member} in base class`)
    assert.equal(layer.inDescription, true, `${member} in description`)
    assert.equal(layer.binding, BINDING_BY_MEMBER[member], `${member} binding`)
  }
  assert.equal(result.description.includes('HOST_READ_IMMUTABLE_UTF8_SNAPSHOT'), true)
  assert.match(result.description, /name\+N \/ name-N/)
  assert.match(result.description, /not a line number/)
  assert.match(result.description, /text\(from = "\^", to = "\$"\)/)
  assert.equal(result.description.includes('_api'), false)
  assert.equal(result.description.includes('__jsFailure'), false)
  assert.equal(result.examples.length, 1, 'one responsibility-shaped Ultra Example')
  assert.match(result.examples[0], /oldApi → newApi/)
})

test('WHAT[REPOSITORY-PROGRAMMING-002] JS004_absent_capability_is_absent_in_all_four_layers', () => {
  const result = surface('Inspector', ['Read', 'Glob', 'Grep']) // no Edit / Write
  assert.equal(isSome(result), true)
  assert.deepEqual(memberNames(result), ['file', 'glob', 'grep'])
  assert.equal(result.description.includes('rewrite(path'), false)
  assert.equal(result.description.includes('write(path'), false)
  assert.equal(result.baseClassSource.includes('js.edit'), false)
  assert.equal(result.baseClassSource.includes('js.write'), false)
  assert.equal(result.examples.some((example) => example.includes('this.rewrite')), false)
})

test('WHAT[REPOSITORY-PROGRAMMING-004] JS001_generated_name_gate_rejects_forged_names', () => {
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep)
  assert.equal(isGeneratedToolName('Inspector', perms, 'js-inspector'), true)
  assert.equal(isGeneratedToolName('Inspector', perms, 'js-coder'), false)
  assert.equal(isGeneratedToolName('Inspector', perms, 'read'), false)
  // a role without the capability set never admits its own name
  assert.equal(isGeneratedToolName('Coder', caps(ToolPermission.Fork), 'js-coder'), false)
})

test('WHAT[REPOSITORY-PROGRAMMING-002] JS004_member_gate_binds_present_members_only', () => {
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep)
  assert.equal(memberBinding('Inspector', perms, 'file'), 'js.read')
  assert.equal(memberBinding('Inspector', perms, 'glob'), 'js.glob')
  assert.equal(memberBinding('Inspector', perms, 'grep'), 'js.grep')
  assert.equal(memberBinding('Inspector', perms, 'rewrite'), undefined)
  assert.equal(memberBinding('Inspector', perms, 'write'), undefined)
  assert.equal(memberBinding('Inquiry', caps(ToolPermission.Inspect), 'file'), undefined)
})

test('WHAT[REPOSITORY-PROGRAMMING-003] JS002_same_capabilities_share_mechanics_but_role_shapes_the_ultra_example', () => {
  const shared = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep)
  const inspector = generate('Inspector', shared, 'en')
  const reviewer = generate('Reviewer', shared, 'en')
  assert.equal(inspector.baseClassSource, reviewer.baseClassSource)
  assert.deepEqual(memberNames(inspector), memberNames(reviewer))
  assert.notEqual(inspector.description, reviewer.description)
  assert.match(inspector.description, /RetryPolicy/)
  assert.match(reviewer.description, /staleReferences/)
})

test('WHAT[REPOSITORY-PROGRAMMING-001] JS001_non_fs_permissions_never_produce_members', () => {
  for (const name of PERMISSION_NAMES.filter((n) => !['Read', 'Write', 'Edit', 'Glob', 'Grep'].includes(n))) {
    const result = generate('Coder', caps(toolPermissionByName[name]), 'en')
    assert.equal(isNone(result), true, `${name} alone must not generate a surface`)
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-003] JS004_fast_deep_profiles_generate_identical_surfaces', () => {
  // Tier never reaches the generator: capability is role-only (AGENT-001).
  // The same capability set from a deep Coder yields byte-identical output.
  const fast = generate('Coder', caps(ToolPermission.Read, ToolPermission.Glob), 'en')
  const deep = generate('Coder', caps(ToolPermission.Read, ToolPermission.Glob), 'en')
  assert.equal(fast.baseClassSource, deep.baseClassSource)
  assert.equal(fast.description, deep.description)
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS002_description_embeds_spec_base_class_rules_and_one_ultra_example', () => {
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
    assert.equal(coder.description.includes(token), true, `coder description missing: ${token}`)
  }
  assert.equal(coder.description.includes('_api'), false)
  assert.equal(coder.description.includes('js.read'), false)
  const inspector = surface('Inspector', ['Read', 'Glob', 'Grep'])
  assert.equal(inspector.description.includes('HOST_READ_IMMUTABLE_UTF8_SNAPSHOT'), true)
  assert.equal(inspector.description.includes('RetryPolicy'), true)
  assert.equal(inspector.description.includes('this.rewrite'), false)
})

test('WHAT[REPOSITORY-PROGRAMMING-003] JS010_each_filesystem_role_gets_exactly_one_distinct_ultra_example', () => {
  const markers = {
    Coder: /oldApi → newApi/,
    Inspector: /RetryPolicy/,
    Reviewer: /staleReferences/,
    DevOps: /candidateTests/,
    Browser: /WidgetOptions/,
  }

  for (const [role, marker] of Object.entries(markers)) {
    const result = surface(role, rolePermissions(role.toLowerCase()))
    const examples = result.examples
    assert.equal(examples.length, 1, `${role} gets exactly one Ultra Example`)
    assert.match(examples[0], marker, `${role} gets its responsibility-shaped lesson`)
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS010_description_never_dilutes_the_ultra_example', () => {
  for (const role of ['Coder', 'Inspector', 'Reviewer', 'DevOps', 'Browser']) {
    const result = surface(role, rolePermissions(role.toLowerCase()))
    const classes = result.description.match(/class Js extends JsProgram/g) ?? []
    assert.equal(classes.length, 1, `${role} description must not dilute the Ultra Example with toy examples`)
    assert.match(result.description, /Semantic branches belong between programs/)
  }

  const reviewer = surface('Reviewer', rolePermissions('reviewer'))
  assert.doesNotMatch(reviewer.examples[0], /verdict\s*:/i, 'reviewer example gathers evidence, never authors judgment')
})

test('WHAT[REPOSITORY-PROGRAMMING-002] JS004_lying_generator_counterexample_is_rejected', () => {
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
  const result = generate('Inspector', perms, 'en')
  const names = result.members.map((f) => f.memberName)
  for (const name of names) {
    assert.notEqual(memberBinding('Inspector', perms, name), undefined, `${name} must resolve`)
  }
  assert.equal(names.includes('rewrite'), false)
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS_description_retains_no_unsubstituted_placeholders', () => {
  const result = generate('Coder', caps(ToolPermission.Read, ToolPermission.Edit, ToolPermission.Write), 'en')
  assert.equal(result.description.includes('{{'), false)
})

test('WHAT[REPOSITORY-PROGRAMMING-022] JS_description_teaches_tool_choice_through_paid_failure_memory', () => {
  const coder = surface('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  const lessonText = coder.description.replace(/\s+/g, ' ')

  assert.equal(
    lessonText.startsWith('WARNING: You may be about to turn a bounded filesystem task into a self-inflicted repair job.'),
    true,
    'the first screen must interrupt autopilot before the API manual begins',
  )

  for (const lesson of [
    'The Host already owns this boundary',
    'There are only two acceptable moves',
    'Suspicion is enough to trigger verification',
    'only evidence earns permission to continue',
    'use the primitive that owns it',
    'prove it cannot express the job before dropping lower',
    'Precommitment matters',
    'does not get to renegotiate those rules after seeing an inconvenient result',
    'Numbers, required sentinels, and section counts outrank the story',
    'Do not let familiarity impersonate evidence',
    'The lower-level technique carries the burden of proof',
    'evidence beats confidence',
    'Do not invent a third category called "probably fine"',
    'I have already paid for this mistake',
    'roughly 8k lines',
    'roughly 31k lines',
    'grep was finding candidates, not owning structure',
    'ordered anchors',
    'second and third cleanup programs',
    'cheap invariants',
    'throw before return',
    'zero committed mutations',
    'If you are about to calculate structural boundaries by hand',
    'STOP SIGNAL',
    'Do not confuse "I can reimplement this" with "I should reimplement this"',
  ]) {
    assert.equal(lessonText.includes(lesson), true, `paid-failure lesson missing: ${lesson}`)
  }

  const zh = generate(
    'Coder',
    caps(...permsOf(['Read', 'Write', 'Edit', 'Glob', 'Grep'])),
    'zh-CN',
  ).description.replace(/\s+/g, ' ')

  assert.equal(
    zh.startsWith('警告：你正准备把一个本来有边界保护的文件任务，亲手变成一场返工事故。'),
    true,
    '中文第一屏必须先惊醒，再解释',
  )

  for (const lesson of [
    'Host 已经拥有这层边界',
    '只有两个合格动作',
    '只要起疑，就触发验证',
    '只有证据才能换来继续执行的资格',
    '使用已经拥有它的 primitive',
    '先证明它表达不了任务，再往下降一层',
    '先承诺，再动手',
    '就没有资格临时改口',
    '优先于你在坏结果出现后给自己编的解释',
    '别让熟悉感冒充证据',
    '举证责任在你',
    '证据 > 自信',
    '不要再发明第三类叫「大概没问题」',
    '这笔学费我已经交过一次',
    '约 8k 行',
    '约 31k 行',
    '给前一个 program 擦屁股',
    '如果你正准备手算结构边界',
    '停止信号',
    '别把「我也能自己重写一遍」误当成「我应该自己重写一遍」',
  ]) {
    assert.equal(zh.includes(lesson), true, `中文现身说法缺失: ${lesson}`)
  }
})
