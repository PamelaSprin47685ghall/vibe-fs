import { createHash } from 'node:crypto'
import { readFile } from 'node:fs/promises'
import { pathToFileURL } from 'node:url'

const SCHEMA_VERSION = 1
const KIND = 'managed-chat-incident-evidence'

const keysExactly = (value, expected, label) => {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) throw new TypeError(`missing ${label}`)
  const actual = Object.keys(value)
  for (const field of actual) if (!expected.includes(field)) throw new TypeError(`unknown ${label} field '${field}'`)
  for (const field of expected) if (!(field in value)) throw new TypeError(`missing ${label} field '${field}'`)
}
const requiredText = (value, label) => {
  if (typeof value !== 'string' || value.trim() === '') throw new TypeError(`missing ${label}`)
  return value
}
const canonicalValue = (value) => {
  if (Array.isArray(value)) return value.map(canonicalValue)
  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((key) => [key, canonicalValue(value[key])]))
  }
  return value
}
const canonicalJson = (value) => JSON.stringify(canonicalValue(value))
const digestOf = (body) => createHash('sha256').update(canonicalJson(body)).digest('hex')
const deepFreeze = (value) => {
  if (value !== null && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.freeze(value)
    for (const child of Object.values(value)) deepFreeze(child)
  }
  return value
}
const withoutIntegrity = (evidence) => Object.fromEntries(Object.entries(evidence).filter(([key]) => key !== 'integrity'))

const validateCapacity = (snapshot) => {
  keysExactly(snapshot, [
    'ledgerEntries', 'tokens', 'custodies', 'executions', 'waiters', 'owners', 'lineage',
    'tokenStateCounts', 'activeCount', 'counters',
  ], 'capacity snapshot')
  for (const field of ['ledgerEntries', 'tokens', 'custodies', 'executions', 'waiters', 'owners', 'lineage']) {
    if (!Array.isArray(snapshot[field])) throw new TypeError(`missing capacity snapshot field '${field}'`)
  }
  keysExactly(snapshot.tokenStateCounts, ['idle', 'inFlight', 'retiring'], 'capacity tokenStateCounts')
  keysExactly(snapshot.counters, ['duplicate', 'stale', 'conflict'], 'capacity counters')
  const target = (value) => keysExactly(value, ['model', 'reasoning'], 'capacity target')
  const owner = (value) => keysExactly(value, ['sessionId', 'physicalUserMessageId', 'effectiveAgent'], 'capacity owner')
  for (const entry of snapshot.ledgerEntries) {
    keysExactly(entry, ['credit', 'target'], 'capacity ledger entry')
    target(entry.target)
  }
  for (const token of snapshot.tokens) {
    keysExactly(token, ['credit', 'state', 'owner', 'target'], 'capacity token')
    owner(token.owner)
    target(token.target)
  }
  for (const custody of snapshot.custodies) {
    keysExactly(custody, ['credit', 'owner'], 'capacity custody')
    owner(custody.owner)
  }
  for (const entry of [...snapshot.executions, ...snapshot.owners]) owner(entry)
  for (const waiter of snapshot.waiters) keysExactly(waiter, ['sessionId', 'physicalUserMessageId', 'effectiveAgent', 'sequence', 'kind'], 'capacity waiter')
  for (const edge of snapshot.lineage) keysExactly(edge, ['parentSessionId', 'childSessionId'], 'capacity lineage')
}

const RECOVERY_OBSERVATIONS = {
  CrashAfterAcceptance: ['ProviderAbsent', 'ResourceAbsent', 'NotCommitted', 'NoFailureDecision'],
  ProviderAlive: ['ProviderAlive', 'ResourceAbsent', 'NotCommitted', 'NoFailureDecision'],
  ProviderTerminalCompleted: ['ProviderTerminalCompleted', 'ResourceAbsent', 'NotCommitted', 'NoFailureDecision'],
  TerminalResourceHeld: ['ProviderTerminalCompleted', 'ResourceHeld', 'Committed', 'NoFailureDecision'],
  MissingReceipt: ['ReceiptMissing', 'ResourceAbsent', 'NotCommitted', 'NoFailureDecision'],
  PersistenceUnknown: ['ProviderAbsent', 'ResourceAbsent', 'Unknown', 'NoFailureDecision'],
  StaleKey: ['StaleKey', 'ResourceAbsent', 'NotCommitted', 'NoFailureDecision'],
  StaleProvider: ['StaleProvider', 'ResourceAbsent', 'NotCommitted', 'NoFailureDecision'],
}
const validateRecoveryObservation = (recovery) => {
  keysExactly(recovery, [
    'scenario', 'providerObservation', 'resourceObservation', 'persistenceCommitment', 'failurePolicy',
  ], 'recovery observation')
  const scenario = requiredText(recovery.scenario, 'recovery scenario')
  const expected = RECOVERY_OBSERVATIONS[scenario]
  const observed = [recovery.providerObservation, recovery.resourceObservation, recovery.persistenceCommitment, recovery.failurePolicy]
  if (!expected || observed.some((value, index) => value !== expected[index])) {
    throw new TypeError(`recovery observation does not match scenario '${scenario}'`)
  }
}

