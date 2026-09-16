import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as authority from '../../../dist/Interaction/Authority/RuntimeSurface.js'
import * as dispatch from '../../../dist/Interaction/Dispatch/DispatchSurface.js'
import * as journal from '../../../dist/Persistence/Journal/Surface.js'

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

test('WHAT[INTERACTION-AUTHORITY-006] HumanRoot missing identity seed is rejected without authority', async () => {
  await withJournal('human-missing', async (handle) => {
    const result = await dispatch.acceptHumanRootSelection(handle, 'ses-human-missing', 'msg-human-missing', null)

    assert.equal(result.ok, false)
    assert.equal(result.error.kind, 'IdentityRejected')
    assert.match(result.error.reason, /explicit root-selection identity seed/i)
    assert.equal(dispatch.projectionObservation(handle, 'ses-human-missing').activeLogicalRun, null)
  })
})

test('WHAT[INTERACTION-AUTHORITY-006] IA_006_canonical_names_resolve_and_legacy_or_malformed_are_refused', () => {
  for (const name of ['coder', 'manager', 'inspector']) {
    const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', 'msg_u1', rootSelection(name))
    assert.equal(result.ok, true, result.error)
    assert.equal(authority.parseAgentName(name).ok, true)
  }
  for (const name of ['build', 'plan', 'student', 'teacher', 'meditator', 'executor', 'fast_coder']) {
    assert.equal(authority.parseAgentName(name).error.kind, 'LegacyAgentName')
    const result = authority.createAuthorityRoot(hash, 'rt_1', 'ses_a', 'HumanRoot', 'msg_u1', rootSelection(name))
    assert.equal(result.ok, false)
  }
  assert.equal(authority.parseAgentName('nonsense').error.kind, 'UnknownManagedAgent')
  assert.equal(authority.parseAgentName('fast-').error.kind, 'Malformed')
  assert.equal(authority.parseAgentName('fast-coder').error.kind, 'Malformed')
  assert.equal(authority.parseAgentName('Coder').error.kind, 'Malformed')
})

test('WHAT[INTERACTION-AUTHORITY-006] IA_006_agent_owner_root_claim_rejects_legacy_name', () => {
  const inherited = authority.issueInheritedIdentitySeed('build', rootFor('manager'))
  assert.equal(inherited.ok, false)
  assert.match(inherited.error, /legacy|managed|malformed/i)
})
