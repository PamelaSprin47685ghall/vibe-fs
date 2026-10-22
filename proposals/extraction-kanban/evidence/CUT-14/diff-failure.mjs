// CUT-14 ① differential: the pre-cut host decision (compiled F# ExecutionFailurePolicy behind
// dist/Execution/Failure/Surface.js, run as-is from a pre-cut dist snapshot) against wanxiangli/failure.
// usage: node diff-failure.mjs <seed> <runs> <old Surface.js> [library ExecutionFailurePolicy.js] [mutant-name]
import { pathToFileURL } from 'node:url'
const [seedArg, runsArg, oldPath, libPath, mutant] = process.argv.slice(2)
const old = await import(pathToFileURL(oldPath).href)
const { ExecutionFailurePolicy } = libPath ? await import(pathToFileURL(libPath).href) : await import('wanxiangli/failure')
const lib = new ExecutionFailurePolicy()

let s = Number(seedArg) >>> 0 || 1
const rnd = () => ((s = (Math.imul(s ^ (s >>> 15), 0x2c1b3c6d) + 0x9e3779b9) >>> 0) / 2 ** 32)
const pick = xs => xs[Math.floor(rnd() * xs.length)]
const kinds = ['LocalInvariant', 'ProtocolRejection', 'AuthorizationDenied', 'UserCancelled', 'Superseded', 'CapacityQueueFull',
  'ProviderTransient', 'ProviderPermanent', 'AcceptanceUnknown', 'StreamInterruptedAfterFirstToken']
const commitments = ['NotCommitted', 'Committed', 'Unknown', 'NoNewWrite']
const ids = ['a', 'b', 'a|1:b', '1:a', 'b|1:c', 'x'.repeat(12), 'run-é', '7']
const bad = () => pick([undefined, null, '', '  ', 3, 'Bogus'])
const maybe = (good, p = 0.04) => (rnd() < p ? bad() : good())

function failure() {
  if (rnd() < 0.3) return maybe(() => ({ kind: 'PersistenceFailure', commitment: maybe(() => pick(commitments)) }))
  const k = maybe(() => pick(kinds))
  return rnd() < 0.5 ? k : { kind: k }
}
function input() {
  return {
    failure: failure(),
    phase: maybe(() => pick(['NoAcceptedFact', 'AcceptedBeforeProvider', 'ProviderStarted', 'Terminal'])),
    executionKey: { sessionId: pick(ids), physicalUserMessageId: pick(ids) },
    capacityFence: rnd() < 0.3 ? null : { reference: pick(ids) },
    provider: {
      logicalRun: maybe(() => pick(ids), 0.02), providerRun: maybe(() => pick(ids), 0.02),
      requestKind: maybe(() => pick(['WorkMain', 'InteractionRepair']), 0.02),
      retryBudget: maybe(() => pick(['Available', 'Exhausted']), 0.02), breaker: maybe(() => pick(['Closed', 'Open']), 0.02)
    }
  }
}

const mutate = d => {
  if (!mutant) return d
  const m = { ...d }
  if (mutant === 'M1-unknown-releases' && m.capacitySettlement === 'RetainExactFence') m.capacitySettlement = 'ReleaseExactFence'
  if (mutant === 'M2-retry-open-breaker' && m.resolution === 'TerminalizeProviderStarted' && m.breaker !== 'NoBreakerTransition') m.resolution = 'RetryFreshAttempt'
  if (mutant === 'M3-noaccepted-releases' && m.capacitySettlement === 'NoCapacitySettlement' && m.capacityFence === null && m.resolution === 'PreserveCurrentFact') m.capacitySettlement = 'ReleaseExactFence'
  if (mutant === 'M4-cancel-as-failed' && m.terminalDisposition === 'Cancelled') m.terminalDisposition = 'Failed'
  if (mutant === 'M5-no-fatal' && m.fatality === 'FatalAfterSettlement') m.fatality = 'NoFatality'
  if (mutant === 'M6-unknown-as-preserve' && m.resolution === 'AwaitAcceptanceReconciliation') m.resolution = 'PreserveCurrentFact'
  if (mutant === 'M7-breaker-swap' && m.breaker === 'RecordProviderPermanentFailure') m.breaker = 'RecordProviderTransientFailure'
  return m
}

// library decision → the pre-cut Surface view shape (what the host adapter must reproduce)
function view(input, d) {
  const key = d.executionKey && { sessionId: d.executionKey.sessionId, physicalUserMessageId: d.executionKey.physicalUserMessageId }
  return {
    resolution: d.resolution,
    authorization: d.authorization && { ...d.authorization },
    terminalDisposition: d.terminalDisposition,
    executionKey: key,
    breaker: { kind: d.breaker },
    capacitySettlement: d.capacitySettlement === 'NoCapacitySettlement' ? { kind: d.capacitySettlement }
      : { kind: d.capacitySettlement, fenceReference: d.capacityFence.reference },
    fatality: { kind: d.fatality }
  }
}
const norm = v => (v === undefined || v === null ? null : Array.isArray(v) ? v.map(norm)
  : typeof v === 'object' ? Object.fromEntries(Object.keys(v).sort().map(k => [k, norm(v[k])])) : v)
const run = f => { try { return { ok: norm(f()) } } catch { return { threw: true } } }

const seen = {}
let mismatches = 0, first = null
const runs = Number(runsArg) || 1000
for (let i = 0; i < runs; i++) {
  const inp = input()
  const a = run(() => old.decide(inp))
  const b = run(() => view(inp, mutate(lib.decide(inp))))
  const tag = a.threw ? 'throw' : a.ok.resolution
  seen[tag] = (seen[tag] || 0) + 1
  if (JSON.stringify(a) !== JSON.stringify(b)) { mismatches++; first ??= { inp, a, b } }
}
console.log(JSON.stringify({ seed: Number(seedArg), runs, mutant: mutant ?? null, mismatches, first, seen }))
