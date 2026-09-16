import assert from 'node:assert/strict'
import test from 'node:test'
import { permissions as rolePermissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import {

// tests/unit/js-tools/js-surface.test.mjs — G5 Phase A: capability algebra,
// four-layer exactness, deterministic generation, generated-name gate.
//
// JS-001 no second permission matrix: the surface is projected from
// ToolPermission only. JS-002 deterministic. JS-004 four-layer exactness:
// capability → member → description → example → runtime binding.


  generate,
  isGeneratedToolName,
  memberBinding,
} from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'

const caps = (...permissions) => permissions
const surface = (role, permissionNames) => generate(role, caps(...permsOf(permissionNames)), 'en')
const memberNames = (s) => s.members.map((fragment) => fragment.memberName)
const isNone = (value) => value === null
const isSome = (value) => value !== null

const PERMISSION_NAMES = [
  'Fork', 'Join', 'Horizon', 'TodoWrite', 'Fission', 'Read', 'Write', 'Edit', 'Fetch', 'Glob', 'Grep', 'Move',
  'Remove', 'Inspect', 'Sphinx', 'Behavior', 'Exec', 'Pty', 'Network', 'ReviewAssessment', 'Chronicle',
  'Finality', 'BashHoneypot',
]
const toolPermissionByName = Object.fromEntries(PERMISSION_NAMES.map((n) => [n, n]))
const ToolPermission = toolPermissionByName
const permsOf = (names) => names.map((n) => toolPermissionByName[n])
const fsPermissionsOf = (role) =>
  rolePermissions(role.toLowerCase()).filter((n) => ['Read', 'Write', 'Edit', 'Glob', 'Grep'].includes(n))

const MEMBERS_BY_PERMISSION = {
  Read: ['file'],
  Glob: ['glob'],
  Grep: ['grep'],
  Edit: ['edit', 'rewrite'],
  Write: ['write'],
}
const BINDING_BY_MEMBER = {
  file: 'js.read',
  glob: 'js.glob',
  grep: 'js.grep',
  edit: 'js.edit',
  rewrite: 'js.edit',
  write: 'js.write',
}
const MEMBER_ORDER = ['file', 'glob', 'grep', 'edit', 'rewrite', 'write']

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

// Builtin coexistence plus the generated js-* registered Host tool. Primitive
// filesystem tools remain normal fallbacks; intent-level preference is only in
// the generated description.
import {
  annotate,
  validateRecommendation,
  builtinTools,
  createRegistered,
  name,
  description,
  execute,
} from '../../../dist/Repository/Programming/Js/OpenCode/ToolHostSurface.js'

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-host-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const toolModule = () => {
  const tool = (definition) => definition
  tool.schema = {
    string: () => ({
      type: 'string',
      describe: (description) => ({ type: 'string', description }),
    }),
  }
  return { tool }
}

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
    'edit(path, changes)',
    '{ find, put, all? }',
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
  assert.equal(inspector.description.includes('this.edit'), false)
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS010_description_never_dilutes_the_ultra_example', () => {
  for (const role of ['Coder', 'Inspector', 'DevOps', 'Browser']) {
    const result = surface(role, rolePermissions(role.toLowerCase()))
    const classes = result.description.match(/class Js extends JsProgram/g) ?? []
    assert.equal(classes.length, 1, `${role} description must not dilute the Ultra Example with toy examples`)
    assert.match(result.description, /Semantic branches belong between programs/)
  }
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS_description_retains_no_unsubstituted_placeholders', () => {
  const result = generate('Coder', caps(ToolPermission.Read, ToolPermission.Edit, ToolPermission.Write), 'en')
  assert.equal(result.description.includes('{{'), false)
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS003_builtin_fallback_descriptions_are_left_untouched', () => {
  assert.deepEqual([...builtinTools()].sort(), ['edit', 'glob', 'grep', 'patch', 'read', 'write'])
  for (const builtinName of builtinTools()) {
    const original = `${builtinName} primitive fallback`
    const visible = annotate(builtinName, original, 'js-coder')
    assert.equal(visible, original)
    assert.doesNotMatch(visible, /DEPRECATED|js-coder/i)
  }
  assert.equal(annotate('join', 'Join a session', 'js-coder'), 'Join a session')
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS003_hook_must_not_recommend_invisible_tools', () => {
  assert.equal(validateRecommendation('js-coder', ['js-coder', 'read']).ok, true)
  const denied = validateRecommendation('js-coder', ['read'])
  assert.equal(denied.ok, false)
  assert.equal(denied.error.includes('not provider-visible') || denied.error.includes('不可见'), true)
})

test('WHAT[REPOSITORY-PROGRAMMING-005] JS073_spec_carries_generated_name_and_honest_description', () => {
  const { dir, cleanup } = sandbox()
  try {
    const registered = createRegistered(toolModule(), 'Coder', 'en', dir, null)
    assert.equal(name(registered), 'js-coder')
    assert.equal(description(registered).includes('class JsProgram'), true)
    assert.equal(description(registered).includes('HOST_READ_IMMUTABLE_UTF8_SNAPSHOT'), true)
    assert.equal(description(registered).includes('_api'), false)
  } finally {
    cleanup()
  }
})
