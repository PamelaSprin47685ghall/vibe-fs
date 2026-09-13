// WHAT[PID-002] — ParticipantIdentity is one opaque owner bound to an exact logical
// run; authority facts/profiles carry only its IdentitySeed, and no session-scoped
// cache, registry, second fact owner or raw internal construction may re-create a
// parallel identity authority.

import assert from 'node:assert/strict'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import { scanEntries, scanRepo } from '../../../scripts/checks/participant-identity-boundary.mjs'

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const AUTHORITY_FACTS = 'src/Wanxiangshu/Interaction/Authority/Facts.fs'
const AUTHORITY_MODEL = 'src/Wanxiangshu/Interaction/Authority/Model.fs'

const violation = (file, line, rule, message) => ({ file, line, rule, message })

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

test('WHAT[PID-002] the identity boundary entries are accepted for clean authority shape', () => {
  assert.deepEqual(scanEntries([
    { file: AUTHORITY_FACTS, text: authorityFacts },
    { file: AUTHORITY_MODEL, text: authorityModel },
  ]), [])
})

test('WHAT[PID-002] AuthorityRootAcceptedPayload must store the IdentitySeed', () => {
  const seedless = authorityFacts.replace('      IdentitySeed: PromptIdentitySeed }', '      AcceptedAt: int }')
  assert.deepEqual(
    scanEntries([{ file: AUTHORITY_FACTS, text: seedless }]),
    [
      violation(
        AUTHORITY_FACTS,
        2,
        'authority-identity-seed',
        'AuthorityRootAcceptedPayload must store IdentitySeed',
      ),
    ],
  )
})

test('WHAT[PID-002] AuthorityRootAcceptedPayload cannot flatten IdentitySeed fields', () => {
  const duplicated = authorityFacts.replace(
    '      IdentitySeed: PromptIdentitySeed }',
    '      IdentitySeed: PromptIdentitySeed\n      SelectedAgent: AgentId }',
  )
  assert.deepEqual(
    scanEntries([{ file: AUTHORITY_FACTS, text: duplicated }]),
    [
      violation(
        AUTHORITY_FACTS,
        2,
        'flat-identity-duplicate',
        'AuthorityRootAcceptedPayload duplicates IdentitySeed fields: SelectedAgent',
      ),
    ],
  )
})

test('WHAT[PID-002] AuthorityExecutionProfile must derive identity from its seed', () => {
  const undivided = authorityModel.replace(
    '    member this.ParticipantIdentity = PromptIdentitySeed.participantIdentity this.StoredIdentitySeed',
    '    member this.SchemaVersion = 1',
  )
  assert.deepEqual(
    scanEntries([{ file: AUTHORITY_MODEL, text: undivided }]),
    [
      violation(
        AUTHORITY_MODEL,
        1,
        'authority-derived-identity',
        'AuthorityExecutionProfile must derive ParticipantIdentity from IdentitySeed',
      ),
    ],
  )
})

test('WHAT[PID-002] a SessionId-keyed identity collection is forbidden', () => {
  const file = 'src/Wanxiangshu/Interaction/Dispatch/Cache.fs'
  const source = [
    'namespace Wanxiangshu.Interaction.Dispatch',
    'open System.Collections.Generic',
    'let issued = Dictionary<SessionId, ParticipantIdentityEvidence>()',
  ].join('\n')
  assert.deepEqual(
    scanEntries([{ file, text: source }]),
    [
      violation(
        file,
        3,
        'session-identity-cache',
        'SessionId-keyed ParticipantIdentity/IdentitySeed collection is forbidden',
      ),
    ],
  )
})

test('WHAT[PID-002] a SessionId-keyed identity registry is forbidden', () => {
  const file = 'src/Wanxiangshu/Interaction/Dispatch/Registry.fs'
  const source = [
    'namespace Wanxiangshu.Interaction.Dispatch',
    'let identityRegistry (sessionId: SessionId) : ParticipantIdentity option = None',
  ].join('\n')
  assert.deepEqual(
    scanEntries([{ file, text: source }]),
    [
      violation(
        file,
        2,
        'session-identity-registry',
        'SessionId-keyed ParticipantIdentity/IdentitySeed registry is forbidden',
      ),
    ],
  )
})

test('WHAT[PID-002] a second identity fact owner is forbidden', () => {
  const file = 'src/Wanxiangshu/Interaction/Authority/Second.fs'
  const source = [
    'namespace Wanxiangshu.Interaction.Authority',
    'let fact = ParticipantIdentityEstablished.name',
  ].join('\n')
  assert.deepEqual(
    scanEntries([{ file, text: source }]),
    [
      violation(
        file,
        2,
        'duplicate-identity-fact',
        'ParticipantIdentityEstablished would create a second identity fact owner',
      ),
    ],
  )
})

test('WHAT[PID-002] the production participant identity boundary is clean', () => {
  assert.deepEqual(scanRepo(ROOT), [])
})
