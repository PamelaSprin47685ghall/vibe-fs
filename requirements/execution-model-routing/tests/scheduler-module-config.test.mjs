import assert from 'node:assert/strict'
import { mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import * as routing from '../../../dist/OpenCode/Host/ModelRoutingSurface.js'

const { bootstrapAndLoadAt, invokeScheduler } = routing

const template = `export default function route(role, running) {
  if (role !== 'fast-coder') return null
  return running.length === 0
    ? { model: 'provider/fast-model', reasoning: 'none' }
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

test('WHAT[EMR-001] EMR_001_missing_scheduler_is_created_once_then_loaded_from_disk', async () => {
  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(path, template)
    assert.equal(await readFile(path, 'utf8'), template)
    const selected = invokeScheduler(scheduler, 'fast-coder', [])
    assert.equal(selected.model, 'provider/fast-model')
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
    assert.equal(invokeScheduler(scheduler, 'deep-coder', []).model, 'provider/user-choice')
  })
})

test('WHAT[EMR-001] EMR_001_concurrent_bootstrap_keeps_one_atomic_winner_without_merge', async () => {
  await withTemp(async (path) => {
    const [left, right] = await Promise.all([
      bootstrapAndLoadAt(path, template),
      bootstrapAndLoadAt(path, template),
    ])

    assert.equal(await readFile(path, 'utf8'), template)
    assert.equal(invokeScheduler(left, 'fast-coder', []).model, 'provider/fast-model')
    assert.equal(invokeScheduler(right, 'fast-coder', []).model, 'provider/fast-model')
  })
})

test('WHAT[EMR-002] EMR_002_scheduler_preserves_running_duplicates_null_and_previous', async () => {
  await withTemp(async (path) => {
    const body = `export default function route(role, running, previous) {
      if (running.length !== 2) throw new Error('duplicates lost')
      if (running[0].model !== running[1].model) throw new Error('unexpected running')
      if (role === 'new' && previous !== null) throw new Error('new conversation previous must be null')
      if (role === 'continued' && (previous?.model !== 'provider/previous' || previous?.reasoning !== 'high')) {
        throw new Error('previous target lost')
      }
      return previous
    }\n`
    const scheduler = await bootstrapAndLoadAt(path, body)
    const running = [
      { model: 'provider/shared', reasoning: 'low' },
      { model: 'provider/shared', reasoning: 'low' },
    ]
    assert.equal(invokeScheduler(scheduler, 'new', running, null), null)
    assert.deepEqual(
      invokeScheduler(scheduler, 'continued', running, { model: 'provider/previous', reasoning: 'high' }),
      { model: 'provider/previous', reasoning: 'high' },
    )
  })
})

test('WHAT[EMR-002] EMR_002_scheduler_program_errors_fail_closed', async () => {
  await withTemp(async (path) => {
    const invalidDefault = `export default 42\n`
    await assert.rejects(() => bootstrapAndLoadAt(path, invalidDefault), /default export.*function/i)
  })

  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(path, `export default async () => ({ model: 'provider/x', reasoning: 'none' })\n`)
    assert.throws(() => invokeScheduler(scheduler, 'fast-coder', []), /Promise|synchronous/i)
  })

  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(path, `export default () => ({ model: 'bare-model', reasoning: 'none' })\n`)
    assert.throws(() => invokeScheduler(scheduler, 'fast-coder', []), /provider\/model/i)
  })

  await withTemp(async (path) => {
    const scheduler = await bootstrapAndLoadAt(path, `export default () => ({ model: 'provider/model', reasoning: '' })\n`)
    assert.throws(() => invokeScheduler(scheduler, 'fast-coder', []), /reasoning/i)
  })
})
