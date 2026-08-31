// WHAT[PID-002] — Participant identity has one typed owner; office capability and
// display text cannot become alternate identity authorities.

import assert from 'node:assert/strict'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  scanFoundationRolesSource,
  scanParticipantIdentityEntries,
  scanParticipantIdentityRepo,
  scanPersonaBindingSource,
  scanPersonaCatalogSource,
  scanProductionIdentitySource,
  scanSessionPersonaSource,
} from '../../../scripts/checks/participant-identity-boundary.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const violation = (rule, file, line, text) => ({
  id: 'participant-identity-owner', rule, file, line, text,
})

const PERSONAS = [
  'Integrator', 'Director', 'Coordinator', 'Lead', 'Coder', 'Engineer', 'Scout',
  'Investigator', 'Technician', 'Operator', 'Navigator', 'Researcher', 'Analyst',
  'Inquirer', 'Examiner', 'Auditor', 'Scribe', 'Chronicler', 'Condenser',
  'Distiller', 'Clerk', 'Curator',
]

const catalog = `[<RequireQualifiedAccess>]
type Persona =
${PERSONAS.map((name) => `    | ${name}`).join('\n')}

module PersonaCatalog =
    let render (persona: Persona) : string =
        match persona with
${PERSONAS.map((name) => `        | Persona.${name} -> "${name}"`).join('\n')}
`

const session = `module SessionPersona =
    type PersonaRejection =
        | AlreadyBound of existing: Persona * requested: Persona
    let private bySession = Dictionary<SessionId, Persona>()
    let bindOnce (sessionId: SessionId) (persona: Persona) : Result<Persona, PersonaRejection> =
        match bySession.TryGetValue sessionId with
        | true, existing when existing = persona -> Ok existing
        | true, existing -> Error (PersonaRejection.AlreadyBound(existing, persona))
        | false, _ ->
            bySession.[sessionId] <- persona
            Ok persona
`

const binding = `module PersonaBinding =
    let bind sessionId persona =
        match SessionPersona.bindOnce sessionId persona with
        | Ok bound -> Ok bound
        | Error rejection -> Error rejection
`

const roles = `[<RequireQualifiedAccess>]
type AgentTier = | Fast | Deep
[<RequireQualifiedAccess>]
type Role = | Manager | Coder
module Roles =
    let roleLabel role = match role with | Role.Manager -> "manager" | Role.Coder -> "coder"
`

const catalogFile = 'src/Wanxiangshu/Participant/Persona/Catalog.fs'
const sessionFile = 'src/Wanxiangshu/Participant/Persona/SessionPersona.fs'
const bindingFile = 'src/Wanxiangshu/OpenCode/Host/PersonaBinding.fs'
const rolesFile = 'src/Wanxiangshu/Foundation/Roles.fs'

// Final owner shapes are jointly accepted before individual mutation proofs.
test('WHAT[PID-002] final participant identity owner shapes are accepted', () => {
  assert.deepEqual(scanParticipantIdentityEntries([
    { file: catalogFile, source: catalog },
    { file: sessionFile, source: session },
    { file: bindingFile, source: binding },
    { file: rolesFile, source: roles },
  ]), [])
})

test('WHAT[PID-002] Catalog requires the closed exhaustive Persona DU', () => {
  const mutated = catalog.replace('    | Curator\n', '')
  assert.deepEqual(scanPersonaCatalogSource(catalogFile, mutated), [
    violation('catalog-closed-persona', catalogFile, 1,
      'Catalog must declare the exhaustive RequireQualifiedAccess Persona DU'),
  ])
})

test('WHAT[PID-002] Catalog requires canonical rendering for every Persona', () => {
  const mutated = catalog.replace('        | Persona.Curator -> "Curator"\n', '')
  assert.deepEqual(scanPersonaCatalogSource(catalogFile, mutated), [
    violation('catalog-canonical-rendering', catalogFile, 27,
      'Catalog must render every Persona to its canonical name'),
  ])
})

test('WHAT[PID-003] SessionPersona storage cannot regress to string', () => {
  const mutated = session.replace('Dictionary<SessionId, Persona>', 'Dictionary<string, Persona>')
  assert.deepEqual(scanSessionPersonaSource(sessionFile, mutated), [
    violation('session-persona-storage', sessionFile, 4,
      'SessionPersona storage must contain Persona values'),
  ])
})

