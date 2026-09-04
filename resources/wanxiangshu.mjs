// Wanxiangshu model scheduler. Edit this file freely.
// `running` is a multiset of active { model, reasoning } leases.
// `previous` is the last successful physical execution target for this session,
// or null for a new conversation. It is a preference hint, not occupancy.
// Return a target to acquire it, or null to wait for an occupancy change.
// `role` is always a canonical lowercase role name (manager, orchestrator,
// coder, inspector, devops, browser, inquiry, blogger, distiller,
// bookkeeper, predictor). Unknown roles fail closed.

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

const BROWSER_A = [
  ['ollama-cloud/minimax-m3', 'none'],
]

const BROWSER_B = [
  ['opencode-go/minimax-m3', 'none'],
]

const CODER_POOL = [
  ...PREMIUM,
  ...STANDARD,
  ...FLASH,
]

const INSPECTOR_POOL = [
  ...FLASH,
  ...STANDARD,
  ...CHEAP_A,
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

const INQUIRY_POOL = [
  ...STANDARD,
  ...FLASH,
]

const BROWSER_POOL = [
  ...BROWSER_A,
  ...BROWSER_B,
]

const BLOGGER_POOL = [
  ...CHEAP_A,
  ...CHEAP_B,
]

const DISTILLER_POOL = [
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
  ['coder', CODER_POOL],
  ['inspector', INSPECTOR_POOL],
  ['manager', MANAGER_POOL],
  ['orchestrator', ORCHESTRATOR_POOL],
  ['devops', DEVOPS_POOL],
  ['inquiry', INQUIRY_POOL],
  ['browser', BROWSER_POOL],
  ['blogger', BLOGGER_POOL],
  ['distiller', DISTILLER_POOL],
  ['bookkeeper', BOOKKEEPER_POOL],
  ['predictor', PREDICTOR_POOL],
])

export const hasTheoreticalCapacity = (role) => {
  const candidates = pools.get(role)
  if (!candidates || candidates.length === 0) return false
  return candidates.some(([model]) => {
    const provider = providerOf(model)
    return providerCapacity(provider) > 0
  })
}

export default function route(role, running, previous) {
  const candidates = pools.get(role)
  if (!candidates) throw new Error(`unknown model-routing role: ${role}`)
  return pick(running, previous, candidates)
}
