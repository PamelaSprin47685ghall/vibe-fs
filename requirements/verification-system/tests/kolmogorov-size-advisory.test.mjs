import assert from 'node:assert/strict'
import { spawnSync } from 'node:child_process'
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')
const SCRIPT = join(ROOT, 'scripts/checks/kolmogorov-size.mjs')

const fixture = () => {
  const root = mkdtempSync(join(tmpdir(), 'kolmogorov-advisory-'))
  mkdirSync(join(root, 'src/Wanxiangshu'), { recursive: true })
  mkdirSync(join(root, 'tests'), { recursive: true })
  mkdirSync(join(root, 'scripts'), { recursive: true })
  const file = join(root, 'src/Wanxiangshu/Large.fs')
  writeFileSync(file, Array.from({ length: 240 }, (_, i) => `let value${i} = ${i}`).join('\n') + '\n')
  return { root, file }
}

const run = (args) => spawnSync(process.execPath, [SCRIPT, ...args], { encoding: 'utf8' })

test('kolmogorov size over advisory limit never blocks', () => {
  const fx = fixture()
  try {
    const result = run([`--root=${fx.root}`])
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /suggestion: src\/Wanxiangshu\/Large\.fs: 240 lines exceeds advisory 200/)
    assert.match(result.stdout, /0 blocking finding\(s\)/)
  } finally {
    rmSync(fx.root, { recursive: true, force: true })
  }
})

test('kolmogorov growth beyond baseline is suggestion not ratchet failure', () => {
  const fx = fixture()
  try {
    const baseline = JSON.stringify({ baseline: { 'src/Wanxiangshu/Large.fs': 200 }, _exceptions: {} })
    const result = run([`--root=${fx.root}`, `--baseline=${baseline}`])
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /grew beyond baseline 200/)
    assert.doesNotMatch(result.stderr, /FAIL:/)
  } finally {
    rmSync(fx.root, { recursive: true, force: true })
  }
})
