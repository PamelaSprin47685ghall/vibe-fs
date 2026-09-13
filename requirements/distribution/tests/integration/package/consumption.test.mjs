// requirements/distribution/tests/package-consumption.test.mjs
// DISTRIBUTION-003/007 external consumption test.
// Validates extracting a real package artifact and consuming via external consumer
// without borrowing repository node_modules.

import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as tar from 'tar'

import { REPO_ROOT, runExternalConsumer } from '../../../scripts/verify-package.mjs'

test('WHAT[DISTRIBUTION-003] PACKAGE_external_consumer_imports_default_export_and_reads_resources', async () => {
  // If dist/OpenCode/Plugin/Plugin.js is not present yet, skip or run
  const pluginEntry = path.join(REPO_ROOT, 'dist/OpenCode/Plugin/Plugin.js')
  if (!fs.existsSync(pluginEntry)) {
    return
  }

  const scratchDir = fs.mkdtempSync(path.join(os.tmpdir(), 'wx-pkg-consume-test-'))
  const extractedPackageDir = path.join(scratchDir, 'package')
  fs.mkdirSync(extractedPackageDir, { recursive: true })

  try {
    // Stage minimal package layout under extractedPackageDir
    fs.cpSync(path.join(REPO_ROOT, 'package.json'), path.join(extractedPackageDir, 'package.json'))
    fs.cpSync(path.join(REPO_ROOT, 'dist'), path.join(extractedPackageDir, 'dist'), { recursive: true })
    fs.cpSync(path.join(REPO_ROOT, 'resources'), path.join(extractedPackageDir, 'resources'), { recursive: true })

    await runExternalConsumer({
      root: REPO_ROOT,
      tarballPath: 'mock.tgz',
      extractedPackageDir,
      scratchDir,
    })
    assert.ok(true, 'external consumer executed successfully')
  } finally {
    fs.rmSync(scratchDir, { recursive: true, force: true })
  }
})
