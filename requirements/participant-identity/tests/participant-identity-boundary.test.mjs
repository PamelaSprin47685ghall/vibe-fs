// WHAT[PID-002] — ParticipantIdentity is one opaque owner bound to an exact logical
// run; authority facts/profiles carry only its IdentitySeed, and no session-scoped
// cache, registry, second fact owner or raw internal construction may re-create a
// parallel identity authority.

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { scanRepo } from '../../../scripts/checks/participant-identity-boundary.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const IDENTITY_OWNER = 'src/Wanxiangshu/Participant/Persona/Identity.fs'
const AUTHORITY_FACTS = 'src/Wanxiangshu/Interaction/Authority/Facts.fs'
const AUTHORITY_MODEL = 'src/Wanxiangshu/Interaction/Authority/Model.fs'
const SURFACE_MANIFEST = 'scripts/lib/test-surface-scan.mjs'

const violation = (file, line, rule, message) => ({ file, line, rule, message })

// Line 1 namespace, 2 ParticipantIdentity, 3 ParticipantIdentityEvidence,
// 4 module, 5..13 the nine owner API members.
const identityOwner = [
  'namespace Wanxiangshu.Participant.Persona',
  'type ParticipantIdentity = private { Value: string }',
  'type ParticipantIdentityEvidence = private { Identity: ParticipantIdentity }',
  'module ParticipantIdentity =',
  '    let resolveAtRoot value = value',
  '    let inheritFromOwner value owner = value, owner',
  '    let rehydrate owner input = owner, input',
  '    let selectedAgent value = value',
  '    let peerAgent value = value',
  '    let role value = value',
  '    let initialTier value = value',
  '    let persona value = value',
  '    let origin value = value',
].join('\n')

// AuthorityRootAcceptedPayload declared on line 2.
const authorityFacts = [
  'namespace Wanxiangshu.Interaction.Authority',
  'type AuthorityRootAcceptedPayload =',
  '    { SchemaVersion: int',
  '      IdentitySeed: PromptIdentitySeed }',
].join('\n')

// AuthorityExecutionProfile declared on line 3, derivation member on line 6.
const authorityModel = [
  'namespace Wanxiangshu.Interaction.Authority',
  'type IdentitySeed = PromptIdentitySeed',
  'type AuthorityExecutionProfile =',
  '    private',
  '        { StoredIdentitySeed: IdentitySeed }',
  '    member this.ParticipantIdentity = PromptIdentitySeed.participantIdentity this.StoredIdentitySeed',
].join('\n')

const CLEAN_SHAPE = {
  [IDENTITY_OWNER]: identityOwner,
  [AUTHORITY_FACTS]: authorityFacts,
  [AUTHORITY_MODEL]: authorityModel,
}

const writeFixture = (root, relativePath, text) => {
  const path = join(root, relativePath)
  mkdirSync(dirname(path), { recursive: true })
  writeFileSync(path, text)
}

// Builds a minimal root that the gate accepts, then applies `overrides`:
// a string replaces the file, `null` removes it from the clean shape.
const withBoundaryRoot = (overrides, assertScan) => {
  const root = mkdtempSync(join(tmpdir(), 'participant-identity-boundary-'))
  try {
    const shape = { ...CLEAN_SHAPE, ...overrides }
    for (const [relativePath, text] of Object.entries(shape)) {
      if (text === null) continue
      writeFixture(root, relativePath, text)
    }
    assertScan(root)
  } finally {
    rmSync(root, { recursive: true, force: true })
  }
}

test('WHAT[PID-002] the merged identity boundary shape is accepted', () => {
  withBoundaryRoot({}, (root) => assert.deepEqual(scanRepo(root), []))
})

test('WHAT[PID-002] a missing identity owner file is a required-surface violation', () => {
  withBoundaryRoot({ [IDENTITY_OWNER]: null }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(IDENTITY_OWNER, 1, 'required-surface', 'required identity surface is missing'),
    ]),
  )
})

test('WHAT[PID-002] a retired identity production file cannot come back', () => {
  const retired = 'src/Wanxiangshu/Participant/Persona/SessionPersona.fs'
  withBoundaryRoot({ [retired]: 'namespace Wanxiangshu.Participant.Persona\n' }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(retired, 1, 'retired-identity-file', 'retired identity production file must not exist'),
    ]),
  )
})

test('WHAT[PID-001] retired identity tokens cannot reappear in production source', () => {
  const file = 'src/Wanxiangshu/OpenCode/Host/Revival.fs'
  const source = ['namespace Wanxiangshu.OpenCode.Host', 'let bind (persona: SessionPersona) = persona'].join('\n')
  withBoundaryRoot({ [file]: source }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        file,
        2,
        'retired-identity-token',
        "retired identity token 'SessionPersona' is forbidden in production source",
      ),
    ]),
  )
})

test('WHAT[PID-002] the retired session surface cannot be registered as a test surface', () => {
  const manifest = ['export const SURFACES = [', "  { module: 'Participant/Persona/SessionSurface.js' },", ']'].join('\n')
  withBoundaryRoot({ [SURFACE_MANIFEST]: manifest }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        SURFACE_MANIFEST,
        2,
        'retired-session-surface-registration',
        "retired surface 'Participant/Persona/SessionSurface.js' must not be registered",
      ),
    ]),
  )
})

