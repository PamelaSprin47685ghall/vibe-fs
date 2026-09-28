// Wanxiangshu model scheduler. Edit this file freely.
// `running` is a multiset of active { model, reasoning } leases.
// `previous` is the last successful physical execution target for this session,
// or null for a new conversation. It is a preference hint, not occupancy.
// `purpose` is the execution purpose: "normal" for the ordinary execution of a
// role, "readonly-delegate" for a readonly delegation, which selects from the
// Predictor pool below. The purpose never changes role or participant identity.
// Return a target to acquire it, or null to wait for an occupancy change.
// Public roles: manager, orchestrator, engineer, devops, blogger.
// bookkeeper and predictor are internal runtime mechanisms, not dispatch targets.
// A model choice does not change role authority. Retired and unknown roles fail closed.
//
// Migration note for an existing configuration: this scheduler now speaks
// protocol 2. Add `export const routingProtocol = 2`, extend route() to
// (role, running, previous, purpose), and export the read-only
// predictorConfiguration() query below. This file is the reference shape; an
// existing user configuration is never overwritten by the runtime.

// Stable contract version of the scheduler ABI. It is not an enable switch.
export const routingProtocol = 2

// Provider-level concurrency limits (maximum concurrent active leases per provider).
const PROVIDER_LIMITS = {
  'ollama-cloud': 16,
  'opencode-go': 8,
  'stepfun': 8,
  'cursor': 4,
  'neuralwatt': 4,
}

const DEFAULT_LIMIT = 4

const providerOf = (model) => model.slice(0, model.indexOf('/'))

// Track providers marked as failed until process restart.
const failedProviders = new Set()

export const markProviderFailed = (provider) => {
  if (provider) failedProviders.add(provider)
}

export const clearFailedProviders = () => {
  failedProviders.clear()
}

export const providerCapacity = (provider) => {
  if (failedProviders.has(provider)) return 0
  return PROVIDER_LIMITS[provider] ?? DEFAULT_LIMIT
}

const isAvailable = (running, model) => {
  const provider = providerOf(model)
  const limit = providerCapacity(provider)
  if (limit <= 0) return false
  const count = running.filter((item) => providerOf(item.model) === provider).length
  return count < limit
}

const targetOf = ([model, reasoning]) => ({ model, reasoning })

const pick = (running, previous, candidates) => {
  if (previous) {
    const preferred = candidates.find(
      ([model, reasoning]) => model === previous.model && reasoning === previous.reasoning,
    )
    if (preferred && isAvailable(running, preferred[0])) return targetOf(preferred)
  }

  for (const candidate of candidates) {
    if (isAvailable(running, candidate[0])) return targetOf(candidate)
  }
  return null
}

const CHEAP_A = [
  ['ollama-cloud/gemma4:31b', 'none'],
  ['opencode-go/deepseek-v4-flash', 'none'],
]

const CHEAP_B = [
  ['opencode-go/deepseek-v4-flash', 'none'],
]

const FLASH = [
  ['stepfun/step-3.5-flash-2603', 'none'],
]

const STANDARD = [
  ['opencode-go/deepseek-v4-flash', 'low'],
]

const PREMIUM = [
  ['cursor/cursor-grok-4.6-xhigh', 'xhigh'],
  ['neuralwatt/glm-5.2-flex', 'high'],
]

const ENGINEER_POOL = [
  ...PREMIUM,
  ...STANDARD,
  ...FLASH,
]

const MANAGER_POOL = [
  ...PREMIUM,
  ...STANDARD,
]

const ORCHESTRATOR_POOL = [
  ...PREMIUM,
  ...STANDARD,
]

const DEVOPS_POOL = [
  ...PREMIUM,
  ...STANDARD,
]

const BLOGGER_POOL = [
  ...CHEAP_A,
  ...CHEAP_B,
]

const BOOKKEEPER_POOL = [
  ...FLASH,
  ...STANDARD,
]

const PREDICTOR_POOL = [
  ...CHEAP_A,
  ...FLASH,
]

const pools = new Map([
  ['engineer', ENGINEER_POOL],
  ['manager', MANAGER_POOL],
  ['orchestrator', ORCHESTRATOR_POOL],
  ['devops', DEVOPS_POOL],
  ['blogger', BLOGGER_POOL],
  ['bookkeeper', BOOKKEEPER_POOL],
  ['predictor', PREDICTOR_POOL],
])

// A readonly delegation is a new physical execution of the same identity: it
// reads from the Predictor slot below, never from the requesting role's pool.
const poolFor = (role, purpose) =>
  purpose === 'readonly-delegate' ? pools.get('predictor') : pools.get(role)

export const hasTheoreticalCapacity = (role, purpose) => {
  const candidates = poolFor(role, purpose)
  if (!candidates || candidates.length === 0) return false
  return candidates.some(([model]) => {
    const provider = providerOf(model)
    return providerCapacity(provider) > 0
  })
}

// Read-only existence query for the Predictor model slot: slot absent or
// candidates empty is not configured; valid non-empty targets are configured; a
// malformed structure is a configuration error. Capacity and provider health
// deliberately take no part in this answer, and one route returning null never
// means "not configured".
export const predictorConfiguration = () => {
  const candidates = pools.get('predictor')
  if (!candidates || candidates.length === 0) return { state: 'unconfigured', reason: null }

  for (const candidate of candidates) {
    if (!Array.isArray(candidate) || candidate.length !== 2) {
      return { state: 'invalid', reason: 'Predictor candidates must be [model, reasoning] pairs' }
    }

    const [model, reasoning] = candidate

    if (
      typeof model !== 'string' ||
      model.indexOf('/') <= 0 ||
      model.indexOf('/') === model.length - 1
    ) {
      return { state: 'invalid', reason: `Predictor model must be a full provider/model: ${model}` }
    }

    if (typeof reasoning !== 'string' || reasoning.length === 0) {
      return { state: 'invalid', reason: `Predictor reasoning must be a non-empty string: ${reasoning}` }
    }
  }

  return { state: 'configured', reason: null }
}

export default function route(role, running, previous, purpose) {
  // `previous` is an owner-continuation hint; a readonly delegation enters as a
  // new physical execution and never inherits the owner's target.
  const continuation = purpose === 'readonly-delegate' ? null : previous

  // The fixed DevOps target preference belongs to the owner execution only.
  if (purpose !== 'readonly-delegate' && role === 'devops' && continuation) {
    if (isAvailable(running, continuation.model)) {
      return continuation
    }
  }

  const candidates = poolFor(role, purpose)
  if (!candidates) return null
  return pick(running, continuation, candidates)
}
