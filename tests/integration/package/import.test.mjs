// tests/integration/package/import.test.mjs — import("wanxiangshu") after install.
//
// Confirms main/exports resolve and module evaluation has no throw
// (RuntimeResources loads only on explicit install, not at import).
// Requires dist/ built. Network may be needed for package dependencies.
//   node --test tests/integration/package/import.test.mjs

import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')

const packTarball = (packDir) => {
  const raw = execFileSync(
    'npm',
    ['pack', '--json', '--pack-destination', packDir, '--no-audit', '--no-fund'],
    { cwd: repoRoot, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024 },
  )
  const entry = JSON.parse(raw)[0]
  const tarball = path.join(packDir, entry.filename)
  assert.ok(fs.existsSync(tarball), `tarball missing: ${tarball}`)
  return tarball
}

const installConsumer = (consumerDir, tarball) => {
  fs.writeFileSync(
    path.join(consumerDir, 'package.json'),
    JSON.stringify({ name: 'consumer', private: true, type: 'module' }),
    'utf8',
  )
  execFileSync(
    'npm',
    ['install', tarball, '--no-audit', '--no-fund'],
    {
      cwd: consumerDir,
      encoding: 'utf8',
      maxBuffer: 64 * 1024 * 1024,
      env: { ...process.env, npm_config_fund: 'false', npm_config_audit: 'false' },
    },
  )
}

test('PACKAGE_import_wanxiangshu_main_exits_zero', () => {
  const packDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-pack-import-'))
  const consumerDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-consumer-import-'))
  try {
    const tarball = packTarball(packDir)
    installConsumer(consumerDir, tarball)

    const probe = path.join(consumerDir, 'probe-import.mjs')
    fs.writeFileSync(
      probe,
      'import("wanxiangshu").then(() => { console.log("import ok") }).catch((err) => { console.error(err); process.exit(1) })\n',
      'utf8',
    )

    const output = execFileSync(process.execPath, [probe], {
      cwd: consumerDir,
      encoding: 'utf8',
      maxBuffer: 16 * 1024 * 1024,
    })
    assert.ok(output.includes('import ok'), `stdout must contain import ok, got: ${output}`)
  } finally {
    fs.rmSync(packDir, { recursive: true, force: true })
    fs.rmSync(consumerDir, { recursive: true, force: true })
  }
})
