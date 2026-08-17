// Wanxiangshu model scheduler. Edit this file freely.
// `running` is a multiset of active { model, reasoning } leases.
// `previous` is the last successful physical execution target for this session,
// or null for a new conversation. It is a preference hint, not occupancy.
// Return a target to acquire it, or null to wait for an occupancy change.

const providerOf = (model) => model.slice(0, model.indexOf('/'))

const providerCount = (running, model) =>
  running.filter((item) => providerOf(item.model) === providerOf(model)).length

const available = (running, [model, , limit]) => providerCount(running, model) < limit

const targetOf = ([model, reasoning]) => ({ model, reasoning })

const pick = (running, previous, candidates) => {
  const preferred = previous && candidates.find(
    ([model, reasoning]) => model === previous.model && reasoning === previous.reasoning,
  )

  if (preferred && available(running, preferred)) return targetOf(preferred)

  for (const candidate of candidates) {
    if (available(running, candidate)) return targetOf(candidate)
  }
  return null
}

const FASTEST = [
  ['ollama-cloud/gemma4:31b', 'none', 16],
  ['opencode-go/deepseek-v4-flash', 'none', 16],
]

const FASTEST_II = [
  ['opencode-go/deepseek-v4-flash', 'none', 16],
]

const FASTER = [
  ['stepfun/step-3.5-flash-2603', 'none', 8],
]

const MEDIUM = [
  ['opencode-go/deepseek-v4-flash', 'low', 8],
]

const HIGHER = [
  ['cursor/cursor-grok-4.6-xhigh', 'xhigh', 4],
  ['neuralwatt/glm-5.2-flex', 'high', 4],
]

const FAST_BROWSER = [
  ['ollama-cloud/minimax-m3', 'none', 8],
]

const DEEP_BROWSER = [
  ['opencode-go/minimax-m3', 'none', 4],
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