test('WHAT[PID-003] SessionPersona rejection is typed', () => {
  const mutated = session.replace('Result<Persona, PersonaRejection>', 'Result<Persona, obj>')
  assert.deepEqual(scanSessionPersonaSource(sessionFile, mutated), [
    violation('session-typed-rejection', sessionFile, 2,
      'bindOnce must expose Result<Persona, PersonaRejection>'),
  ])
})

test('WHAT[PID-003] SessionPersona rejects Result values with string errors', () => {
  const mutated = session.replace('Result<Persona, PersonaRejection>', 'Result<Persona, string>')
  assert.deepEqual(scanSessionPersonaSource(sessionFile, mutated), [
    violation('session-typed-rejection', sessionFile, 2,
      'bindOnce must expose Result<Persona, PersonaRejection>'),
    violation('session-string-rejection', sessionFile, 5, 'Result<Persona, string>'),
  ])
})

test('WHAT[PID-003] bind-once conflicts preserve the original value', () => {
  const mutated = session.replace('Error (PersonaRejection.AlreadyBound(existing, persona))', 'Ok persona')
  assert.deepEqual(scanSessionPersonaSource(sessionFile, mutated), [
    violation('session-bind-once', sessionFile, 5,
      'bindOnce must preserve the original SessionId-scoped Persona'),
  ])
})

test('WHAT[PID-003] Host PersonaBinding cannot swallow a typed conflict', () => {
  const mutated = `module PersonaBinding =
    let bind sessionId persona =
        match SessionPersona.bindOnce sessionId persona with
        | Ok bound -> bound
        | Error _ -> SessionPersona.tryGet sessionId |> Option.defaultValue persona
`
  assert.deepEqual(scanPersonaBindingSource(bindingFile, mutated), [
    violation('binding-conflict-swallowed', bindingFile, 5,
      'PersonaBinding must propagate PersonaRejection conflicts'),
  ])
})

test('WHAT[PID-002] RoleIdentity.fs is prohibited', () => {
  const file = 'src/Wanxiangshu/Participant/Persona/RoleIdentity.fs'
  assert.deepEqual(scanParticipantIdentityEntries([{ file, source: 'module AgentRoleIdentity\n' }]), [
    violation('legacy-role-identity-file', file, 1, 'RoleIdentity.fs must be absent'),
  ])
})

test('WHAT[PID-002] AgentRoleIdentity references are prohibited', () => {
  const file = 'src/Wanxiangshu/Fixture.fs'
  assert.deepEqual(scanProductionIdentitySource(file, 'let role = AgentRoleIdentity.toRole value\n'), [
    violation('legacy-role-identity-reference', file, 1, 'AgentRoleIdentity.'),
  ])
})

for (const symbol of ['ToolPermission', 'permissions', 'isAllowed', 'RoleDefinition']) {
  test(`WHAT[PID-001] Foundation Roles excludes ${symbol} office-capability vocabulary`, () => {
    assert.deepEqual(scanFoundationRolesSource(rolesFile, `module Roles\nlet value = ${symbol}\n`), [
      violation('foundation-role-capability', rolesFile, 2, symbol),
    ])
  })
}

test('WHAT[PID-002] PersonaCatalog output cannot be lowercased into identity authority', () => {
  const file = 'src/Wanxiangshu/Execution/Fixture.fs'
  const source = `let identity role tier =
    PersonaCatalog.persona role tier
    |> fun value -> value.ToLowerInvariant()
`
  assert.deepEqual(scanProductionIdentitySource(file, source), [
    violation('persona-lowercase-authority', file, 2, 'PersonaCatalog.persona role tier'),
  ])
})

test('WHAT[PID-002] persona display text cannot authorize production behavior', () => {
  const file = 'src/Wanxiangshu/Execution/Fixture.fs'
  const source = 'let authorize candidatePersona expected = if candidatePersona = PersonaCatalog.render expected then true else false\n'
  assert.deepEqual(scanProductionIdentitySource(file, source), [
    violation('persona-text-authority', file, 1, source.trim()),
  ])
})

test('WHAT[PID-002] production participant identity boundary is clean', () => {
  assert.deepEqual(
    scanParticipantIdentityRepo(ROOT),
    [],
    'participant-identity-owner debt keeps this proof RED until the production ownership cutover',
  )
})
