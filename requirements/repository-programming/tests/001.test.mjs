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

// park and deliver are lifecycle members: every generated surface carries them
// in its base class, but they are not the projection of a ToolPermission, so
// they are never counted among the capability members of a role.
const LIFECYCLE_MEMBERS = ['deliver', 'park']

const projectedMembers = (s) => memberNames(s).filter((name) => !LIFECYCLE_MEMBERS.includes(name))

const PERMISSION_NAMES = [
  'Fork', 'Resume', 'Join', 'Horizon', 'Fission', 'Advisor', 'Ask', 'DevOpsConsultation',
  'Read', 'Write', 'Edit', 'Glob', 'Grep', 'Move', 'Remove', 'Exec', 'Pty', 'Fetch',
  'ReviewAssessment', 'Review', 'Finality',
]

const toolPermissionByName = Object.fromEntries(PERMISSION_NAMES.map((n) => [n, n]))

const ToolPermission = toolPermissionByName

const permsOf = (names) => names.map((n) => toolPermissionByName[n])

// The member expectation map is the JS-side encoding of the code's
// ToolPermission -> JsCapability mapping: the expected set is rolePermissions
// crossed with it, never a hand list of projections per role. The manager's
// read-only members keep their own namespace.
const MEMBERS_BY_PERMISSION = {
  Read: ['cat', 'stat', 'whereAll', 'where'],
  Glob: ['ls'],
  Grep: ['rg'],
  Write: ['write', 'mkdir'],
  Move: ['mv'],
  Remove: ['rm'],
  Exec: ['call'],
  Fork: ['fork'],
  Resume: ['resume'],
  DevOpsConsultation: ['devops'],
  Join: ['join'],
  Fission: ['fission'],
  Horizon: ['horizon'],
  Advisor: ['advisor'],
  Ask: ['ask'],
}

const MEMBER_ORDER = [
  'write', 'rm', 'mv',
  'cat', 'ls', 'rg', 'stat', 'mkdir', 'whereAll', 'where',
  'call',
  'fork', 'resume', 'commission', 'devops', 'dag',
  'join', 'fission', 'horizon', 'advisor', 'ask',
]

// The offices that hold at least one programming-surface permission, with the
// member set each of them projects. The delegation members are programming
// surface members only: the host registers no fork/resume/commission/devops
// tool of its own.
const EXPECTED_MEMBERS = [
  {
    role: 'Engineer',
    label: 'engineer',
    members: ['write', 'rm', 'mv', 'cat', 'ls', 'rg', 'stat', 'mkdir', 'whereAll', 'where', 'fission', 'advisor'],
  },
  {
    role: 'DevOps',
    label: 'devops',
    members: ['write', 'rm', 'mv', 'cat', 'ls', 'rg', 'stat', 'mkdir', 'whereAll', 'where', 'call', 'join', 'horizon', 'advisor'],
  },
  {
    role: 'Manager',
    label: 'manager',
    members: ['reviewOnly.cat', 'reviewOnly.ls', 'reviewOnly.rg', 'reviewOnly.stat', 'whereAll', 'where', 'fork', 'resume', 'dag', 'join', 'horizon', 'advisor'],
  },
  {
    role: 'Orchestrator',
    label: 'orchestrator',
    members: ['commission', 'join', 'horizon', 'advisor'],
  },
  {
    role: 'Plan',
    label: 'plan',
    members: ['write', 'cat', 'stat', 'mkdir', 'whereAll', 'where', 'devops', 'ask'],
  },
]

// The offices whose permission set projects no member at all.
const SURFACE_LESS_ROLES = ['Coder', 'Inspector', 'Browser', 'Inquiry', 'Distiller', 'Advisor']

test('WHAT[repository-programming-001] JS001_generate_none_when_no_filesystem_capability', () => {
  for (const role of SURFACE_LESS_ROLES) {
    const perms = caps(...permsOf(rolePermissions(role.toLowerCase())))
    assert.equal(isNone(surface(role, rolePermissions(role.toLowerCase()))), true, `${role} must get no js-* surface`)
    assert.equal(isGeneratedToolName(role, perms, `todowrite-for-${role.toLowerCase()}`), false)
  }

  // Orchestrator has no file permission but holds Fork/Join, so it gets a
  // delegation-only programming surface (WHAT[repository-programming-056]).
  const orchestrator = surface('Orchestrator', rolePermissions('orchestrator'))
  assert.equal(isNone(orchestrator), false, 'Orchestrator gets the delegation-only surface')
  assert.deepEqual(
    projectedMembers(orchestrator),
    ['commission', 'join', 'horizon', 'advisor'],
  )
})

