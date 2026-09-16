import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'
import * as factCodec from '../../../dist/Persistence/Journal/FactCodecSurface.js'
import * as journalCodec from '../../../dist/Persistence/Journal/JournalCodecSurface.js'
import * as tp from '../../../dist/OpenCode/Host/TerminalPolicySurface.js'
import * as roles from '../../../dist/Foundation/RolesSurface.js'

const hash = (value) => `H(${value})`
const personas = {
  coder: 'Coder',
  manager: 'Lead',
  reviewer: 'Auditor',
  inspector: 'Investigator',
  devops: 'Operator',
}
const rootSelection = (agent) => {
  const role = agent === 'predictor' ? 'inspector' : agent
  return {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant: agent,
      role,
      selectedTier: 'deep',
      persona: personas[agent] ?? 'Unknown',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }
}
const rootFor = (agent = 'coder', physical = 'msg_u1') => {
  const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', physical, rootSelection(agent))
  assert.equal(result.ok, true, result.error)
  return result.value
}

const profile = (value) => ({
  session: value.session,
  logicalRun: value.logicalRun,
  authorityRoot: value.authorityRoot,
  authorityKind: value.authorityKind,
  participant: value.participantIdentity.participant,
  role: value.participantIdentity.role,
})

const register = (root) => authority.registerAuthority(root, authority.empty)
const continuation = (key, root, kind = 'ManagerGuard', payload = 'payload') =>
  authority.claimContinuation(key, 'ses_a', kind, root, payload)

const managerRootSelection = {
  kind: 'RootSelection',
  ownerSession: null,
  ownerLogicalRun: null,
  ownerAuthorityRoot: null,
  participantIdentity: {
    participant: 'manager',
    role: 'manager',
    selectedTier: 'deep',
    persona: 'Lead',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  },
}

const hostPort = (sendPrompt) => ({
  SubscribeTerminal: () => ({ Dispose: () => {} }),
  SendPrompt: sendPrompt,
})

const withJournal = async (label, action) => {
  const directory = mkdtempSync(join(tmpdir(), `wxs-authority-acceptance-${label}-`))
  const opened = await journal.JournalSurface_bootWithWriterId(
    directory,
    `writer-${label}`,
    `runtime-${label}`,
    4242,
    '2026-08-30T00:00:00Z',
  )
  assert.equal(opened.ok, true, opened.ok ? '' : JSON.stringify(opened.error))

  try {
    await action(opened.journal)
  } finally {
    journal.JournalSurface_dispose(opened.journal)
    rmSync(directory, { recursive: true, force: true })
  }
}

const acceptOwner = async (handle, session = 'ses-owner') => {
  const accepted = await dispatch.acceptHumanRootSelection(
    handle,
    session,
    `msg-${session}`,
    managerRootSelection,
  )
  assert.equal(accepted.ok, true, accepted.ok ? '' : accepted.error)
  return accepted.profile
}

const inheritedSeed = (owner, child = 'coder') => {
  const issued = authority.issueInheritedIdentitySeed(child, owner)
  assert.equal(issued.ok, true, issued.ok ? '' : issued.error)
  return issued.value
}

// Fixture and helpers from authority-root-identity-upgrade
const legacyHumanRoot = readFileSync(
  new URL('./fixtures/authority-root-v1.json', import.meta.url),
  'utf8',
).trim()

const legacyAgentBytes = /"SelectedAgent"|"PeerAgent"/