test('WHAT[PID-002] the retired session surface cannot be emitted into dist', () => {
  const emitted = 'dist/Participant/Persona/SessionSurface.js'
  withBoundaryRoot({ [emitted]: 'export const surface = {}\n' }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        emitted,
        1,
        'retired-session-surface-emission',
        "retired surface 'Participant/Persona/SessionSurface.js' must not be emitted",
      ),
    ]),
  )
})

test('WHAT[PID-002] ParticipantIdentity must stay an opaque private record', () => {
  const leaked = identityOwner.replace('type ParticipantIdentity = private {', 'type ParticipantIdentity = {')
  withBoundaryRoot({ [IDENTITY_OWNER]: leaked }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        IDENTITY_OWNER,
        1,
        'opaque-identity-owner',
        'ParticipantIdentity must remain an opaque private record',
      ),
    ]),
  )
})

test('WHAT[PID-003] the identity owner API cannot drop rehydrate', () => {
  const truncated = identityOwner.replace('    let rehydrate owner input = owner, input\n', '')
  withBoundaryRoot({ [IDENTITY_OWNER]: truncated }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(IDENTITY_OWNER, 1, 'opaque-identity-api', "ParticipantIdentity owner API is missing 'rehydrate'"),
    ]),
  )
})

test('WHAT[PID-002] AuthorityRootAcceptedPayload must store the IdentitySeed', () => {
  const seedless = authorityFacts.replace('      IdentitySeed: PromptIdentitySeed }', '      AcceptedAt: int }')
  withBoundaryRoot({ [AUTHORITY_FACTS]: seedless }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        AUTHORITY_FACTS,
        2,
        'authority-identity-seed',
        'AuthorityRootAcceptedPayload must store IdentitySeed',
      ),
    ]),
  )
})

test('WHAT[PID-002] AuthorityRootAcceptedPayload cannot flatten IdentitySeed fields', () => {
  const duplicated = authorityFacts.replace(
    '      IdentitySeed: PromptIdentitySeed }',
    '      IdentitySeed: PromptIdentitySeed\n      SelectedAgent: AgentId }',
  )
  withBoundaryRoot({ [AUTHORITY_FACTS]: duplicated }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        AUTHORITY_FACTS,
        2,
        'flat-identity-duplicate',
        'AuthorityRootAcceptedPayload duplicates IdentitySeed fields: SelectedAgent',
      ),
    ]),
  )
})

test('WHAT[PID-002] AuthorityExecutionProfile must derive identity from its seed', () => {
  const undivided = authorityModel.replace(
    '    member this.ParticipantIdentity = PromptIdentitySeed.participantIdentity this.StoredIdentitySeed',
    '    member this.SchemaVersion = 1',
  )
  withBoundaryRoot({ [AUTHORITY_MODEL]: undivided }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        AUTHORITY_MODEL,
        1,
        'authority-derived-identity',
        'AuthorityExecutionProfile must derive ParticipantIdentity from IdentitySeed',
      ),
    ]),
  )
})

test('WHAT[PID-002] a SessionId-keyed identity collection is forbidden', () => {
  const file = 'src/Wanxiangshu/Interaction/Dispatch/Cache.fs'
  const source = [
    'namespace Wanxiangshu.Interaction.Dispatch',
    'open System.Collections.Generic',
    'let issued = Dictionary<SessionId, ParticipantIdentityEvidence>()',
  ].join('\n')
  withBoundaryRoot({ [file]: source }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        file,
        3,
        'session-identity-cache',
        'SessionId-keyed ParticipantIdentity/IdentitySeed collection is forbidden',
      ),
    ]),
  )
})

test('WHAT[PID-002] a SessionId-keyed identity registry is forbidden', () => {
  const file = 'src/Wanxiangshu/Interaction/Dispatch/Registry.fs'
  const source = [
    'namespace Wanxiangshu.Interaction.Dispatch',
    'let identityRegistry (sessionId: SessionId) : ParticipantIdentity option = None',
  ].join('\n')
  withBoundaryRoot({ [file]: source }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        file,
        2,
        'session-identity-registry',
        'SessionId-keyed ParticipantIdentity/IdentitySeed registry is forbidden',
      ),
    ]),
  )
})

test('WHAT[PID-002] a second identity fact owner is forbidden', () => {
  const file = 'src/Wanxiangshu/Interaction/Authority/Second.fs'
  const source = [
    'namespace Wanxiangshu.Interaction.Authority',
    'let fact = ParticipantIdentityEstablished.name',
  ].join('\n')
  withBoundaryRoot({ [file]: source }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        file,
        2,
        'duplicate-identity-fact',
        'ParticipantIdentityEstablished would create a second identity fact owner',
      ),
    ]),
  )
})

test('WHAT[PID-002] raw identity internals cannot be constructed outside the owner', () => {
  const file = 'src/Wanxiangshu/Execution/Binding.fs'
  const source = ['namespace Wanxiangshu.Execution', 'let persona = PersonaName "Coder"'].join('\n')
  withBoundaryRoot({ [file]: source }, (root) =>
    assert.deepEqual(scanRepo(root), [
      violation(
        file,
        2,
        'private-identity-construction',
        'raw construction of opaque ParticipantIdentity internals is forbidden outside its owner',
      ),
    ]),
  )
})

test('WHAT[PID-002] the production participant identity boundary is clean', () => {
  assert.deepEqual(scanRepo(ROOT), [])
})
