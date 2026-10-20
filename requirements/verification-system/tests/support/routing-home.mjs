import { mkdirSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

export const writeRoutingHome = home => {
  const directory = join(home, '.config', 'opencode')
  mkdirSync(directory, { recursive: true })
  writeFileSync(join(directory, 'wanxiangshu.mjs'), `export const routingProtocol = 2
export default function route(role, running, previous, purpose) {
  if (!new Set(['manager', 'orchestrator', 'engineer', 'coder', 'inspector', 'browser', 'inquiry', 'reviewer', 'devops', 'distiller', 'bookkeeper', 'plan', 'advisor']).has(role)) throw new Error('unexpected managed role: ' + role)
  if (Array.isArray(globalThis.__wanxiangshu_test_routing_seen)) {
    globalThis.__wanxiangshu_test_routing_seen.push({ role, running: running.map(item => ({ ...item })) })
  }
  if (typeof globalThis.__wanxiangshu_test_routing_decision === 'function') {
    return globalThis.__wanxiangshu_test_routing_decision(role, running, previous, purpose)
  }
  return { model: 'provider/' + role + '-model', reasoning: 'none' }
}

export const predictorConfiguration = () => {
  const state = globalThis.__wanxiangshu_test_predictor_state ?? 'unconfigured'
  if (state === 'configured') return { state: 'configured', reason: null }
  if (state === 'invalid') return {
    state: 'invalid',
    reason: globalThis.__wanxiangshu_test_predictor_reason ?? 'test Predictor configuration is invalid',
  }
  return { state: 'unconfigured', reason: null }
}
`, 'utf8')
  return home
}
