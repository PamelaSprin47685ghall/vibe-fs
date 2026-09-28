import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import { runChatAdmissionCanary } from './support/run-opencode-chat-admission-canary.mjs'

const here = path.dirname(fileURLToPath(import.meta.url))

const fixture = JSON.parse(fs.readFileSync(path.join(here, '../fixtures/opencode-chat-admission-1.18.29.json'), 'utf8'))

const driftFixture = JSON.parse(fs.readFileSync(path.join(here, '../fixtures/opencode-chat-admission-drift.json'), 'utf8'))

const execute = promisify(execFile)

const assertPassingVersionEvidence = (versions) => {
  const supported = fixture.supportedVersionRange !== null
    && versions.opencode === fixture.supportedVersionRange
    && versions.plugin === fixture.supportedVersionRange
  if (!supported) {
    throw new Error(
      `OpenCode ${versions.opencode}/plugin ${versions.plugin} is outside the passing observed range: ${fixture.missingCapability ?? fixture.supportedVersionRange}`,
    )
  }
  assert.deepEqual(versions, fixture.observedVersions)
}

const first = (evidence, kind) => evidence.observations.find((observation) => observation.kind === kind)

test('WHAT[host-boundary-023] installed OpenCode chat admission public contract is observed and version-fenced', async (t) => {
  const stage = (name, verify) => async (value) => {
    let failure
    await t.test(`WHAT[host-boundary-023] ${name}`, async () => {
      try { await verify(value) } catch (error) { failure = error; throw error }
    })
    if (failure) throw failure
  }
  const evidence = await runChatAdmissionCanary({
    versions: stage('installed binary and plugin match the observed version', (versions) => {
      assert.deepEqual(versions, fixture.observedVersions)
      assert.throws(() => assertPassingVersionEvidence(driftFixture.versions), /outside the passing observed range/)
    }),
    ready: stage('one real Host is healthy and shares its supervising process group', async ({ sessionID, pid, health }) => {
      assert.equal(typeof sessionID, 'string')
      assert.ok(sessionID.length > 0)
      assert.equal(health.status, 200)
      assert.equal(health.data.healthy, true)
      assert.ok(Number.isInteger(pid) && pid > 0)
      if (process.platform !== 'win32') {
        const group = async (target) => Number((await execute('ps', ['-o', 'pgid=', '-p', String(target)])).stdout.trim())
        const hostGroup = await group(pid)
        assert.ok(hostGroup > 0)
        assert.equal(hostGroup, await group(process.pid))
        assert.notEqual(hostGroup, pid, 'Host must not escape into a detached group')
      }
    }),
    provider: stage('accepted input crosses the public hooks before the real provider request', (observations) => {
      const providerIndex = observations.findIndex(({ kind }) => kind === 'provider')
      assert.ok(providerIndex > 0)
      assert.deepEqual(observations.slice(0, providerIndex + 1)
        .map(({ kind }) => kind).filter((kind) => fixture.order.includes(kind)), fixture.order)
      const message = observations.find(({ kind }) => kind === 'chat.message').value
      assert.equal(message.input.messageID, '$accepted-message')
      assert.equal(message.output.sessionID, '$session')
    }),
    terminal: stage('the accepted assistant has matching public start and terminal evidence before idle', (observations) => {
      const assistant = observations.filter(({ kind, value }) => kind === 'message.updated'
        && value.info.role === 'assistant' && value.info.parentID === '$accepted-message')
      const started = assistant.find(({ value }) => value.info.id && value.info.created !== null)
      assert.ok(started)
      const ended = assistant.find(({ value }) => value.info.id === started.value.info.id && value.info.completed !== null)
      assert.ok(ended)
      assert.equal(ended.value.info.sessionID, '$session')
      assert.ok(observations.find(({ kind }) => kind === 'session.idle').sequence > ended.sequence)
    }),
    duplicate: stage('the duplicate input is redelivered with no additional transform or provider call at the observed checkpoint', (observed) => {
      assert.equal(observed.responseStatus, 204)
      assert.equal(observed.transformsAfter, observed.transformsBefore)
      assert.equal(observed.providerDeliveriesAfter, observed.providerDeliveriesBefore)
      assert.equal(observed.observations.filter(({ kind, value }) => kind === 'chat.message'
        && value.input.messageID === '$accepted-message').length, 2)
    }),
    rejection: stage('an accepted request reports the public hook rejection through session.error', ({ responseStatus, observations }) => {
      assert.equal(responseStatus, 204)
      assert.ok(observations.some(({ kind, value }) => kind === 'chat.message.rejection'
        && value.messageID === '$rejected-message'))
      const rejected = observations.find(({ kind }) => kind === 'session.error').value
      assert.deepEqual(rejected.properties, {
        error: { data: { message: '<string>' }, name: 'UnknownError' },
        sessionID: '<string>',
      })
    }),
  })
  assert.deepEqual(evidence.versions, fixture.observedVersions)
  assert.deepEqual(evidence.publicApis.hooks, fixture.publicHooks)

  const message = first(evidence, 'chat.message')
  assert.equal(message.value.input.sessionID, '$session')
  assert.equal(message.value.input.messageID, '$accepted-message')
  assert.equal(message.value.output.sessionID, '$session')
  assert.equal(message.value.output.messageID, '$accepted-message')
  assert.deepEqual(message.value.output.keys, fixture.chatMessageOutputKeys)
  assert.equal(message.value.output.keys.includes('error'), false, 'public chat.message output has no user-error channel')

  const providerIndex = evidence.observations.findIndex(({ kind }) => kind === 'provider')
  assert.ok(providerIndex > 0, 'real provider boundary was not reached')
  const observedOrder = evidence.observations
    .slice(0, providerIndex + 1)
    .map(({ kind }) => kind)
    .filter((kind) => fixture.order.includes(kind))
  assert.deepEqual(observedOrder, fixture.order)
  assert.deepEqual(observedOrder, fixture.supportedOrder)

  const transformed = first(evidence, 'experimental.chat.messages.transform').value
  assert.equal(transformed.messageID, '$accepted-message')
  assert.equal(transformed.sessionID, '$session')
  assert.equal(transformed.role, 'user')

  const assistantEvents = evidence.observations.filter(
    ({ kind, value }) => kind === 'message.updated'
      && value.info.role === 'assistant'
      && value.info.parentID === '$accepted-message',
  )
  assert.ok(assistantEvents.length > 0)
  const assistantStart = assistantEvents.find(({ value }) => value.info.id && value.info.created !== null)
  assert.ok(assistantStart, 'public event omitted exact assistant start evidence')
  assert.equal(assistantStart.value.info.sessionID, '$session')
  assert.equal(typeof assistantStart.value.info.id, 'string')
  assert.equal(assistantStart.value.info.timeKeys.includes('created'), true)
  const assistantTerminal = assistantEvents.find(({ value }) => value.info.id === assistantStart.value.info.id
    && value.info.completed !== null)
  assert.ok(assistantTerminal, 'public event omitted exact assistant terminal evidence')
  assert.equal(evidence.providerLifecycle.chatParamsDeliveries,
    evidence.observations.filter(({ kind }) => kind === 'chat.params').length)
  assert.equal(evidence.providerLifecycle.providerDeliveries,
    evidence.observations.filter(({ kind }) => kind === 'provider').length)
  assert.equal(evidence.providerLifecycle.assistantMessageUpdates,
    evidence.observations.filter(({ kind, value }) => kind === 'message.updated' && value.info.role === 'assistant').length)

  const params = first(evidence, 'chat.params').value
  assert.deepEqual(params.inputKeys, ['agent', 'message', 'model', 'provider', 'sessionID'])
  assert.equal(params.sessionID, '$session')
  assert.equal(params.messageID, '$accepted-message')
  assert.equal(params.messageSessionID, '$session')
  assert.equal(params.inputKeys.some((key) => /attempt|providerRun/i.test(key)), false)

  const idle = first(evidence, 'session.idle').value
  assert.deepEqual(idle.keys, ['id', 'properties', 'type'])
  assert.deepEqual(idle.propertyKeys, ['sessionID'])
  assert.deepEqual(idle.properties, { sessionID: '<string>' })

  assert.equal(evidence.rejection.responseStatus, 204, 'prompt_async accepts before the Hook Promise rejects')
  const rejection = first(evidence, 'session.error').value
  assert.deepEqual(rejection.keys, ['id', 'properties', 'type'])
  assert.deepEqual(rejection.propertyKeys, ['error', 'sessionID'])
  assert.deepEqual(rejection.properties, {
    error: { data: { message: '<string>' }, name: 'UnknownError' },
    sessionID: '<string>',
  })

  const acceptedDeliveries = evidence.observations.filter(
    ({ kind, value }) => kind === 'chat.message' && value.input.messageID === '$accepted-message',
  )
  assert.equal(acceptedDeliveries.length, 2, 'duplicate messageID is re-delivered to chat.message')
  assert.equal(evidence.duplicate.responseStatus, 204)
  assert.equal(evidence.duplicate.transformsAfter, evidence.duplicate.transformsBefore)
  assert.equal(evidence.duplicate.providerDeliveriesAfter, evidence.duplicate.providerDeliveriesBefore)
  assert.equal(evidence.providerLifecycle.providerDeliveries, evidence.duplicate.providerDeliveriesBefore)
  assert.equal(evidence.observations.filter(({ kind }) => kind === 'experimental.chat.messages.transform').length,
    evidence.duplicate.transformsBefore)

  if (fixture.supportedVersionRange === null) {
    assert.throws(
      () => assertPassingVersionEvidence(evidence.versions),
      /outside the passing observed range: exact public assistant message\.updated start and terminal evidence has not yet been captured/,
    )
  } else {
    assertPassingVersionEvidence(evidence.versions)
  }
  console.log(JSON.stringify({ opencodeChatAdmissionCanary: evidence }))
})

test.todo('WHAT[host-boundary-023] duplicate redelivery has a public completion witness and cannot cause a later transform or provider request (GAP-063)')
