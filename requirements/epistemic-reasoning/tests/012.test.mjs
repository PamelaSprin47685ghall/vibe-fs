import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync, readdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { close, createStore, start, resume, state, assessWhy, relativeServerEntry } from './support.mjs'

const here = dirname(fileURLToPath(import.meta.url))

const root = join(here, '../../..')

test('WHAT[EPI-012] closure_is_idempotent_at_fixed_point', () => {
  const store = createStore()
  const started = start(store, '花儿为什么这样红？')
  assessWhy(store, started.handle)
  const first = close(store, started.handle)
  const second = close(store, started.handle)
  assert.deepEqual(second, first)
})
