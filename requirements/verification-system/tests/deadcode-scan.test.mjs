import assert from 'node:assert/strict'
import { mkdtemp, mkdir, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import {
  evaluateDeadBindings,
  scanDeadBindings,
} from '../../../scripts/checks/deadcode.mjs'

const fixture = async (files) => {
  const root = await mkdtemp(join(tmpdir(), 'wanxiang-deadcode-'))
  for (const [relative, text] of Object.entries(files)) {
    const path = join(root, relative)
    await mkdir(join(path, '..'), { recursive: true })
    await writeFile(path, text)
  }
  return root
}

test('WHAT[VERIFICATION-SYSTEM-004] deadcode_private_binding_without_any_repository_reference_is_red', async () => {
  const root = await fixture({ 'A.fs': 'module A\nlet private orphan = 1\n' })
  const hits = await scanDeadBindings(root)
  assert.deepEqual(hits.map(({ binding }) => binding), ['orphan'])
  assert.equal(evaluateDeadBindings(hits, []).regressions.length, 1)
})

test('WHAT[VERIFICATION-SYSTEM-004] deadcode_referenced_private_binding_is_green', async () => {
  const root = await fixture({ 'A.fs': 'module A\nlet private used = 1\nlet value = used + 1\n' })
  assert.deepEqual(await scanDeadBindings(root), [])
})

test('WHAT[VERIFICATION-SYSTEM-010] deadcode_baseline_allows_only_existing_named_debt', async () => {
  const root = await fixture({
    'A.fs': 'module A\nlet private oldDebt = 1\nlet private newDebt = 2\n',
  })
  const hits = await scanDeadBindings(root)
  const result = evaluateDeadBindings(hits, ['A.fs::oldDebt'])
  assert.deepEqual(result.regressions.map(({ key }) => key), ['A.fs::newDebt'])
  assert.deepEqual(result.improvements, [])
})

test('WHAT[VERIFICATION-SYSTEM-005] deadcode_missing_root_fails_closed', async () => {
  await assert.rejects(() => scanDeadBindings(join(tmpdir(), 'wanxiang-deadcode-missing')))
})
