import assert from 'node:assert/strict'
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const { bootstrapAndLoadAt, invokeScheduler } = routing
const templateUrl = new URL('../../../resources/wanxiangshu.mjs', import.meta.url)

const MANAGED = [
  'manager',
  'orchestrator',
  'coder',
  'inspector',
  'devops',
  'browser',
  'inquiry',
  'blogger',
  'distiller',
]

const template = `export default function route(role, running) {
  if (role !== 'coder') return null
  return running.length === 0
    ? { model: 'provider/coder-model', reasoning: 'none' }
    : null
}\n`

const withTemp = async (run) => {
  const root = await mkdtemp(join(tmpdir(), 'wanxiangshu-routing-'))
  try {
    await run(join(root, 'nested', 'wanxiangshu.mjs'))
  } finally {
    await rm(root, { recursive: true, force: true })
  }
}

test('WHAT[EMR-001] EMR_001_recommended_resource_is_directly_executable_and_uses_full_model_selectors', async () => {
  const source = await readFile(templateUrl, 'utf8')
  assert.match(source, /export default function route/)
  const { default: scheduler } = await import(`${templateUrl.href}?test=${Date.now()}`)
  const { invokeScheduler } = await import('../../../dist/OpenCode/Host/ModelRoutingSurface.js')
  const route = (role, running, previous = null) => invokeScheduler(scheduler, role, running, previous)

  for (const role of MANAGED) {
    const selected = route(role, [])
    assert.ok(selected, `${role} must be schedulable from the empty recommended template`)
    assert.match(selected.model, /^[^/\s]+\/.+$/, `${role} must use provider/model, never a bare model id`)
    assert.ok(selected.reasoning.length > 0)
  }

  assert.match(route('browser', [], null).model, /minimax-m3/)
  assert.throws(() => route('fast-browser', []), /unknown model-routing role/)
  assert.throws(() => route('deep-coder', []), /unknown model-routing role/)
})

test('WHAT[EMR-001] EMR_001_missing_scheduler_is_created_once_then_loaded_from_disk', async () => {
  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(path, template)
    assert.equal(await readFile(path, 'utf8'), template)
    const selected = invokeScheduler(scheduler, 'coder', [])
    assert.equal(selected.model, 'provider/coder-model')
    assert.equal(selected.reasoning, 'none')
  })
})

test('WHAT[EMR-001] EMR_001_existing_scheduler_is_never_overwritten', async () => {
  await withTemp(async (path) => {
    const existing = `export default () => ({ model: 'provider/user-choice', reasoning: 'high' })\n`
    await import('node:fs/promises').then(({ mkdir }) => mkdir(join(path, '..'), { recursive: true }))
    await writeFile(path, existing, 'utf8')

    const scheduler = await bootstrapAndLoadAt(path, template)
    assert.equal(await readFile(path, 'utf8'), existing)
    assert.equal(invokeScheduler(scheduler, 'coder', []).model, 'provider/user-choice')
  })
})

test('WHAT[EMR-001] EMR_001_concurrent_bootstrap_keeps_one_atomic_winner_without_merge', async () => {
  await withTemp(async (path) => {
    const [left, right] = await Promise.all([
      bootstrapAndLoadAt(path, template),
      bootstrapAndLoadAt(path, template),
    ])

    assert.equal(await readFile(path, 'utf8'), template)
    assert.equal(invokeScheduler(left, 'coder', []).model, 'provider/coder-model')
    assert.equal(invokeScheduler(right, 'coder', []).model, 'provider/coder-model')
  })
})
