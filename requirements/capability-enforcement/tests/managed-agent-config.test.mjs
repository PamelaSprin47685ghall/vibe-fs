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
  'manager',
  'orchestrator',
  'coder',
  'inspector',
  'devops',
  'browser',
  'inquiry',
  'blogger',
  'distiller',
  'bookkeeper',
  'predictor',
]

function fullConfig() {
  return { agent: Object.fromEntries(NAMES.map((name) => [name, {}])) }
}

test('WHAT[ENF-010] MACFG_validate_rejects_null_config_and_legacy_agent', () => {
  assert.match(errOf(validate(null)), /Host config object/)
  assert.match(errOf(validate({ agent: { build: {} } })), /build/)
})

test('WHAT[ENF-011] MACFG_validate_accepts_empty_agent_map_and_projects_full_catalog', () => {
  const empty = okOf(validate({}))
  assert.equal(empty.ok, true, empty.ok ? '' : empty.error)
  assert.equal(empty.bindingNames.length, 10)
  const blankMap = okOf(validate({ agent: {} }))
  assert.equal(blankMap.ok, true, blankMap.ok ? '' : blankMap.error)
})

test('WHAT[ENF-010] MACFG_validate_rejects_legacy_agent_present', () => {
  const cfg = fullConfig()
  cfg.agent.build = {}
  assert.match(errOf(validate(cfg)), /build/)
})

test('WHAT[ENF-011] MACFG_validate_accepts_missing_equal_and_arbitrary_model_fields', () => {
  const cfg = fullConfig()
  cfg.agent.manager.model = 'provider/shared'
  cfg.agent.coder.model = ''
  cfg.agent.inspector.model = { anything: 'host-owned-and-ignored' }

  const result = okOf(validate(cfg))
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(result.bindingNames.length, 10, 'bookkeeper is presence-checked but has no Role binding')
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
  delete cfg.agent.blogger
  const result = configure(cfg)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  const projected = cfg.agent.blogger
  assert.equal(projected.mode, 'primary')
  assert.equal(projected.hidden, true)
  assert.equal(projected.permission['*'], 'deny')
  assert.equal('model' in projected, false, 'projection never invents a model binding')
})

test('WHAT[ENF-011] MACFG_applyOwnedFields_honors_chat_max_retries_env', () => {
  // HOSTFAIL-001 / AGENTS.md §13.2: Wanxiangshu plugin forces chatMaxRetries to zero unconditionally
  const cfg = fullConfig()
  const result = configure(cfg)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(cfg.experimental.chatMaxRetries, 0)
})

test('WHAT[ENF-011] MACFG_configureFromHostConfig_returns_role_inventory_without_model_authority', () => {
  const cfg = fullConfig()
  cfg.agent.manager.model = 'provider/shared'
  const result = configure(cfg)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(cfg.compaction.auto, false)
  assert.equal(cfg.agent.manager.model, 'provider/shared')
})

test('WHAT[ENF-011] MACFG_configureFromHostConfig_projects_missing_catalog_without_model_authority', () => {
  const cfg = {}
  const result = configure(cfg)
  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(result.bindingNames.length, 10)
  for (const name of NAMES) {
    const entry = cfg.agent[name]
    assert.ok(entry.mode !== undefined, `${name} must be projected`)
    assert.ok(entry.permission !== undefined, `${name} must receive owned permission`)
    assert.equal('model' in entry, false, `${name} must not receive a model binding`)
  }
  assert.equal(cfg.agent.blogger.hidden, true)
  assert.equal(cfg.agent.bookkeeper.hidden, true)
  assert.equal(cfg.compaction.auto, false)
})

test('WHAT[ENF-010] MACFG_configureManager_legacy_agent_is_fatal_after_owned_fields_land', () => {
  const cfg = fullConfig()
  cfg.agent.build = {}

  assert.throws(() => configureManager(cfg), /Legacy agent name 'build'/)
  assert.equal(cfg.compaction.auto, false)
  assert.equal(cfg.agent.manager.permission['*'], 'deny')
  assert.equal(cfg.agent.orchestrator.permission['*'], 'deny')
})
