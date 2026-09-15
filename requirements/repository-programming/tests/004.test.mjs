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

test('WHAT[REPOSITORY-PROGRAMMING-004] JS001_generated_name_gate_rejects_forged_names', () => {
  const perms = caps(ToolPermission.Read, ToolPermission.Glob, ToolPermission.Grep)
  assert.equal(isGeneratedToolName('Inspector', perms, 'js-inspector'), true)
  assert.equal(isGeneratedToolName('Inspector', perms, 'js-coder'), false)
  assert.equal(isGeneratedToolName('Inspector', perms, 'read'), false)
  // a role without the capability set never admits its own name
  assert.equal(isGeneratedToolName('Coder', caps(ToolPermission.Fork), 'js-coder'), false)
})
