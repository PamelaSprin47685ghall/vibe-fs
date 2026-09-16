import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import * as chatExecution from '../../../dist/Execution/Session/ChatExecution/Surface.js'
import * as intent from '../../../dist/OpenCode/Host/ChatAdmission/IntentSurface.js'
import { acceptManagedChat, providerStarted } from './support/chat-wire.mjs'

const fixture = readFileSync(
  new URL('./fixtures/chat-execution-v1.json', import.meta.url),
  'utf8',
).trim()

const acceptedPayload = (wire) => wire[1][1][1]

test('WHAT[CHATEXEC-011] external and plugin roots share AcceptManagedChatIntent', () => {
  const external = intent.resolve(
    { sessionId: 'ses-chat', physicalUserMessageId: 'msg-chat', explicitAgent: 'coder' },
    { available: true, activeParticipant: null, activeKind: null, claims: [], acceptedContinuations: [] },
  )
  const prompt = intent.resolve(
    { sessionId: 'ses-chat', physicalUserMessageId: 'msg-chat', promptKey: 'prompt-1' },
    { available: true, activeParticipant: null, activeKind: null, claims: [{ promptKey: 'prompt-1', sessionId: 'ses-chat', origin: 'InteractionRepair', participant: 'coder' }], acceptedContinuations: [] },
  )
  assert.equal(external.case, 'ExternalRootIntent')
  assert.equal(prompt.case, 'PendingPromptIntent')
  assert.equal(external.participant, 'coder')
  assert.equal(prompt.participant, 'coder')
  assert.equal(external.identitySeed, 'RootSelection')
  assert.equal(prompt.identitySeed, 'RootSelection')
})

test('WHAT[CHATEXEC-011] malformed exact identity seed is rejected by the production codec', () => {
  const malformed = JSON.parse(fixture)
  acceptedPayload(malformed).Evidence.IdentitySeed[0] = 'ForgedSeed'
  const result = chatExecution.canonicalize(JSON.stringify(malformed))
  assert.equal(result.ok, false)
})

test('WHAT[CHATEXEC-011] exact physical provider run and evidence are frozen', () => {
  const key = { sessionId: 'ses-11-1', physicalUserMessageId: 'msg-11-1' }
  const accepted = acceptManagedChat('run-11-1', 'msg-root-11-1', 'HumanRoot', {
    kind: 'RootSelection',
    ownerSession: null,
    ownerLogicalRun: null,
    ownerAuthorityRoot: null,
    participantIdentity: { origin: 'ResolvedAtRoot', participant: 'coder', persona: 'Coder', personaCatalogVersion: 1, role: 'coder' },
  }, key, 'work-main')
  const start = providerStarted(key, 'run-11-1-p', 'use-committed-epoch', 'work-main')
  const folded = chatExecution.fold([accepted, start])
  assert.equal(folded.value[0].identity.providerRun, 'run-11-1-p')
})