const currentPayload = Object.freeze({
  SchemaVersion: 2,
  SessionId: 'ses-authority-codec',
  LogicalRunId: 'run-authority-codec',
  AuthorityRootUserMessageId: 'msg-authority-codec',
  AuthorityKind: 'HumanRoot',
  IdentitySeed: {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: {
      participant: 'coder',
      role: 'coder',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  },
  TargetRole: null,
  ExpectedClosureKind: 'HumanRootManagedRun',
})

const inheritedPayload = Object.freeze({
  ...currentPayload,
  AuthorityKind: 'AgentOwnerRoot',
  IdentitySeed: {
    kind: 'InheritedFromOwner',
    ownerSession: 'ses-owner',
    ownerLogicalRun: 'run-owner',
    ownerAuthorityRoot: 'msg-owner',
    participantIdentity: {
      participant: 'coder',
      role: 'coder',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'InheritedFromOwner',
    },
  },
})

const currentFact = (payload = currentPayload) => ({
  case: 'AuthorityRootAccepted',
  payload,
})

const envelope = (fact = currentFact()) => ({
  runtime: 'rt-authority-codec',
  seq: 1,
  observedAt: '2026-01-02T03:04:05Z',
  id: 'a'.repeat(32),
  stream: { kind: 'Session', id: currentPayload.SessionId },
  providerRun: null,
  fact,
})

const authorityCase = (value) => {
  if (Array.isArray(value)) {
    if (value[0] === 'AuthorityRootAccepted' && value[1] && typeof value[1] === 'object') return value
    for (const item of value) {
      const found = authorityCase(item)
      if (found) return found
    }
  } else if (value && typeof value === 'object') {
    for (const item of Object.values(value)) {
      const found = authorityCase(item)
      if (found) return found
    }
  }
  return null
}

const replacePayload = (line, change) => {
  const fact = JSON.parse(line)
  const taggedCase = authorityCase(fact)
  assert.notEqual(taggedCase, null, 'AuthorityRootAccepted case must be present')
  change(taggedCase[1])
  return JSON.stringify(fact)
}

const identityInSeed = (seed) => seed[0] === 'RootSelection' ? seed[1] : seed[1].ParticipantIdentity

const envelopeLineForRawFact = (line) => {
  const value = JSON.parse(journalCodec.serialize(envelope()))
  value.Fact = JSON.parse(line)
  return JSON.stringify(value)
}

const eventForRawFact = (line) => {
  const event = journalCodec.encode([], [], envelope())
  event.payload.Fact = JSON.parse(line)
  return event
}

const assertDecodeErrorAcrossReplayRoutes = (line, expected) => {
  const factResult = factCodec.decode(line)
  assert.equal(factResult.ok, false)
  assert.match(factResult.error, expected)

  const envelopeResult = journalCodec.deserialize(envelopeLineForRawFact(line))
  assert.equal(envelopeResult.ok, false)
  assert.match(envelopeResult.error, expected)

  const eventResult = journalCodec.decode(eventForRawFact(line))
  assert.equal(eventResult.ok, false)
  assert.match(eventResult.error, expected)
}

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_malformed_profile_role_and_root_kind_fail_closed_tier_is_compat', () => {
  const root = rootFor()
  const identity = root.identitySeed.participantIdentity
  const malformed = [
    [{ ...root, identitySeed: { ...root.identitySeed, participantIdentity: { ...identity, role: 'unknown' } } }, /unknown role/],
    [{ ...root, authorityKind: 'unknown' }, /unknown authority root kind/],
  ]
  // selectedTier is accepted on input but is not part of the fixed authority view: it is dropped, never stored.
  const tierCompat = { ...root, identitySeed: { ...root.identitySeed, participantIdentity: { ...identity, selectedTier: 'unknown' } } }
  const tierResult = authority.registerAuthority(tierCompat, authority.empty)
  assert.equal(Object.hasOwn(tierResult.activeLogicalRun.participantIdentity, 'selectedTier'), false)
  assert.deepEqual(profile(tierResult.activeLogicalRun), profile(root))
  for (const [candidate, expected] of malformed) {
    const result = authority.registerAuthority(candidate, authority.empty)
    assert.equal(result.ok, false)
    assert.match(result.error, expected)
  }
})

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_root_carries_resolved_participant_identity', () => {
  assert.deepEqual(profile(rootFor('coder')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    participant: 'coder',
    role: 'coder',
  })
  assert.deepEqual(profile(rootFor('manager')), {
    session: 'ses_a',
    logicalRun: 'H(rt_1\nses_a\nmsg_u1)',
    authorityRoot: 'msg_u1',
    authorityKind: 'HumanRoot',
    participant: 'manager',
    role: 'manager',
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] IA_003_root_remains_the_source_for_continuations', () => {
  const root = rootFor()
  const state = authority.registerClaim(continuation('pk_c', root, 'BusyAgentNudge', 'pd-n'), register(root))
  assert.deepEqual(profile(state.activeLogicalRun), profile(root))
  assert.deepEqual(profile(state.lastAuthorityProfile), profile(root))
})

