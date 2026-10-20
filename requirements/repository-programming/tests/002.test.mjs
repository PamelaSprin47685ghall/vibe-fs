import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { rolePredicate } from '../../../dist/OpenCode/Tools/ToolRegistrySurface.js'
import { allRoleLabels } from '../../../dist/Foundation/RolesSurface.js'
import {
  generate,
  generateRole,
  memberBinding,
} from '../../../dist/Repository/Programming/Js/GeneratorSurface.js'
import { permissions as rolePermissions } from '../../../dist/Participant/Persona/OfficeCapabilitySurface.js'
import {
  execute,
  caseName,
  render,
} from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import {
  installDelegationHost,
  clearDelegationHost,
  deliveredValue,
  deliveredViewOf,
  clearDeliveredValue,
} from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'

const caps = (...permissions) => permissions

const surface = (role, permissionNames) => generate(role, caps(...permissionNames), 'en')

const memberNames = (s) => s.members.map((fragment) => fragment.memberName)

// park and deliver are lifecycle members: every generated surface carries them
// in its base class, but they are not the projection of a ToolPermission.
const LIFECYCLE_MEMBERS = ['deliver', 'park']

const projectedMembers = (s) => memberNames(s).filter((name) => !LIFECYCLE_MEMBERS.includes(name))

const isNone = (value) => value === null

const isSome = (value) => value !== null

// The member expectation map is the JS-side encoding of the code's
// ToolPermission -> JsCapability mapping: the expected set is rolePermissions
// crossed with it, never a hand list of projections per role.
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
  call: 'call',
  fork: 'fork',
  resume: 'resume',
  commission: 'commission',
  devops: 'devops',
  dag: 'dag',
  join: 'join',
  fission: 'fission',
  horizon: 'horizon',
  advisor: 'advisor',
  ask: 'ask',
  deliver: 'deliver',
  park: 'park',
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
const EXPECTED_MEMBERS = {
  engineer: ['write', 'rm', 'mv', 'cat', 'ls', 'rg', 'stat', 'mkdir', 'whereAll', 'where', 'fission', 'advisor'],
  devops: ['write', 'rm', 'mv', 'cat', 'ls', 'rg', 'stat', 'mkdir', 'whereAll', 'where', 'call', 'join', 'horizon', 'advisor'],
  manager: ['reviewOnly.cat', 'reviewOnly.ls', 'reviewOnly.rg', 'reviewOnly.stat', 'whereAll', 'where', 'fork', 'resume', 'dag', 'join', 'horizon', 'advisor'],
  orchestrator: ['commission', 'join', 'horizon', 'advisor'],
  plan: ['write', 'cat', 'stat', 'mkdir', 'whereAll', 'where', 'devops', 'ask'],
}

// Sugar members are compiled inside the base class itself instead of
// delegating to `_api` (dag builds fork waves plus one join), so their base-class
// layer is the public method they declare.
const SUGAR_MEMBERS = ['dag', 'commission', 'resume', 'devops']

const layersOf = (s) =>
  Object.fromEntries(
    s.members.map((fragment) => [
      fragment.memberName,
      {
        binding: fragment.memberName.split('.').at(-1),
        inBaseClass: s.baseClassSource.includes(`this._api.${fragment.memberName.split('.').at(-1)}`) || s.baseClassSource.includes(`async ${fragment.memberName}(`),
        inDescription: s.description.includes(fragment.memberName),
        inExamples: s.examples.some((example) => example.includes(fragment.memberName)),
      },
    ]),
  )

// The description block that documents one member: from its pseudocode
// signature up to the end of that block.
const memberProse = (description, memberName) => {
  const start = description.indexOf(`async ${memberName}(`)
  if (start < 0) return null
  const end = description.indexOf('\n  }', start)
  return description.slice(start, end < 0 ? undefined : end)
}

