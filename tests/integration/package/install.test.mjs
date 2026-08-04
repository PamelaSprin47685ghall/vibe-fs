// tests/integration/package/install.test.mjs — isolated consumer npm install.
//
// Packs the repo, installs the tarball into a fresh package root.
// Requires dist/ built. Network may be needed for package dependencies.
//   node --test tests/integration/package/install.test.mjs

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
  assert.equal(typeof entry.filename, 'string')
  const tarball = path.join(packDir, entry.filename)
  assert.ok(fs.existsSync(tarball), `tarball missing: ${tarball}`)
  return tarball
}

test('PACKAGE_install_tarball_into_consumer_node_modules', () => {
  const packDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-pack-install-'))
  const consumerDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wxs-consumer-install-'))
  try {
    const tarball = packTarball(packDir)

    fs.writeFileSync(
      path.join(consumerDir, 'package.json'),
      JSON.stringify({ name: 'consumer', private: true }),
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

    const installed = path.join(consumerDir, 'node_modules', 'wanxiangshu')
    assert.ok(fs.existsSync(installed), 'node_modules/wanxiangshu must exist after install')
    assert.ok(
      fs.existsSync(path.join(installed, 'package.json')),
      'installed package must contain package.json',
    )
    assert.ok(
      fs.existsSync(path.join(installed, 'dist', 'Infrastructure', 'OpenCode', 'Plugin', 'Plugin.js')),
      'installed package must contain main entry under dist/',
    )
  } finally {
    fs.rmSync(packDir, { recursive: true, force: true })
    fs.rmSync(consumerDir, { recursive: true, force: true })
  }
})
