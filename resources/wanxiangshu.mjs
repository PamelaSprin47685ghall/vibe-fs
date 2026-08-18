// Wanxiangshu model scheduler. Edit this file freely.
// `running` is a multiset of active { model, reasoning } leases.
// `previous` is the last successful physical execution target for this session,
// or null for a new conversation. It is a preference hint, not occupancy.
// Return a target to acquire it, or null to wait for an occupancy change.

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

const isAvailable = (running, model) => {
  const provider = providerOf(model)
  const limit = PROVIDER_LIMITS[provider] ?? DEFAULT_LIMIT
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

const FASTEST = [
  ['ollama-cloud/gemma4:31b', 'none'],
  ['opencode-go/deepseek-v4-flash', 'none'],
]

const FASTEST_II = [
  ['opencode-go/deepseek-v4-flash', 'none'],
]

const FASTER = [
  ['stepfun/step-3.5-flash-2603', 'none'],
]

const MEDIUM = [
  ['opencode-go/deepseek-v4-flash', 'low'],
]

const HIGHER = [
  ['cursor/cursor-grok-4.6-xhigh', 'xhigh'],
  ['neuralwatt/glm-5.2-flex', 'high'],
]

const FAST_BROWSER = [
  ['ollama-cloud/minimax-m3', 'none'],
]

const DEEP_BROWSER = [
  ['opencode-go/minimax-m3', 'none'],
]

const pools = new Map([
  ['fast-distiller', FASTEST],
  ['fast-blogger', FASTEST],
  ['deep-distiller', FASTEST_II],
  ['deep-blogger', FASTEST_II],
  ['fast-inspector', FASTER],
  ['fast-bookkeeper', FASTER],
  ['deep-inspector', MEDIUM],
  ['deep-bookkeeper', MEDIUM],
  ['fast-manager', MEDIUM],
  ['fast-orchestrator', MEDIUM],
  ['fast-coder', MEDIUM],
  ['fast-devops', MEDIUM],
  ['fast-inquiry', MEDIUM],
  ['fast-reviewer', MEDIUM],
  ['deep-manager', HIGHER],
  ['deep-orchestrator', HIGHER],
  ['deep-coder', HIGHER],
  ['deep-devops', HIGHER],
  ['deep-inquiry', HIGHER],
  ['deep-reviewer', HIGHER],
  ['fast-browser', FAST_BROWSER],
  ['deep-browser', DEEP_BROWSER],
])

export default function route(role, running, previous) {
  const candidates = pools.get(role)
  if (!candidates) throw new Error(`unknown model-routing role: ${role}`)
  return pick(running, previous, candidates)
}
