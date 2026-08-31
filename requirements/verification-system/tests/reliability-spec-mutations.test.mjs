import assert from 'node:assert/strict'
import { copyFileSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'
import test from 'node:test'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..')

test('WHAT[VERIFICATION-SYSTEM-004] spec gate rejects duplicate CHATEXEC identifiers', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'spec-duplicate-id-'))

  try {
    const checkerDirectory = join(fixture, 'scripts/checks')
    mkdirSync(checkerDirectory, { recursive: true })
    copyFileSync(join(ROOT, 'scripts/checks/spec.mjs'), join(checkerDirectory, 'spec.mjs'))
    copyFileSync(join(ROOT, 'scripts/checks/spec-rules.mjs'), join(checkerDirectory, 'spec-rules.mjs'))

    for (const rootDocument of ['AGENTS.md', 'README.md', 'CHANGELOG.md']) {
      writeFileSync(join(fixture, rootDocument), '')
    }

    for (const packageName of ['chat-execution-a', 'chat-execution-b']) {
      const packageDirectory = join(fixture, 'requirements', packageName)
      mkdirSync(packageDirectory, { recursive: true })
      writeFileSync(
        join(packageDirectory, 'WHAT.md'),
        `# ${packageName} — WHAT\n\n## CHATEXEC-001: duplicate fixture clause\n`,
      )
    }

    const result = spawnSync(process.execPath, [join(checkerDirectory, 'spec.mjs')], {
      cwd: fixture,
      encoding: 'utf8',
    })
    const output = `${result.stdout ?? ''}${result.stderr ?? ''}`

    assert.ifError(result.error)
    assert.equal(typeof result.status, 'number', `spec gate did not exit normally:\n${output}`)
    assert.notEqual(result.status, 0, `spec gate accepted duplicate CHATEXEC-001 definitions:\n${output}`)
    assert.match(output, /条款 ID 重复定义：CHATEXEC-001/)
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
