import assert from 'node:assert/strict'
import test from 'node:test'
import {
  generate,
  isGeneratedToolName,
  memberBinding,
} from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { permissions as rolePermissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'

const caps = (...permissions) => permissions

const surface = (role, permissionNames) => generate(role, caps(...permissionNames), 'en')

const memberNames = (s) => s.members.map((fragment) => fragment.memberName)

const isNone = (value) => value === null

const isSome = (value) => value !== null

const fsPermissionsOf = (role) =>
  rolePermissions(role.toLowerCase()).filter((n) => ['Read', 'Write', 'Glob', 'Move', 'Remove'].includes(n))

const MEMBERS_BY_PERMISSION = {
  Read: ['cat', 'stat', 'whereAll', 'where'],
  Glob: ['ls'],
  Grep: ['rg'],
  Write: ['write', 'mkdir'],
  Move: ['mv'],
  Remove: ['rm'],
}

const BINDING_BY_MEMBER = {
  write: 'write',
  rm: 'rm',
  mv: 'mv',
  cat: 'cat',
  ls: 'ls',
  rg: 'rg',
  stat: 'stat',
  mkdir: 'mkdir',
  whereAll: 'whereAll',
  where: 'where',
}

const MEMBER_ORDER = ['write', 'rm', 'mv', 'cat', 'ls', 'rg', 'stat', 'mkdir', 'whereAll', 'where']

const layersOf = (s) =>
  Object.fromEntries(
    s.members.map((fragment) => [
      fragment.memberName,
      {
        binding: fragment.memberName,
        inBaseClass: s.baseClassSource.includes(`this._api.${fragment.memberName}`),
        inDescription: s.description.includes(fragment.memberName),
        inExamples: s.examples.some((example) => example.includes(fragment.memberName)),
      },
    ]),
  )

test('WHAT[repository-programming-004] JS001_generated_name_gate_rejects_forged_names', () => {
  const perms = caps('Read', 'Glob', 'Grep')
  assert.equal(isGeneratedToolName('Engineer', perms, 'todowrite-for-engineer'), true)
  assert.equal(isGeneratedToolName('Engineer', perms, 'todowrite-for-devops'), false)
  assert.equal(isGeneratedToolName('Engineer', perms, 'read'), false)

  // A permission that projects no member at all generates no surface, so the
  // role never admits its own name. Edit is such a permission: it is a real
  // permission that resolves, but the js-* projection of it is empty.
  assert.equal(isGeneratedToolName('Engineer', caps('Edit'), 'todowrite-for-engineer'), false)

  // The control: Ask is a real permission that does project a member, so the
  // role does admit its own name. Without this the negative probe above could
  // pass for the wrong reason.
  assert.equal(isGeneratedToolName('Engineer', caps('Ask'), 'todowrite-for-engineer'), true)
})