// The body of one base-class method: from its signature to the brace that
// closes it.
const methodBody = (source, memberName) => {
  const start = source.indexOf(`async ${memberName}(`)
  if (start < 0) return null
  let depth = 0
  let opened = false
  for (let index = start; index < source.length; index += 1) {
    const character = source[index]
    if (character === '{') {
      depth += 1
      opened = true
    } else if (character === '}') {
      depth -= 1
      if (opened && depth === 0) return source.slice(start, index + 1)
    }
  }
  return source.slice(start)
}

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-surface-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}

const program = (body) => `class Js extends JsProgram {
  async run() {
${body}
  }
}`

const runProgram = (dir, role, sessionId, body) =>
  execute(dir, role, 'en', sessionId, { program: program(body) }, 3000, Date.now() + 60_000, 1 << 20, null)

// A recording delegation host: every dispatch member travels the single
// `(name, payload, reportMapping)` entry, so the payload the host receives is
// the payload the member built.
const recordingHost = () => {
  const calls = []
  return {
    calls,
    delegate: async (name, payload, reportMapping) => {
      calls.push({ member: name, payload, reportMapping })
      return { ok: true, name: payload?.name ?? '', agentId: 'agent-1', outcome: 'created' }
    },
    join: async () => ({ ok: true, records: [], byName: {} }),
    fission: async () => ({ ok: false, code: 'fission-unavailable' }),
    horizon: async () => ({ ok: true, agents: [], ptys: [] }),
    advisor: async () => ({ ok: true, advice: 'stub-advice' }),
  }
}

const install = (sessionId, host) =>
  installDelegationHost(sessionId, host.delegate, host.join, host.fission, host.horizon, host.advisor)

test('WHAT[repository-programming-002] registered_js_tools_match_js_tool_generator_output', () => {
  for (const role of allRoleLabels) {
    const generated = generateRole(role, 'en')
    const toolName = `todowrite-for-${role}`
    const admitted = rolePredicate(toolName, role)
    if (generated) {
      assert.equal(admitted, true, `Role ${role} has generated surface ${generated.toolName} but ToolRegistry denied it`)
      assert.equal(generated.toolName, toolName, `Generator tool name mismatch for ${role}`)
    } else {
      assert.equal(admitted, false, `Role ${role} has no generated surface but ToolRegistry admitted ${toolName}`)
    }
  }
})

test('WHAT[repository-programming-002] the delegation verbs are programming-surface members only: no host tool is registered for them', () => {
  for (const name of ['fork', 'resume', 'commission', 'devops']) {
    for (const role of allRoleLabels) {
      assert.equal(rolePredicate(name, role), false, `${name} must not be a host tool for ${role}`)
    }
  }

  // The office that holds the permission carries the member on its own
  // programming surface.
  assert.equal(memberBinding('Manager', rolePermissions('manager'), 'fork'), 'fork')
  assert.equal(memberBinding('Manager', rolePermissions('manager'), 'resume'), 'resume')
  assert.equal(memberBinding('Orchestrator', rolePermissions('orchestrator'), 'commission'), 'commission')
  assert.equal(memberBinding('Plan', rolePermissions('plan'), 'devops'), 'devops')
})

test('WHAT[repository-programming-002] every capability family reaches all four layers', () => {
  for (const [permission, family] of Object.entries(MEMBERS_BY_PERMISSION)) {
    const result = surface('Engineer', caps(permission))
    assert.equal(isSome(result), true, `${permission} projects a surface`)

    const layers = layersOf(result)
    for (const member of family) {
      assert.ok(memberNames(result).includes(member), `${permission}: ${member} is a member`)
      assert.equal(layers[member].inBaseClass, true, `${permission}: ${member} in base class`)
      assert.equal(layers[member].inDescription, true, `${permission}: ${member} in description`)
      assert.equal(
        memberBinding('Engineer', caps(permission), member),
        member,
        `${permission}: ${member} runtime gate`,
      )
    }

    // The members of one surface keep the canonical member order.
    const positions = memberNames(result)
      .filter((name) => MEMBER_ORDER.includes(name))
      .map((name) => MEMBER_ORDER.indexOf(name))
    assert.deepEqual(positions, [...positions].sort((left, right) => left - right), `${permission}: canonical member order`)
  }
})

