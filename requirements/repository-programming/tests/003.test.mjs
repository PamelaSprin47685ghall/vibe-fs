import assert from 'node:assert/strict'
import test from 'node:test'
import {
  generate,
  isGeneratedToolName,
  memberBinding,
} from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { permissions as rolePermissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

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

test('WHAT[REPOSITORY-PROGRAMMING-003] JS002_generation_is_deterministic_and_names_js_role', () => {
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep, ToolPermission.Edit, ToolPermission.Write)
  const a = generate('Engineer', perms, 'en')
  const b = generate('Engineer', perms, 'en')
  assert.equal(isSome(a) && isSome(b), true)
  assert.equal(a.toolName, 'js-engineer')
  assert.equal(a.description, b.description)
  assert.equal(a.baseClassSource, b.baseClassSource)
  assert.deepEqual(a.examples, b.examples)
  assert.equal(a.capabilities.length, 5)
})

test('WHAT[REPOSITORY-PROGRAMMING-003] JS002_same_capabilities_share_mechanics_but_role_shapes_the_ultra_example', () => {
  const shared = caps(ToolPermission.Read, ToolPermission.Edit, ToolPermission.Glob, ToolPermission.Grep)
  const engineer = generate('Engineer', shared, 'en')
  const devops = generate('DevOps', shared, 'en')
  assert.equal(engineer.baseClassSource, devops.baseClassSource)
  assert.deepEqual(memberNames(engineer), memberNames(devops))
  assert.notEqual(engineer.description, devops.description)
  assert.match(engineer.description, /oldApi → newApi/)
  assert.match(devops.description, /candidateTests/)
})

test('WHAT[REPOSITORY-PROGRAMMING-003] JS004_fast_deep_profiles_generate_identical_surfaces', () => {
  // Tier never reaches the generator: capability is role-only (AGENT-001).
  // The same capability set from a deep Coder yields byte-identical output.
  const fast = generate('Engineer', caps(ToolPermission.Read, ToolPermission.Glob), 'en')
  const deep = generate('Engineer', caps(ToolPermission.Read, ToolPermission.Glob), 'en')
  assert.equal(fast.baseClassSource, deep.baseClassSource)
  assert.equal(fast.description, deep.description)
})

test('WHAT[REPOSITORY-PROGRAMMING-003] JS010_each_filesystem_role_gets_exactly_one_distinct_ultra_example', () => {
  const markers = {
    Engineer: /oldApi → newApi/,
    DevOps: /candidateTests/,
  }

  for (const [role, marker] of Object.entries(markers)) {
    const result = surface(role, rolePermissions(role.toLowerCase()))
    const examples = result.examples
    assert.equal(examples.length, 1, `${role} gets exactly one Ultra Example`)
    assert.match(examples[0], marker, `${role} gets its responsibility-shaped lesson`)
  }
})
