{
const { default: assert } = await import("node:assert/strict");
const { mkdir, mkdtemp, readFile, rm, writeFile } = await import("node:fs/promises");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { default: test } = await import("node:test");
const routing = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");

const { bootstrapAndLoadAt, predictorConfiguration, invokeScheduler } = routing
const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)

// The loader creates the nested directory and publishes the body itself
// (atomic hard-link, never overwriting an existing file), so the test must not
// pre-write into a directory that does not exist yet.
const load = async (body) => {
  const root = await mkdtemp(join(tmpdir(), 'wanxiangshu-predictor-config-'))
  const path = join(root, 'nested', 'wanxiangshu.mjs')
  try {
    return await bootstrapAndLoadAt(path, body)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
}

const loadTemplateWith = async (suffix) => {
  const source = await readFile(templateUrl, 'utf8')
  return load(source + '\n' + suffix)
}

test('WHAT[execution-model-routing-020] EMR_020_recommended_template_predictor_pool_is_configured', async () => {
  const scheduler = await load(await readFile(templateUrl, 'utf8'))
  assert.deepEqual(predictorConfiguration(scheduler), { kind: 'Configured', reason: null })
})

test('WHAT[execution-model-routing-020] EMR_020_absent_or_empty_predictor_slot_is_not_configured', async () => {
  const emptied = await loadTemplateWith("pools.set('predictor', [])")
  assert.deepEqual(predictorConfiguration(emptied), { kind: 'NotConfigured', reason: null })

  const removed = await loadTemplateWith("pools.delete('predictor')")
  assert.deepEqual(predictorConfiguration(removed), { kind: 'NotConfigured', reason: null })
})

test('WHAT[execution-model-routing-020] EMR_020_malformed_predictor_targets_are_a_configuration_error', async () => {
  const badModel = await loadTemplateWith("pools.set('predictor', [['no-slash', 'none']])")
  const observed = predictorConfiguration(badModel)
  assert.equal(observed.kind, 'ConfigurationInvalid')
  assert.match(observed.reason, /provider\/model/)

  const badShape = await loadTemplateWith("pools.set('predictor', ['oops'])")
  assert.equal(predictorConfiguration(badShape).kind, 'ConfigurationInvalid')
})

test('WHAT[execution-model-routing-020] EMR_020_missing_configuration_query_export_is_a_configuration_error', async () => {
  const scheduler = await load(`export const routingProtocol = 2
export default function route(role, running, previous, purpose) {
  return null
}
`)
  const observed = predictorConfiguration(scheduler)
  assert.equal(observed.kind, 'ConfigurationInvalid')
  assert.match(observed.reason, /predictorConfiguration/)
})

test('WHAT[execution-model-routing-020] EMR_020_configuration_state_is_independent_of_capacity_and_provider_health', async () => {
  const scheduler = await load(await readFile(templateUrl, 'utf8'))

  // The bootstrap attaches the loaded module's own helpers onto the scheduler
  // object, so this poisons the providers of exactly the instance under test.
  scheduler.markProviderFailed('ollama-cloud')
  scheduler.markProviderFailed('opencode-go')
  scheduler.markProviderFailed('stepfun')

  assert.deepEqual(
    predictorConfiguration(scheduler),
    { kind: 'Configured', reason: null },
    'poisoned providers do not turn a configured Predictor slot into unconfigured',
  )

  // Every Predictor candidate provider is saturated, so a readonly-delegate
  // route returns null; that still says nothing about the configuration.
  const saturated = [
    ...Array.from({ length: 16 }, () => ({ model: 'ollama-cloud/leased', reasoning: 'none' })),
    ...Array.from({ length: 8 }, () => ({ model: 'opencode-go/leased', reasoning: 'none' })),
    ...Array.from({ length: 8 }, () => ({ model: 'stepfun/leased', reasoning: 'none' })),
  ]
  assert.equal(invokeScheduler(scheduler, 'engineer', saturated, null, 'readonly-delegate'), null)
  assert.deepEqual(predictorConfiguration(scheduler), { kind: 'Configured', reason: null })
})

test('WHAT[execution-model-routing-020] EMR_020_template_readonly_delegate_reads_the_predictor_slot', async () => {
  const scheduler = await load(await readFile(templateUrl, 'utf8'))

  const delegate = invokeScheduler(scheduler, 'engineer', [], null, 'readonly-delegate')
  assert.ok(delegate, 'a configured Predictor slot admits a readonly delegate from an empty running set')
  assert.equal(delegate.model, 'ollama-cloud/gemma4:31b')

  // The same role's normal purpose keeps using the role pool, never the
  // Predictor slot.
  const normal = invokeScheduler(scheduler, 'engineer', [], null, 'normal')
  assert.equal(normal.model, 'cursor/cursor-grok-4.6-xhigh')
})

test('WHAT[execution-model-routing-020] EMR_020_shared_predictor_configuration_is_owned_by_the_loaded_module', async () => {
  const { execFileSync } = await import('node:child_process')
  const root = await mkdtemp(join(tmpdir(), 'wanxiangshu-shared-predictor-'))
  const configDirectory = join(root, '.config', 'opencode')
  try {
    await mkdir(configDirectory, { recursive: true })
    await writeFile(join(configDirectory, 'wanxiangshu.mjs'), `export const routingProtocol = 2
export default function route() { return { model: 'provider/model', reasoning: 'none' } }
export const predictorConfiguration = () => ({ state: globalThis.__wxs_owned_predictor_configuration, reason: null })
`)
    const script = `
      import * as routing from ${JSON.stringify(new URL('../../../dist/OpenCode/Host/ModelRoutingSurface.js', import.meta.url).href)};
      globalThis.__wxs_owned_predictor_configuration = 'unconfigured';
      await routing.initialize();
      globalThis.__wanxiangshu_test_predictor_state = 'configured';
      const first = routing.sharedPredictorConfiguration();
      globalThis.__wxs_owned_predictor_configuration = 'configured';
      globalThis.__wanxiangshu_test_predictor_state = 'unconfigured';
      process.stdout.write(JSON.stringify({ pid: process.pid, first, second: routing.sharedPredictorConfiguration() }));
    `
    const observed = JSON.parse(execFileSync(process.execPath, ['--input-type=module', '-e', script], {
      env: { ...process.env, HOME: root, USERPROFILE: root }, encoding: 'utf8', timeout: 5000,
    }))
    assert.notEqual(observed.pid, process.pid)
    assert.deepEqual(observed.first, { kind: 'NotConfigured', reason: null },
      'an unrelated global flag cannot invent a configured Predictor')
    assert.deepEqual(observed.second, { kind: 'Configured', reason: null },
      'an unrelated global flag cannot disable the actual configured slot')
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})
}
