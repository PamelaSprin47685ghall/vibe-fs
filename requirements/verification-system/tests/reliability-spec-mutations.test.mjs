import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'

import { duplicateClauseDefinitions } from '../../../scripts/lib/spec-rules.mjs'

test('WHAT[VERIFICATION-SYSTEM-004] spec gate rejects duplicate CHATEXEC identifiers', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'spec-duplicate-id-'))

  try {
    const entries = []
    for (const packageName of ['chat-execution-a', 'chat-execution-b']) {
      const packageDirectory = join(fixture, 'requirements', packageName)
      mkdirSync(packageDirectory, { recursive: true })
      const file = join(packageDirectory, 'WHAT.md')
      writeFileSync(
        file,
        `# ${packageName} — WHAT\n\n## CHATEXEC-001: duplicate fixture clause\n`,
      )
      entries.push({ file, pkg: packageName, text: readFileSync(file, 'utf8') })
    }

    const findings = duplicateClauseDefinitions(entries)
    assert.ok(
      findings.length > 0,
      'duplicateClauseDefinitions accepted duplicate CHATEXEC-001 definitions',
    )
    assert.match(
      findings.map((finding) => finding.msg).join('\n'),
      /条款 ID 重复定义：CHATEXEC-001/,
    )

    assert.deepEqual(
      duplicateClauseDefinitions([entries[0]]),
      [],
      'a single CHATEXEC-001 definition must not fail closed',
    )
  } finally {
    rmSync(fixture, { recursive: true, force: true })
  }
})