test('WHAT[INTERACTION-AUTHORITY-003] valid authority profiles carry one atomic participant identity', () => {
  const coderProfile = authority.profileFromResolved('ses-1', 'run-1', 'msg-1', 'HumanRoot', 'coder')
  assert.equal(coderProfile.ok, true)
  assert.deepEqual(coderProfile.value, {
    session: 'ses-1',
    logicalRun: 'run-1',
    authorityRoot: 'msg-1',
    authorityKind: 'HumanRoot',
    participantIdentity: {
      participant: 'coder',
      role: 'coder',
      persona: 'Coder',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  })

  const managerProfile = authority.profileFromResolved('ses-2', 'run-2', 'msg-2', 'AgentOwnerRoot', 'manager')
  assert.equal(managerProfile.ok, true)
  assert.deepEqual(managerProfile.value, {
    session: 'ses-2',
    logicalRun: 'run-2',
    authorityRoot: 'msg-2',
    authorityKind: 'AgentOwnerRoot',
    participantIdentity: {
      participant: 'manager',
      role: 'manager',
      persona: 'Lead',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] rejects hand-built mismatched profile', () => {
  const mismatched = {
    session: 'ses-1',
    logicalRun: 'run-1',
    authorityRoot: 'msg-1',
    authorityKind: 'HumanRoot',
    participantIdentity: {
      participant: 'coder',
      role: 'manager',
      persona: 'Lead',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }

  const result = authority.validateAuthorityProfile(mismatched)
  assert.equal(result.ok, false)
  assert.match(result.error, /participant identity mismatch/i)
})

test('WHAT[INTERACTION-AUTHORITY-003] Bookkeeper cannot enter a public authority profile', () => {
  const bookkeeper = {
    session: 'ses-1',
    logicalRun: 'run-1',
    authorityRoot: 'msg-1',
    authorityKind: 'HumanRoot',
    participantIdentity: {
      participant: 'bookkeeper',
      role: 'bookkeeper',
      persona: 'Bookkeeper',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    },
  }

  const result = authority.validateAuthorityProfile(bookkeeper)
  assert.equal(result.ok, false)
  assert.match(result.error, /not an admitted public participant/i)
})

test('WHAT[INTERACTION-AUTHORITY-003] current schema-v2 authority bytes round-trip canonically', () => {
  const factLine = factCodec.encode(currentFact())
  const decodedFact = factCodec.decode(factLine)
  assert.equal(decodedFact.ok, true, decodedFact.ok ? '' : decodedFact.error)
  assert.equal(decodedFact.case, 'AuthorityRootAccepted')
  assert.deepEqual(decodedFact.payload, currentPayload)
  assert.equal(Object.hasOwn(decodedFact.payload, 'ParticipantIdentity'), false)
  assert.equal(decodedFact.line, factLine)

  const envelopeLine = journalCodec.serialize(envelope())
  const decodedEnvelope = journalCodec.deserialize(envelopeLine)
  assert.equal(decodedEnvelope.ok, true, decodedEnvelope.ok ? '' : decodedEnvelope.error)
  assert.deepEqual(decodedEnvelope.value.fact, currentFact())
  assert.equal(journalCodec.serialize(decodedEnvelope.value), envelopeLine)

  const event = journalCodec.encode([], [], envelope())
  const decodedEvent = journalCodec.decode(event)
  assert.equal(decodedEvent.ok, true, decodedEvent.ok ? '' : decodedEvent.error)
  assert.deepEqual(decodedEvent.value.fact, currentFact())
  assert.equal(decodedEvent.value.line, envelopeLine)
  for (const line of [factLine, envelopeLine, decodedEvent.value.line]) {
    assert.equal(legacyAgentBytes.test(line), false)
  }
})

test('WHAT[INTERACTION-AUTHORITY-003] current AgentOwnerRoot retains exact inherited owner provenance', () => {
  const line = factCodec.encode(currentFact(inheritedPayload))
  const decoded = factCodec.decode(line)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.deepEqual(decoded.payload.IdentitySeed, inheritedPayload.IdentitySeed)
})

test('WHAT[INTERACTION-AUTHORITY-003] repeated legacy HumanRoot replay upgrades to identical schema-v2 identity', () => {
  const factFirst = factCodec.decode(legacyHumanRoot)
  const factSecond = factCodec.decode(legacyHumanRoot)
  assert.equal(factFirst.ok, true, factFirst.ok ? '' : factFirst.error)
  assert.equal(factSecond.ok, true, factSecond.ok ? '' : factSecond.error)
  assert.equal(factFirst.payload.SchemaVersion, 2)
  assert.equal(factFirst.payload.IdentitySeed.kind, 'RootSelection')
  assert.deepEqual(factSecond.payload.IdentitySeed, factFirst.payload.IdentitySeed)

  const envelopeLine = envelopeLineForRawFact(legacyHumanRoot)
  const envelopeFirst = journalCodec.deserialize(envelopeLine)
  const envelopeSecond = journalCodec.deserialize(envelopeLine)
  assert.equal(envelopeFirst.ok, true, envelopeFirst.ok ? '' : envelopeFirst.error)
  assert.equal(envelopeSecond.ok, true, envelopeSecond.ok ? '' : envelopeSecond.error)
  assert.deepEqual(envelopeFirst.value.fact.payload.IdentitySeed, factFirst.payload.IdentitySeed)
  assert.deepEqual(envelopeSecond.value.fact.payload.IdentitySeed, factFirst.payload.IdentitySeed)

  const event = eventForRawFact(legacyHumanRoot)
  const eventFirst = journalCodec.decode(event)
  const eventSecond = journalCodec.decode(event)
  assert.equal(eventFirst.ok, true, eventFirst.ok ? '' : eventFirst.error)
  assert.equal(eventSecond.ok, true, eventSecond.ok ? '' : eventSecond.error)
  assert.deepEqual(eventFirst.value.fact.payload.IdentitySeed, factFirst.payload.IdentitySeed)
  assert.deepEqual(eventSecond.value.fact.payload.IdentitySeed, factFirst.payload.IdentitySeed)
})

test('WHAT[INTERACTION-AUTHORITY-003] unknown authority schema fails closed precisely', () => {
  assert.throws(
    () => factCodec.encode(currentFact({ ...currentPayload, SchemaVersion: 1 })),
    /AuthorityRootAccepted encoder requires SchemaVersion 2, got 1/,
  )

  const unknown = replacePayload(factCodec.encode(currentFact()), (payload) => {
    payload.SchemaVersion = 99
  })
  assertDecodeErrorAcrossReplayRoutes(unknown, /AuthorityRootAccepted schema version is unsupported: 99/)
})

test('WHAT[INTERACTION-AUTHORITY-003] malformed legacy identity fails closed at the missing field', () => {
  const malformed = replacePayload(legacyHumanRoot, (payload) => {
    delete payload.SelectedAgent
  })
  assertDecodeErrorAcrossReplayRoutes(malformed, /SelectedAgent/)
})

test('WHAT[INTERACTION-AUTHORITY-003] stale legacy peer fields normalize to canonical identity', () => {
  const stale = replacePayload(legacyHumanRoot, (payload) => {
    const legacyPeerField = Object.keys(payload).find((key) => key === `Peer${'Agent'}`)
    assert.notEqual(legacyPeerField, undefined, 'v1 fixture must carry the legacy peer field')
    payload[legacyPeerField] = 'reviewer'
    payload.SelectedTier = 'fast'
  })
  const decoded = factCodec.decode(stale)
  assert.equal(decoded.ok, true, decoded.ok ? '' : decoded.error)
  assert.deepEqual(decoded.payload.IdentitySeed.participantIdentity, {
    participant: 'coder',
    role: 'coder',
    persona: 'Coder',
    personaCatalogVersion: 1,
    origin: 'ResolvedAtRoot',
  })
  assert.equal(legacyAgentBytes.test(decoded.line), false)
})

test('WHAT[INTERACTION-AUTHORITY-003] malformed schema-v2 identity fails closed at the missing field', () => {
  const malformed = replacePayload(factCodec.encode(currentFact()), (payload) => {
    delete identityInSeed(payload.IdentitySeed).Persona
  })
  assertDecodeErrorAcrossReplayRoutes(malformed, /Persona/)
})

test('WHAT[INTERACTION-AUTHORITY-003] mismatched schema-v2 identity fails closed precisely', () => {
  const mismatch = replacePayload(factCodec.encode(currentFact()), (payload) => {
    identityInSeed(payload.IdentitySeed).Persona = 'Engineer'
  })
  assertDecodeErrorAcrossReplayRoutes(
    mismatch,
    /participant identity Persona mismatch: expected Coder, got Engineer/,
  )
})

test('WHAT[INTERACTION-AUTHORITY-003] rejects unprovable historical identity', () => {
  const agentOwner = replacePayload(legacyHumanRoot, (payload) => {
    payload.AuthorityKind = 'AgentOwnerRoot'
  })
  assertDecodeErrorAcrossReplayRoutes(
    agentOwner,
    /legacy AuthorityRootAccepted v1 AgentOwnerRoot cannot prove participant identity/,
  )
})

test('WHAT[INTERACTION-AUTHORITY-003] participant identity is not a second live fact case', () => {
  const invented = JSON.parse(legacyHumanRoot)
  const taggedCase = authorityCase(invented)
  assert.notEqual(taggedCase, null)
  taggedCase[0] = 'ParticipantIdentityInstalled'
  assertDecodeErrorAcrossReplayRoutes(JSON.stringify(invented), /ParticipantIdentityInstalled/)
})

test('WHAT[INTERACTION-AUTHORITY-003] HumanRoot persists identity before provider work and returns the exact profile', async () => {
  await withJournal('human-explicit', async (handle) => {
    const result = await dispatch.acceptHumanRootSelection(handle, 'ses-human-explicit', 'msg-human-explicit', managerRootSelection)
    const projection = dispatch.projectionObservation(handle, 'ses-human-explicit')

    assert.equal(result.ok, true, result.ok ? '' : result.error)
    assert.equal(result.profile.identitySeed.kind, 'RootSelection')
    assert.deepEqual(result.profile.identitySeed.participantIdentity, {
      participant: 'manager',
      role: 'manager',
      persona: 'Lead',
      personaCatalogVersion: 1,
      origin: 'ResolvedAtRoot',
    })
    assert.deepEqual(projection.activeLogicalRun, result.profile)
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] physical receipt installs exact AgentOwnerRoot authority after its claim', async () => {
  await withJournal('physical-acceptance', async (handle) => {
    const owner = await acceptOwner(handle)
    const seed = inheritedSeed(owner, 'coder')
    const order = []

    const result = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => {
        const beforeReceipt = dispatch.projectionObservation(handle, 'ses-physical-child')
        order.push({
          phase: 'HostSend',
          pendingClaims: beforeReceipt.pendingClaims.length,
          authorityAccepted: Number(beforeReceipt.activeLogicalRun !== null),
        })
        return dispatch.admittedWithPhysicalMessage('msg-physical-child')
      }),
      handle,
      'ses-physical-child',
      'physical authority root',
      seed,
    )

    const afterReceipt = dispatch.projectionObservation(handle, 'ses-physical-child')
    order.push({
      phase: 'Returned',
      pendingClaims: afterReceipt.pendingClaims.length,
      authorityAccepted: Number(afterReceipt.activeLogicalRun !== null),
    })

    assert.equal(result.ok, true, result.ok ? '' : result.error)
    assert.deepEqual(order, [
      { phase: 'HostSend', pendingClaims: 1, authorityAccepted: 0 },
      { phase: 'Returned', pendingClaims: 0, authorityAccepted: 1 },
    ])
    assert.equal(afterReceipt.activeLogicalRun.authorityRoot, 'msg-physical-child')
    assert.deepEqual(afterReceipt.activeLogicalRun.identitySeed, seed)
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] rejected or unknown physical send outcome leaves no authority', async () => {
  await withJournal('unaccepted-send', async (handle) => {
    const owner = await acceptOwner(handle)
    const rejectedSeed = inheritedSeed(owner, 'coder')
    const unknownSeed = inheritedSeed(owner, 'inspector')

    const rejected = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => dispatch.fatal('provider rejected')),
      handle,
      'ses-rejected-child',
      'rejected child',
      rejectedSeed,
    )
    const unknown = await dispatch.sendAgentOwnerRootAwait(
      hostPort(async () => dispatch.acceptanceUnknown('physical result unavailable')),
      handle,
      'ses-unknown-child',
      'unknown child',
      unknownSeed,
    )

    assert.equal(rejected.ok, false)
    assert.match(rejected.error, /provider rejected/)
    assert.equal(unknown.ok, false)
    assert.match(unknown.error, /acceptance unknown/i)
    assert.equal(dispatch.projectionObservation(handle, 'ses-rejected-child').activeLogicalRun, null)
    assert.equal(dispatch.projectionObservation(handle, 'ses-rejected-child').pendingClaims.length, 0)
    assert.equal(dispatch.projectionObservation(handle, 'ses-unknown-child').activeLogicalRun, null)
    assert.equal(dispatch.projectionObservation(handle, 'ses-unknown-child').pendingClaims.length, 1)
  })
})

test('WHAT[INTERACTION-AUTHORITY-003] TPOL_top_level_manager_has_fail_closed_parent_rules_table_driven', () => {
  assert.equal(tp.sessionDeadWithoutJournal('ses-root-manager'), false)
  assert.ok(roles.allRoleLabels.includes('manager'))
  assert.ok(roles.allRoleLabels.includes('orchestrator'))
  assert.ok(roles.allRoleLabels.includes('coder'))
  assert.equal(tp.outstandingWithoutJournal('Manager', false, 'ses-root-manager'), false)
  assert.equal(tp.outstandingWithoutJournal('Coder', true, 'ses-coder'), false)
})
