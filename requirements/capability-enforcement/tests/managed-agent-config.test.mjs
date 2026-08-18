// Managed-agent Host config projection. The production owner surface translates
// its Result and catalog; the live config object remains the observable contract.

import assert from 'node:assert/strict'
import test from 'node:test'

const { installDefaultResources, validate, configure, configureManager } = await import(
  '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'
)
installDefaultResources()

const okOf = (result) => result
const errOf = (result) => result.error

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

function fullConfig() {
  return { agent: Object.fromEntries(NAMES.map((name) => [name, {}])) }
}

test('WHAT[ENF-010] MACFG_validate_rejects_null_config_and_legacy_agent', () => {
  assert.match(errOf(validate(null)), /Host config object/)
  assert.match(errOf(validate({ agent: { coder: {} } })), /coder/)
})

test('WHAT[ENF-011] MACFG_validate_accepts_empty_agent_map_and_projects_full_catalog', () => {
  const empty = okOf(validate({}))
  assert.equal(empty.ok, true, empty.ok ? '' : empty.error)
  assert.equal(empty.bindingNames.length, 20)
  const blankMap = okOf(validate({ agent: {} }))
  assert.equal(blankMap.ok, true, blankMap.ok ? '' : blankMap.error)
})

test('WHAT[ENF-010] MACFG_validate_rejects_legacy_agent_present', () => {
  const cfg = fullConfig()
  cfg.agent.coder = {}
  assert.match(errOf(validate(cfg)), /coder/)
})

test('WHAT[ENF-011] MACFG_validate_accepts_missing_equal_and_arbitrary_model_fields', () => {
  const cfg = fullConfig()
  cfg.agent['fast-manager'].model = 'provider/shared'
  cfg.agent['deep-manager'].model = 'provider/shared'
  cfg.agent['fast-coder'].model = ''
  cfg.agent['deep-coder'].model = { anything: 'host-owned-and-ignored' }

  const result = okOf(validate(cfg))
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(result.bindingNames.length, 20, 'bookkeepers are presence-checked but have no Role binding')
})

test('WHAT[ENF-011] MACFG_applyOwnedFields_writes_owned_keys_and_never_touches_model', () => {
  const cfg = fullConfig()
  for (const name of NAMES) cfg.agent[name].model = `host/${name}`
  const result = configure(cfg)
  assert.equal(result.ok, true, result.ok ? '' : result.error)

  assert.equal(cfg.compaction.auto, false)
  assert.equal(cfg.mcp['stealth-browser-mcp'].type, 'local')
  for (const name of NAMES) {
    const entry = cfg.agent[name]
    assert.ok(entry.mode !== undefined, `${name} must receive owned mode`)
    assert.ok(entry.permission !== undefined, `${name} must receive owned permission`)
    assert.equal(entry.temperature, 1, `${name} must receive forced temperature 1.0`)
    assert.equal(entry.model, `host/${name}`, 'model stays untouched but is never routing truth')
  }
})

test('WHAT[ENF-011] MACFG_applyOwnedFields_skips_null_config_and_projects_missing_catalog_agents', () => {
  const rejected = configure(null)
  assert.equal(rejected.ok, false)

  const cfg = fullConfig()
  delete cfg.agent['deep-blogger']
  const result = configure(cfg)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  const projected = cfg.agent['deep-blogger']
  assert.equal(projected.mode, 'primary')
  assert.equal(projected.hidden, true)
  assert.equal(projected.permission['*'], 'deny')
  assert.equal('model' in projected, false, 'projection never invents a model binding')
})

test('WHAT[ENF-011] MACFG_applyOwnedFields_honors_chat_max_retries_env', () => {
  process.env.WANXIANGSHU_CHAT_MAX_RETRIES = '7'
  try {
    const cfg = fullConfig()
    const result = configure(cfg)
    assert.equal(result.ok, true, result.ok ? '' : result.error)
    assert.equal(cfg.experimental.chatMaxRetries, 7)
  } finally {
    delete process.env.WANXIANGSHU_CHAT_MAX_RETRIES
  }
})

test('WHAT[ENF-011] MACFG_configureFromHostConfig_returns_role_inventory_without_model_authority', () => {
  const cfg = fullConfig()
  cfg.agent['fast-manager'].model = 'provider/shared'
  cfg.agent['deep-manager'].model = 'provider/shared'
  const result = configure(cfg)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(cfg.compaction.auto, false)
  assert.equal(cfg.agent['fast-manager'].model, 'provider/shared')
  assert.equal(cfg.agent['deep-manager'].model, 'provider/shared')
})

test('WHAT[ENF-011] MACFG_configureFromHostConfig_projects_missing_catalog_without_model_authority', () => {
  const cfg = {}
  const result = configure(cfg)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(result.bindingNames.length, 20)
  for (const name of NAMES) {
    const entry = cfg.agent[name]
    assert.ok(entry.mode !== undefined, `${name} must be projected`)
    assert.ok(entry.permission !== undefined, `${name} must receive owned permission`)
    assert.equal('model' in entry, false, `${name} must not receive a model binding`)
  }
  assert.equal(cfg.agent['fast-blogger'].hidden, true)
  assert.equal(cfg.agent['fast-bookkeeper'].hidden, true)
  assert.equal(cfg.compaction.auto, false)
})

test('WHAT[ENF-010] MACFG_configureManager_legacy_agent_is_fatal_after_owned_fields_land', () => {
  const cfg = fullConfig()
  cfg.agent.coder = {}

  assert.throws(() => configureManager(cfg), /Legacy agent name 'coder'/)
  assert.equal(cfg.compaction.auto, false)
  assert.equal(cfg.agent['fast-manager'].permission['*'], 'deny')
  assert.equal(cfg.agent['fast-orchestrator'].permission['*'], 'deny')
})