test('WHAT[repository-programming-002] JS004_capability_exactness_plus_one_ultra_example_coder', () => {
  const result = surface('Engineer', ['Read', 'Write', 'Edit', 'Glob', 'Grep'])
  assert.equal(isSome(result), true)
  const layers = layersOf(result)
  assert.deepEqual(
    Object.keys(layers).sort(),
    ['cat', 'ls', 'mkdir', 'rg', 'stat', 'where', 'whereAll', 'write'],
  )
  for (const [member, layer] of Object.entries(layers)) {
    assert.equal(layer.inBaseClass, true, `${member} in base class`)
    assert.equal(layer.inDescription, true, `${member} in description`)
    assert.equal(layer.binding, BINDING_BY_MEMBER[member.split('.').at(-1)], `${member} binding`)
  }
  assert.match(result.description, /cat\(path\)/)
  assert.match(result.description, /where\(content, needle, from = 0\)/)
  assert.match(result.description, /whereAll\(content, needle, from = 0\)/)
  assert.match(result.description, /write\(path, content\)/)
  assert.match(result.description, /mkdir\(path\)/)
  assert.equal(result.description.includes('file(path'), false)
  assert.equal(result.description.includes('edit(path'), false)
  assert.equal(result.description.includes('rewrite(path'), false)
  assert.equal(result.description.includes('_api'), false)
  assert.equal(result.description.includes('__jsFailure'), false)
  assert.equal(result.examples.length, 1, 'one responsibility-shaped Ultra Example')
  assert.match(result.examples[0], /this\.rg\(/)
  assert.match(result.examples[0], /oldApi/)
})

test('WHAT[repository-programming-002] JS004_absent_capability_is_absent_in_all_four_layers', () => {
  const result = surface('Engineer', ['Read', 'Glob', 'Grep']) // no Edit / Write
  assert.equal(isSome(result), true)
  assert.deepEqual(memberNames(result), ['cat', 'ls', 'rg', 'stat', 'whereAll', 'where'])
  assert.equal(result.description.includes('edit(path'), false)
  assert.equal(result.description.includes('rewrite(path'), false)
  assert.equal(result.description.includes('write(path, content)'), false)
  assert.equal(result.baseClassSource.includes('this.edit'), false)
  assert.equal(result.baseClassSource.includes('write'), false)
  assert.equal(result.examples.some((example) => example.includes('write')), false)
})

test('WHAT[repository-programming-002] JS004_edit_guidance_never_names_missing_read_or_write_members', () => {
  const editOnly = surface('Engineer', ['Edit'])
  assert.equal(isNone(editOnly), true, 'Edit permission alone projects no JsCapability after legacy retirement')

  const writeOnly = surface('Engineer', ['Write'])
  assert.deepEqual(memberNames(writeOnly), ['write', 'mkdir'])
  for (const unavailable of [
    'async cat(path)',
    'async where(content',
    'whereAll(content, needle',
    'file(matches)',
    'file() + JavaScript',
    'file(path',
    'FileView.text',
    'edit(path',
    'rewrite(path',
  ]) {
    assert.equal(writeOnly.description.includes(unavailable), false, `Write-only surface leaked ${unavailable}`)
  }
  assert.match(writeOnly.description, /(^|[^A-Za-z0-9_])write\(path, content\)/m)

  const readEdit = surface('Engineer', ['Read', 'Edit'])
  assert.deepEqual(memberNames(readEdit), ['cat', 'stat', 'whereAll', 'where'])
  assert.equal(readEdit.description.includes('cat(path)'), true)
  assert.equal(readEdit.description.includes('file(matches)'), false)
  assert.equal(readEdit.description.includes('edit(path'), false)
  assert.equal(readEdit.description.includes('rewrite(path'), false)
  assert.doesNotMatch(readEdit.description, /(^|[^A-Za-z0-9_])write\(path, content\)/m)

  const editWrite = surface('Engineer', ['Edit', 'Write'])
  assert.deepEqual(memberNames(editWrite), ['write', 'mkdir'])
  assert.equal(editWrite.description.includes('file(matches)'), false)
  assert.equal(editWrite.description.includes('file(path'), false)
  assert.equal(editWrite.description.includes('FileView.text'), false)
  assert.match(editWrite.description, /(^|[^A-Za-z0-9_])write\(path, content\)/m)
})

test('WHAT[repository-programming-002] JS004_member_gate_binds_present_members_only', () => {
  const perms = caps('Read', 'Glob', 'Grep')
  assert.equal(memberBinding('Engineer', perms, 'file'), undefined)
  assert.equal(memberBinding('Engineer', perms, 'glob'), undefined)
  assert.equal(memberBinding('Engineer', perms, 'grep'), undefined)
  assert.equal(memberBinding('Engineer', perms, 'cat'), 'cat')
  assert.equal(memberBinding('Engineer', perms, 'ls'), 'ls')
  assert.equal(memberBinding('Engineer', perms, 'stat'), 'stat')
  assert.equal(memberBinding('Engineer', perms, 'whereAll'), 'whereAll')
  assert.equal(memberBinding('Engineer', perms, 'where'), 'where')
  assert.equal(memberBinding('Engineer', perms, 'edit'), undefined)
  assert.equal(memberBinding('Engineer', perms, 'rewrite'), undefined)
  assert.equal(memberBinding('Engineer', perms, 'write'), undefined)
})

test('WHAT[repository-programming-002] JS004_lying_generator_counterexample_is_rejected', () => {
  // A "lying" surface advertises a member with no runtime binding — the exact
  // failure mode the four-layer invariant exists to make impossible. The gate
  // must refuse the member: memberBinding returns undefined for it, so a
  // forged call cannot resolve an executor (JS-004).
  const perms = caps('Read', 'Glob', 'Grep')
  // lie: retired 'file'/'edit'/'rewrite' and absent 'write' are NOT in this surface's bindings
  assert.equal(memberBinding('Inspector', perms, 'file'), undefined)
  assert.equal(memberBinding('Inspector', perms, 'edit'), undefined)
  assert.equal(memberBinding('Inspector', perms, 'rewrite'), undefined)
  assert.equal(memberBinding('Inspector', perms, 'write'), undefined)
  // the honest members resolve
  assert.equal(memberBinding('Inspector', perms, 'cat'), 'cat')
  // a lying description would name methods the surface lacks; the generator
  // never does — prove the surface itself contains no unbounded members
  const result = generate('Inspector', perms, 'en')
  const names = result.members.map((f) => f.memberName)
  for (const name of names) {
    assert.notEqual(memberBinding('Inspector', perms, name), undefined, `${name} must resolve`)
  }
  assert.equal(names.includes('rewrite'), false)
  assert.equal(names.includes('write'), false)
})

test('WHAT[repository-programming-002] JS004_horizon_projects_four_layers_for_every_holding_role', () => {
  const holders = allRoleLabels.filter((role) => rolePermissions(role.toLowerCase()).includes('Horizon'))
  assert.deepEqual(holders, ['devops', 'manager', 'orchestrator'], 'the roles holding ToolPermission.Horizon')

  for (const role of holders) {
    const permissions = rolePermissions(role.toLowerCase())
    const result = surface(role, permissions)
    assert.equal(isSome(result), true, `${role} gets a js-* surface`)
    assert.deepEqual(projectedMembers(result), EXPECTED_MEMBERS[role], `${role} member set`)
    assert.equal(
      memberBinding(role, permissions, 'horizon'),
      'horizon',
      `${role} horizon runtime gate`,
    )

    const layers = layersOf(result)
    assert.equal(layers.horizon.binding, 'horizon', `${role}: horizon binding key`)
    assert.match(result.description, /async horizon\(\)/, `${role}: horizon pseudocode signature`)
    for (const [member, layer] of Object.entries(layers)) {
      const baseClassMarker = SUGAR_MEMBERS.includes(member)
        ? `async ${member}(`
        : `this._api.${layer.binding}`
      assert.equal(
        result.baseClassSource.includes(baseClassMarker),
        true,
        `${role}: ${member} in base class`,
      )
      assert.equal(layer.inDescription, true, `${role}: ${member} in description`)
      assert.equal(layer.binding, BINDING_BY_MEMBER[member.split('.').at(-1)], `${role}: ${member} binding`)
    }
  }

  // The permission is the sole source of the member: Fork/Join without Horizon
  // project fork/dag/join but never horizon.
  assert.equal(memberBinding('Engineer', caps('Fork', 'Join'), 'horizon'), undefined)
})

test('WHAT[repository-programming-002] JS004_advisor_projects_four_layers_for_every_holding_role', () => {
  const holders = allRoleLabels.filter((role) => rolePermissions(role.toLowerCase()).includes('Advisor'))
  assert.deepEqual(holders, ['devops', 'engineer', 'manager', 'orchestrator'], 'the roles holding ToolPermission.Advisor')

  for (const role of holders) {
    const permissions = rolePermissions(role.toLowerCase())
    const result = surface(role, permissions)
    assert.equal(isSome(result), true, `${role} gets a js-* surface`)
    assert.deepEqual(projectedMembers(result), EXPECTED_MEMBERS[role], `${role} member set`)
    assert.equal(
      memberBinding(role, permissions, 'advisor'),
      'advisor',
      `${role} advisor runtime gate`,
    )

    const layers = layersOf(result)
    assert.equal(layers.advisor.binding, 'advisor', `${role}: advisor binding key`)
    assert.match(result.description, /async advisor\(intent, view\)|async advisor\(intent, reportMapping\)/, `${role}: advisor pseudocode signature`)
    for (const [member, layer] of Object.entries(layers)) {
      const baseClassMarker = SUGAR_MEMBERS.includes(member)
        ? `async ${member}(`
        : `this._api.${layer.binding}`
      assert.equal(
        result.baseClassSource.includes(baseClassMarker),
        true,
        `${role}: ${member} in base class`,
      )
      assert.equal(layer.inDescription, true, `${role}: ${member} in description`)
      assert.equal(layer.binding, BINDING_BY_MEMBER[member.split('.').at(-1)], `${role}: ${member} binding`)
    }
  }

  // The permission is the sole source of the member: Fork/Join without Advisor
  // project fork/dag/join but never advisor.
  const delegationOnly = surface('Engineer', caps('Fork', 'Join'))
  assert.deepEqual(memberNames(delegationOnly), ['fork', 'dag', 'join'])
  assert.equal(memberBinding('Engineer', caps('Fork', 'Join'), 'advisor'), undefined)
  assert.deepEqual(memberNames(surface('Engineer', caps('Advisor'))), ['advisor'])

  // Absence: a role without the permission never projects the member — the
  // advisor seat itself holds no permission at all, and plan keeps its
  // surface advisor-free.
  const without = allRoleLabels.filter((role) => !rolePermissions(role.toLowerCase()).includes('Advisor'))
  assert.deepEqual(without, ['advisor', 'plan'], 'the roles without ToolPermission.Advisor')
  for (const role of without) {
    assert.equal(
      memberBinding(role, rolePermissions(role.toLowerCase()), 'advisor'),
      undefined,
      `${role} never resolves this.advisor`,
    )
  }
})

test('WHAT[repository-programming-002] dispatch members carry a reportMapping; pull members never do', () => {
  const manager = surface('Manager', rolePermissions('manager'))
  assert.equal(isSome(manager), true)

  // Dispatch members: the report material travels with the call.
  for (const member of ['fork', 'resume', 'dag', 'advisor']) {
    assert.match(
      manager.baseClassSource,
      new RegExp(`async ${member}\\([^)]*reportMapping`),
      `${member} takes a reportMapping`,
    )
    const prose = memberProse(manager.description, member)
    assert.ok(prose, `${member} is documented in the description`)
    assert.match(prose, /reportMapping/, `${member} documents the reportMapping`)
  }

  // Pull members: they gather, they do not report — no reportMapping at all.
  for (const member of ['join', 'horizon']) {
    assert.match(
      manager.baseClassSource,
      new RegExp(`async ${member}\\(\\)`),
      `${member} takes no argument`,
    )
    const prose = memberProse(manager.description, member)
    assert.ok(prose, `${member} is documented in the description`)
    assert.equal(/reportMapping/.test(prose), false, `${member} documents no reportMapping`)
  }

  const engineer = surface('Engineer', rolePermissions('engineer'))
  assert.match(engineer.baseClassSource, /async fission\(payload\)/, 'fission takes its payload only')
  const fissionProse = memberProse(engineer.description, 'fission')
  assert.ok(fissionProse, 'fission is documented in the description')
  assert.equal(/reportMapping/.test(fissionProse), false, 'fission documents no reportMapping')
})

test('WHAT[repository-programming-002] dag is emitted only when Fork and Join both exist, and its body calls no missing member', () => {
  const forkOnly = surface('Engineer', caps('Fork'))
  assert.equal(isSome(forkOnly), true)
  assert.deepEqual(memberNames(forkOnly), ['fork'], 'Fork alone carries fork, never dag')
  assert.equal(forkOnly.baseClassSource.includes('async dag('), false, 'dag needs Join as well')

  const forkAndJoin = surface('Engineer', caps('Fork', 'Join'))
  assert.deepEqual(memberNames(forkAndJoin), ['fork', 'dag', 'join'])

  const body = methodBody(forkAndJoin.baseClassSource, 'dag')
  assert.ok(body, 'the dag method exists in the base class')
  const called = [...body.matchAll(/this\.([A-Za-z0-9_]+)\s*\(/g)].map((match) => match[1])
  assert.ok(called.includes('fork'), 'dag forks its nodes')
  assert.ok(called.includes('join'), 'dag joins the wave')
  for (const member of called) {
    assert.ok(
      memberNames(forkAndJoin).includes(member) || member === 'park',
      `dag must not call the missing member ${member}`,
    )
  }
})

test('WHAT[repository-programming-002] commission derives the name from a single charge', async () => {
  const { dir, cleanup } = sandbox()
  const commissionSessionId = `sess-commission-name-${Math.random().toString(36).slice(2, 8)}`
  try {
    const host = recordingHost()
    install(commissionSessionId, host)

    const commissioned = await runProgram(
      dir,
      'Orchestrator',
      commissionSessionId,
      `    return await this.commission("audit the parser module on its own", { 1: null });`,
    )
    assert.equal(caseName(commissioned), 'Succeeded')
    const commissionCalls = host.calls.filter((call) => call.member === 'commission')
    assert.equal(commissionCalls.length, 1, 'the commission member reaches the delegation host')
    assert.equal(commissionCalls[0].payload.charge, 'audit the parser module on its own')
    assert.equal(typeof commissionCalls[0].payload.name, 'string', 'a single charge derives the name')
    assert.ok(commissionCalls[0].payload.name.length > 0, 'the derived name is not empty')
    assert.match(commissionCalls[0].payload.name, /audit|parser/i, 'the derived name comes from the charge')
  } finally {
    clearDelegationHost(commissionSessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-002] resume does not derive the name from a single charge', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-resume-name-${Math.random().toString(36).slice(2, 8)}`
  try {
    const host = recordingHost()
    install(sessionId, host)

    const resumed = await runProgram(
      dir,
      'Manager',
      sessionId,
      `    const derived = await this.resume("resume the audit", { 1: null });
    const explicit = await this.resume({ charge: "resume the audit", name: "audit-road" }, { 1: null });
    return { derived, explicit };`,
    )
    assert.equal(caseName(resumed), 'Succeeded')
    const resumeCalls = host.calls.filter((call) => call.member === 'resume')
    assert.equal(resumeCalls.length, 2, 'both resume calls reach the delegation host')

    // WHAT[repository-programming-056]: resume 的 payload 必带 name（缺 name 由
    // 宿主以 name-required 拒绝）。生成侧不派生命名，所以单参 charge 抵达宿主
    // 时没有 name——这里断言可观察事实：抵达宿主的载荷形状。
    assert.equal(resumeCalls[0].payload.charge, 'resume the audit', 'the charge travels unchanged')
    assert.equal(
      'name' in resumeCalls[0].payload,
      false,
      'a single charge derives no name for resume; the host refuses a nameless resume',
    )
    assert.equal(resumeCalls[0].payload.name, undefined, 'the nameless resume carries no name value')
    assert.equal(resumeCalls[1].payload.name, 'audit-road', 'an explicit name is kept')
    assert.equal(resumeCalls[0].reportMapping['1'], null, 'the report mapping travels unchanged')
    assert.equal(resumeCalls[1].reportMapping['1'], null, 'the report mapping travels unchanged')
  } finally {
    clearDelegationHost(sessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-002] deliver refuses a second argument and never hands over', async () => {
  const { dir, cleanup } = sandbox()
  const sessionId = `sess-deliver-2nd-${Math.random().toString(36).slice(2, 8)}`
  const acceptedSessionId = `${sessionId}-accepted`
  try {
    clearDeliveredValue(sessionId)
    clearDeliveredValue(acceptedSessionId)

    const refused = await runProgram(
      dir,
      'Engineer',
      sessionId,
      `    const refusal = await this.deliver({ 0: "final report" }, "second argument");
    return { refusal };`,
    )
    assert.match(render(refused), /VIEW_DECLARATION_INVALID/, 'the second argument is refused')
    assert.equal(deliveredValue(sessionId), null, 'a refused delivery never populates the delivery cell')

    // The single-argument form still hands over: the refusal is about the
    // second argument, not about delivery itself.
    const accepted = await runProgram(
      dir,
      'Engineer',
      acceptedSessionId,
      `    await this.deliver({ 0: "final report" });`,
    )
    assert.equal(caseName(accepted), 'Succeeded')
    const view = deliveredViewOf(acceptedSessionId)
    assert.equal(view.prefixUntil, 0, 'the accepted delivery is read as a prefixUntil 0 declaration')
    assert.equal(view.thenMap['0'], 'final report', 'the accepted delivery records the report mapping')
  } finally {
    clearDeliveredValue(sessionId)
    clearDeliveredValue(acceptedSessionId)
    cleanup()
  }
})

test('WHAT[repository-programming-002] the devops member documents the { ok, kind, status, view } result shape', () => {
  const plan = surface('Plan', rolePermissions('plan'))
  assert.equal(isSome(plan), true)
  assert.ok(memberNames(plan).includes('devops'), 'the DevOpsConsultation permission projects the devops member')

  const prose = memberProse(plan.description, 'devops')
  assert.ok(prose, 'the devops member is documented in the description')
  assert.match(
    prose,
    /\bok\b[\s\S]{0,40}?kind\b[\s\S]{0,40}?status\b[\s\S]{0,40}?view\b/,
    'the documented devops result names ok, kind, status and view',
  )
  assert.match(plan.baseClassSource, /async devops\(intent, reportMapping\)/, 'devops takes an intent and a reportMapping')
})