test('WHAT[repository-programming-001] JS001_role_projection_is_exactly_roles_permissions_intersection', () => {
  for (const office of EXPECTED_MEMBERS) {
    const permissions = rolePermissions(office.label)
    const result = surface(office.role, permissions)
    assert.equal(isSome(result), true, `${office.role} must get a js-* surface`)
    assert.deepEqual(projectedMembers(result), office.members, `${office.role} member set`)

    // The member gate resolves exactly the members the surface projects.
    for (const member of office.members) {
      assert.equal(
        memberBinding(office.role, permissions, member),
        member,
        `${office.role}: ${member} runtime gate`,
      )
    }
  }
})

test('WHAT[repository-programming-001] JS001_manager_carries_the_delegation_members_of_its_office', () => {
  const permissions = rolePermissions('manager')
  const manager = surface('Manager', permissions)
  const names = memberNames(manager)

  // Manager holds Fork/Resume/Join/Horizon/Advisor, so it carries the
  // dispatch members and the pull members alike.
  for (const member of ['fork', 'dag', 'resume', 'join', 'horizon', 'advisor']) {
    assert.ok(names.includes(member), `Manager carries ${member}`)
    assert.equal(memberBinding('Manager', permissions, member), member, `Manager: ${member} runtime gate`)
  }

  // commission is the Orchestrator's projection of Fork and devops belongs to
  // the DevOpsConsultation permission: Manager holds neither.
  assert.equal(names.includes('commission'), false, 'commission is not a Manager member')
  assert.equal(names.includes('devops'), false, 'devops is not a Manager member')
  assert.equal(memberBinding('Manager', permissions, 'commission'), undefined)
  assert.equal(memberBinding('Manager', permissions, 'devops'), undefined)
})

test('WHAT[repository-programming-001] JS001_delegation_permissions_project_programming_surface_members', () => {
  // The delegation verbs live on the programming surface of the office that
  // holds the permission — nowhere else.
  assert.equal(memberBinding('Manager', rolePermissions('manager'), 'fork'), 'fork')
  assert.equal(memberBinding('Manager', rolePermissions('manager'), 'resume'), 'resume')
  assert.equal(memberBinding('Orchestrator', rolePermissions('orchestrator'), 'commission'), 'commission')
  assert.equal(memberBinding('Plan', rolePermissions('plan'), 'devops'), 'devops')

  // A permission nobody holds projects no member at all.
  assert.equal(memberBinding('Engineer', rolePermissions('engineer'), 'fork'), undefined)
  assert.equal(memberBinding('Engineer', rolePermissions('engineer'), 'resume'), undefined)
  assert.equal(memberBinding('Engineer', rolePermissions('engineer'), 'commission'), undefined)
  assert.equal(memberBinding('Engineer', rolePermissions('engineer'), 'devops'), undefined)
})

test('WHAT[repository-programming-001] JS001_lifecycle_members_are_carried_by_every_generated_surface', () => {
  for (const office of EXPECTED_MEMBERS) {
    const result = surface(office.role, rolePermissions(office.label))
    assert.equal(isSome(result), true, `${office.role} must get a js-* surface`)
    assert.match(result.baseClassSource, /async park\(payload\)/, `${office.role} carries park`)
    assert.match(result.baseClassSource, /async deliver\(reportMapping\)/, `${office.role} carries deliver`)
  }
})

test('WHAT[repository-programming-001] JS001_non_projected_permissions_never_produce_members', () => {
  for (const name of ['Edit', 'Pty', 'Fetch', 'ReviewAssessment', 'Review', 'Finality']) {
    const result = generate('Engineer', caps(toolPermissionByName[name]), 'en')
    assert.equal(isNone(result), true, `${name} alone must not generate a surface`)
  }
})

test('WHAT[repository-programming-001] JS001_member_projection_follows_the_capability_families', () => {
  // The capability families are the only source of a member: a permission that
  // projects members brings exactly its own family, in the canonical order.
  for (const [permission, members] of Object.entries(MEMBERS_BY_PERMISSION)) {
    const result = generate('Engineer', caps(toolPermissionByName[permission]), 'en')
    assert.equal(isSome(result), true, `${permission} alone must generate a surface`)
    const projected = projectedMembers(result)
    const expected = members
      .slice()
      .sort((left, right) => MEMBER_ORDER.indexOf(left) - MEMBER_ORDER.indexOf(right))
    assert.deepEqual(projected, expected, `${permission} member family`)
  }
})