const projectHostContract = (host) => {
  keysExactly(host, [
    'schemaVersion', 'supportedVersionRange', 'observedVersions', 'observedResult', 'missingCapability',
    'publicHooks', 'order', 'supportedOrder', 'chatMessageOutputKeys', 'terminalEvents', 'duplicateDelivery',
  ], 'Host contract')
  keysExactly(host.observedVersions, ['opencode', 'plugin'], 'Host observedVersions')
  if (host.schemaVersion !== 1 || host.observedResult !== 'supported' || typeof host.supportedVersionRange !== 'string'
      || host.observedVersions.opencode !== host.supportedVersionRange
      || host.observedVersions.plugin !== host.supportedVersionRange) {
    throw new TypeError('Host contract is not in an exact supported version')
  }
  return {
    schemaVersion: host.schemaVersion,
    supportedVersion: host.supportedVersionRange,
    observedVersions: host.observedVersions,
    publicHooks: host.publicHooks,
    supportedOrder: host.supportedOrder,
    terminalEvents: host.terminalEvents,
    duplicateDelivery: host.duplicateDelivery,
    acceptedMessageReplayCapability: 'Absent',
  }
}

const actionOf = (runtime) => runtime.decisions.map((decision, index) => {
  const effect = runtime.effects[index] ?? decision
  return {
    owner: effect.includes('ReleaseTerminalResource') ? 'execution-model-routing' : 'managed-chat-execution',
    action: decision,
    authority: 'EffectRequestOnly',
  }
})

const validateEnvelopeShape = (evidence) => {
  keysExactly(evidence, ['schemaVersion', 'kind', 'execution', 'capacity', 'diagnostics', 'hostContract', 'recovery', 'integrity'], 'incident evidence')
  if (evidence.schemaVersion !== SCHEMA_VERSION) throw new TypeError(`unsupported incident evidence schema version '${evidence.schemaVersion}'`)
  if (evidence.kind !== KIND) throw new TypeError(`unknown incident evidence kind '${evidence.kind}'`)
  keysExactly(evidence.execution, ['key', 'facts', 'projection', 'status'], 'execution evidence')
  keysExactly(evidence.execution.key, ['sessionId', 'physicalUserMessageId'], 'execution key')
  keysExactly(evidence.capacity, ['snapshot', 'reconciliation'], 'capacity evidence')
  validateCapacity(evidence.capacity.snapshot)
  keysExactly(evidence.hostContract, [
    'schemaVersion', 'supportedVersion', 'observedVersions', 'publicHooks', 'supportedOrder',
    'terminalEvents', 'duplicateDelivery', 'acceptedMessageReplayCapability',
  ], 'Host evidence')
  keysExactly(evidence.hostContract.observedVersions, ['opencode', 'plugin'], 'Host observedVersions')
  keysExactly(evidence.hostContract.terminalEvents, ['providerLifecycle', 'success', 'rejection'], 'Host terminalEvents')
  keysExactly(evidence.recovery, ['observation', 'runtime'], 'recovery evidence')
  validateRecoveryObservation(evidence.recovery.observation)
  keysExactly(evidence.recovery.runtime, ['decisions', 'effects'], 'recovery runtime evidence')
  if (!Array.isArray(evidence.execution.facts) || evidence.execution.facts.length === 0
      || !Array.isArray(evidence.execution.projection)
      || !Array.isArray(evidence.diagnostics) || evidence.diagnostics.length === 0
      || !Array.isArray(evidence.recovery.runtime.decisions)
      || !Array.isArray(evidence.recovery.runtime.effects)) throw new TypeError('missing incident evidence collection')
  keysExactly(evidence.integrity, ['algorithm', 'digest'], 'incident evidence integrity')
  if (evidence.integrity.algorithm !== 'sha256') throw new TypeError(`unsupported incident evidence integrity '${evidence.integrity.algorithm}'`)
}

