// MACFG: ManagedAgentConfig gate — validate/applyOwnedFields/configureFromHostConfig
// over the Host-final opencode.json agent inventory.

import assert from 'node:assert/strict'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { dirname, join } from 'node:path'

const here = dirname(fileURLToPath(import.meta.url))
const { resultOf, runtimeResources, mapEntries, caseOf, isSome, isNone } = await import('../../verification-system/tests/support/domain.mjs')

const bindingsOf = (inventory) => Object.fromEntries(mapEntries(inventory.Bindings))

// configureFromHostConfig injects RuntimeResources prompts into StaticTools.*AgentConfig.
// Production installs resources at plugin init.
runtimeResources.installFromPackage()

const { validate, applyOwnedFields, configureFromHostConfig, tryOpencodeModel, tryBoundModel } = await import(
  join(here, '../../../dist/Infrastructure/OpenCode/Host/ManagedAgentConfig.js')
)
const { OpencodeModel } = await import(
  join(here, '../../../dist/Infrastructure/OpenCode/Codec/OpencodeTypes.js')
)

const okOf = (r) => resultOf(r)
const errOf = (r) => resultOf(r).error

const NAMES = [
  'fast-orchestrator', 'deep-orchestrator',
  'fast-manager', 'deep-manager',
  'fast-coder', 'deep-coder',
  'fast-inspector', 'deep-inspector',
  'fast-devops', 'deep-devops',
  'fast-browser', 'deep-browser',
  'fast-inquiry', 'deep-inquiry',
  'fast-reviewer', 'deep-reviewer',
  'fast-blogger', 'deep-blogger',
  'fast-distiller', 'deep-distiller',
  'fast-bookkeeper', 'deep-bookkeeper',
]

// model differs per tier so no pair collides
function fullConfig(overrides = {}) {
  const agent = {}
  for (const name of NAMES) {
    agent[name] = { model: name.includes('fast') ? 'fast-model' : 'deep-model' }
  }
  return { ...overrides, agent }
}

test('MACFG_validate_rejects_null_and_missing_agent_map', () => {
  assert.match(errOf(validate(null)), /config\.agent/)
  assert.match(errOf(validate(undefined)), /config\.agent/)
  assert.match(errOf(validate({})), /config\.agent/)
})

test('MACFG_validate_reports_missing_managed_agent_in_order', () => {
  const err = errOf(validate({ agent: {} }))
  assert.match(err, /Missing required managed agent 'fast-orchestrator'/)
})

test('MACFG_validate_reports_missing_model_then_empty_model', () => {
  const missing = fullConfig()
  missing.agent['fast-orchestrator'] = {}
  assert.match(errOf(validate(missing)), /'fast-orchestrator' is missing a non-empty model binding/)

  const empty = fullConfig()
  empty.agent['fast-orchestrator'] = { model: '   ' }
  assert.match(errOf(validate(empty)), /'fast-orchestrator' has an empty model binding/)
})

test('MACFG_validate_accepts_model_object_with_provider_model_ids', () => {
  const cfg = fullConfig()
  cfg.agent['fast-manager'] = { model: { providerID: 'anthropic', modelID: 'sonnet' } }
  const ok = okOf(validate(cfg))
  assert.equal(ok.ok, true)
  const bindings = bindingsOf(ok.value)
  assert.equal(bindings['fast-manager'].Model, 'anthropic/sonnet')
})

test('MACFG_validate_rejects_duplicate_pair_model', () => {
  const cfg = fullConfig()
  cfg.agent['fast-manager'] = { model: 'same' }
  cfg.agent['deep-manager'] = { model: 'same' }
  const err = errOf(validate(cfg))
  assert.match(err, /fast-manager\/deep-manager resolves to the same model/)
  assert.match(err, /Shared model: same/)
})

test('MACFG_validate_rejects_legacy_agent_present', () => {
  const cfg = fullConfig()
  cfg.agent.coder = { model: 'x' }
  const err = errOf(validate(cfg))
  assert.match(err, /coder/)
})

test('MACFG_validate_ok_builds_full_inventory_with_trimmed_models', () => {
  const cfg = fullConfig()
  cfg.agent['fast-coder'] = { model: '  trimmed-model  ' }
  const ok = okOf(validate(cfg))
  assert.equal(ok.ok, true)
  const bindings = bindingsOf(ok.value)
  assert.equal(bindings['fast-coder'].Model, 'trimmed-model')
  assert.equal(Object.keys(bindings).length, 20, 'bookkeeper pair validates but is not a Role binding')
  assert.equal(caseOf(bindings['deep-blogger'].Agent.Role), 'Blogger')
})

test('MACFG_applyOwnedFields_writes_owned_keys_and_never_touches_model', () => {
  const cfg = fullConfig()
  const ok = okOf(validate(cfg))
  assert.equal(ok.ok, true)
  const inventory = ok.value
  applyOwnedFields(cfg, inventory)

  assert.equal(cfg.compaction.auto, false, 'compaction.auto must be forced false')
  assert.equal(cfg.mcp['stealth-browser-mcp'].type, 'local')
  for (const name of NAMES) {
    const entry = cfg.agent[name]
    assert.ok(entry.mode !== undefined, `${name} must receive owned mode`)
    assert.ok(entry.permission !== undefined, `${name} must receive owned permission`)
    assert.equal(entry.model, name.includes('fast') ? 'fast-model' : 'deep-model', 'model must stay untouched')
  }
})

