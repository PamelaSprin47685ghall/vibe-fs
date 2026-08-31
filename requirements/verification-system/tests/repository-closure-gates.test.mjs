import assert from 'node:assert/strict'
import { copyFileSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { spawnSync } from 'node:child_process'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

const repositoryRoot = fileURLToPath(new URL('../../..', import.meta.url))
const write = (root, path, text) => {
  const target = join(root, path)
  mkdirSync(join(target, '..'), { recursive: true })
  writeFileSync(target, text)
}

const runNode = (root, args) => {
  const env = { ...process.env }
  delete env.NODE_TEST_CONTEXT
  return spawnSync(process.execPath, args, {
    cwd: root,
    encoding: 'utf8',
    env,
  })
}

test('WHAT[VERIFICATION-SYSTEM-009] repository closure gates reject a missing semantic owner and package member', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'repository-closure-'))

  try {
    mkdirSync(join(fixture, 'scripts/checks'), { recursive: true })
    mkdirSync(join(fixture, 'scripts/lib'), { recursive: true })
    copyFileSync(
      join(repositoryRoot, 'scripts/checks/semantic-owners.mjs'),
      join(fixture, 'scripts/checks/semantic-owners.mjs'),
    )
    copyFileSync(
      join(repositoryRoot, 'scripts/lib/walk.mjs'),
      join(fixture, 'scripts/lib/walk.mjs'),
    )
    write(fixture, 'scripts/checks/semantic-owners.json', JSON.stringify({ owners: ['distribution'], ownership: [] }))
    write(fixture, 'src/Wanxiangshu/Unowned.fs', 'namespace ClosureFixture\n')

    const ownerGate = runNode(fixture, ['scripts/checks/semantic-owners.mjs'])
    assert.equal(ownerGate.status, 1, ownerGate.stderr || ownerGate.stdout)
    assert.match(ownerGate.stderr, /UNMANIFESTED production files/)
    assert.match(ownerGate.stderr, /src\/Wanxiangshu\/Unowned\.fs/)

    mkdirSync(join(fixture, 'requirements/distribution/tests'), { recursive: true })
    copyFileSync(
      join(repositoryRoot, 'requirements/distribution/tests/pack-closure.test.mjs'),
      join(fixture, 'requirements/distribution/tests/pack-closure.test.mjs'),
    )
    write(fixture, 'package.json', JSON.stringify({
      main: './dist/OpenCode/Plugin/Plugin.js',
      exports: { '.': './dist/OpenCode/Plugin/Plugin.js' },
      files: ['resources/'],
      scripts: {},
    }))

    const packageGate = runNode(fixture, [
      '--test',
      '--test-name-pattern=DISTRIBUTION_files_whitelist_is_explicit',
      'requirements/distribution/tests/pack-closure.test.mjs',
    ])
    assert.equal(packageGate.status, 1, packageGate.stderr || packageGate.stdout)
    assert.match(`${packageGate.stdout}\n${packageGate.stderr}`, /files whitelist must include dist/)
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