export const captureEvidence = async (input, surfaces) => {
  keysExactly(input, ['facts', 'key', 'capacitySnapshot', 'diagnostics', 'hostContract', 'recovery'], 'incident capture')
  keysExactly(input.key, ['sessionId', 'physicalUserMessageId'], 'execution key')
  validateCapacity(input.capacitySnapshot)
  if (!Array.isArray(input.facts) || input.facts.length === 0) throw new TypeError("missing incident capture field 'facts'")
  if (!Array.isArray(input.diagnostics) || input.diagnostics.length === 0) throw new TypeError("missing incident capture field 'diagnostics'")
  validateRecoveryObservation(input.recovery)

  const facts = input.facts.map((fact) => {
    const canonical = surfaces.canonicalize(fact)
    if (!canonical.ok) throw new TypeError(`invalid canonical ChatExecution fact: ${canonical.error}`)
    return canonical.value
  })
  const folded = surfaces.fold(facts)
  if (!folded.ok) throw new TypeError(`invalid canonical ChatExecution projection: ${folded.error}`)
  const status = surfaces.queryFacts(facts, input.key.sessionId, input.key.physicalUserMessageId)
  if (!status.ok) throw new TypeError(`invalid canonical ChatExecution status: ${status.error}`)
  const reconciliation = surfaces.reconcileCapacityEvidence(input.capacitySnapshot)
  const diagnostics = input.diagnostics.map(surfaces.projectRecord)
  const runtime = await surfaces.recoverScenarios([input.recovery.scenario])

  const body = {
    schemaVersion: SCHEMA_VERSION,
    kind: KIND,
    execution: { key: input.key, facts, projection: folded.value, status: status.status },
    capacity: { snapshot: input.capacitySnapshot, reconciliation },
    diagnostics,
    hostContract: projectHostContract(input.hostContract),
    recovery: { observation: input.recovery, runtime },
  }
  return deepFreeze({ ...body, integrity: { algorithm: 'sha256', digest: digestOf(body) } })
}

export const replayEvidence = async (serialized, surfaces) => {
  const evidence = typeof serialized === 'string' ? JSON.parse(serialized) : structuredClone(serialized)
  validateEnvelopeShape(evidence)
  if (digestOf(withoutIntegrity(evidence)) !== evidence.integrity.digest) throw new TypeError('incident evidence integrity mismatch')

  const folded = surfaces.fold(evidence.execution.facts)
  if (!folded.ok || canonicalJson(folded.value) !== canonicalJson(evidence.execution.projection)) {
    throw new TypeError('incident execution projection mismatch')
  }
  const status = surfaces.queryFacts(
    evidence.execution.facts,
    evidence.execution.key.sessionId,
    evidence.execution.key.physicalUserMessageId,
  )
  if (!status.ok || canonicalJson(status.status) !== canonicalJson(evidence.execution.status)) {
    throw new TypeError('incident execution status mismatch')
  }
  const reconciliation = surfaces.reconcileCapacityEvidence(evidence.capacity.snapshot)
  if (canonicalJson(reconciliation) !== canonicalJson(evidence.capacity.reconciliation)) {
    throw new TypeError('incident capacity reconciliation mismatch')
  }
  for (const record of evidence.diagnostics) surfaces.projectRecord(record)
  const runtime = await surfaces.recoverScenarios([evidence.recovery.observation.scenario])
  if (canonicalJson(runtime) !== canonicalJson(evidence.recovery.runtime)) {
    throw new TypeError('incident recovery decision mismatch')
  }

  return deepFreeze({
    ok: true,
    execution: evidence.execution,
    capacity: evidence.capacity,
    recovery: evidence.recovery,
    operatorActions: actionOf(runtime),
    mutations: [],
  })
}

export const serializeEvidence = (evidence) => `${canonicalJson(evidence)}\n`

const main = async () => {
  if (process.argv[2] !== 'replay' || !process.argv[3]) {
    throw new TypeError('usage: node incident-evidence.mjs replay <evidence.json>')
  }
  const root = new URL('../../../../', import.meta.url)
  const [chat, status, diagnostics, routing, recovery] = await Promise.all([
    import(new URL('dist/Execution/Session/ChatExecution/Surface.js', root)),
    import(new URL('dist/Execution/Session/ChatExecution/StatusSurface.js', root)),
    import(new URL('dist/OpenCode/Host/ReliabilityDiagnosticsSurface.js', root)),
    import(new URL('dist/OpenCode/Host/ModelRoutingSurface.js', root)),
    import(new URL('dist/Execution/Session/ChatExecution/RecoveryRuntimeSurface.js', root)),
  ])
  const result = await replayEvidence(await readFile(process.argv[3], 'utf8'), {
    canonicalize: chat.canonicalize,
    fold: chat.fold,
    queryFacts: status.queryFacts,
    projectRecord: diagnostics.projectRecord,
    reconcileCapacityEvidence: routing.reconcileCapacityEvidence,
    recoverScenarios: recovery.recoverScenarios,
  })
  process.stdout.write(serializeEvidence(result))
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main().catch((error) => {
  process.stderr.write(`${error.message}\n`)
  process.exitCode = 1
})