test('MACFG_applyOwnedFields_skips_null_config_and_missing_agents', () => {
  applyOwnedFields(null, { Bindings: {} }) // must not throw
  const cfg = fullConfig()
  delete cfg.agent['deep-blogger']
  applyOwnedFields(cfg, { Bindings: {} })
  assert.equal(cfg.agent['deep-blogger'], undefined, 'missing agents are not invented')
  assert.equal(cfg.agent['fast-coder'].model, 'fast-model', 'unbound agents stay untouched')
})

test('MACFG_applyOwnedFields_honors_chat_max_retries_env', () => {
  process.env.WANXIANGSHU_CHAT_MAX_RETRIES = '7'
  try {
    const cfg = fullConfig()
    const okv = okOf(validate(cfg))
    applyOwnedFields(cfg, okv.value)
    assert.equal(cfg.experimental.chatMaxRetries, 7)
  } finally {
    delete process.env.WANXIANGSHU_CHAT_MAX_RETRIES
  }
})

test('MACFG_configureFromHostConfig_ok_path_applies_fields_and_returns_inventory', () => {
  const cfg = fullConfig()
  const ok = okOf(configureFromHostConfig(cfg))
  assert.equal(ok.ok, true)
  const bindings = bindingsOf(ok.value)
  assert.equal(bindings['fast-blogger'].Model, 'fast-model')
  assert.equal(cfg.compaction.auto, false)
})

test('MACFG_configureFromHostConfig_error_path_still_writes_role_fields', () => {
  const cfg = fullConfig()
  cfg.agent['fast-manager'] = { model: 'same' }
  cfg.agent['deep-manager'] = { model: 'same' }
  const r = okOf(configureFromHostConfig(cfg))
  assert.equal(r.ok, false)
  const err = r.error
  assert.match(err, /resolves to the same model/)
  assert.equal(cfg.compaction.auto, false, 'fail-closed: owned fields still applied')
  assert.ok(cfg.agent['fast-manager'].mode !== undefined)
})

test('MACFG_tryOpencodeModel_parses_provider_slash_model_and_refuses_to_invent_fast', () => {
  const cfg = fullConfig()
  cfg.agent['deep-coder'] = { model: 'anthropic/deep-opus' }
  cfg.agent['fast-coder'] = { model: 'anthropic/fast-haiku' }
  const inventory = okOf(validate(cfg)).value

  const deep = tryOpencodeModel(inventory, 'deep-coder', undefined)
  assert.equal(isSome(deep), true)
  assert.equal(deep.providerID, 'anthropic')
  assert.equal(deep.modelID, 'deep-opus')

  const fast = tryOpencodeModel(inventory, 'fast-coder', undefined)
  assert.equal(fast.modelID, 'fast-haiku')
  assert.notEqual(fast.modelID, deep.modelID)

  assert.equal(isNone(tryOpencodeModel(inventory, 'unknown-agent', undefined)), true)
  assert.equal(isNone(tryOpencodeModel(inventory, '', undefined)), true)

  const current = new OpencodeModel('anthropic', 'fast-haiku', undefined)
  const corrected = tryOpencodeModel(inventory, 'deep-coder', current)
  assert.equal(corrected.providerID, 'anthropic')
  assert.equal(corrected.modelID, 'deep-opus')
})

test('MACFG_tryOpencodeModel_bare_id_keeps_current_provider_and_refuses_without_one', () => {
  const inventory = okOf(validate(fullConfig())).value
  assert.equal(isNone(tryOpencodeModel(inventory, 'deep-coder', undefined)), true, 'bare id must not invent a provider')

  const current = new OpencodeModel('anthropic', 'fast-model', undefined)
  const corrected = tryOpencodeModel(inventory, 'deep-coder', current)
  assert.equal(corrected.providerID, 'anthropic')
  assert.equal(corrected.modelID, 'deep-model')
})

test('MACFG_tryBoundModel_reads_live_inventory_after_configure', () => {
  const cfg = fullConfig()
  cfg.agent['deep-coder'] = { model: 'anthropic/deep-opus' }
  cfg.agent['fast-coder'] = { model: 'anthropic/fast-haiku' }
  const ok = okOf(configureFromHostConfig(cfg))
  assert.equal(ok.ok, true, ok.ok ? '' : ok.error)

  const deep = tryBoundModel('deep-coder')
  assert.equal(isSome(deep), true)
  assert.equal(deep.providerID, 'anthropic')
  assert.equal(deep.modelID, 'deep-opus')

  const fast = tryBoundModel('fast-coder')
  assert.equal(fast.modelID, 'fast-haiku')
  assert.notEqual(fast.modelID, deep.modelID)
  assert.equal(isNone(tryBoundModel('unknown-agent')), true)
})
