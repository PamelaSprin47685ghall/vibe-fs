import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { tmpdir } from 'node:os'
import test from 'node:test'

import { scanRetryOwnership } from '../../../scripts/checks/retry-owner.mjs'

const fixture = (mutationPath, mutation) => {
  const root = mkdtempSync(join(tmpdir(), 'retry-owner-'))
  const source = join(root, 'src/Wanxiangshu')
  const write = (path, text) => {
    const target = join(source, path)
    mkdirSync(dirname(target), { recursive: true })
    writeFileSync(target, text)
  }

  write('Execution/Failure/Policy.fs', 'module ExecutionFailurePolicy =\n    let decide input = input\n')
  write('Execution/Failure/Model.fs', 'type ProviderRecoveryAuthorization private (value: string) = class end\n')
  write(mutationPath, mutation)
  return { root, close: () => rmSync(root, { recursive: true, force: true }) }
}

test('WHAT[PAR-019] rejects nested physical retry owner', () => {
  const fx = fixture(
    'Interaction/Dispatch/NestedRetry.fs',
    'let resend port =\n    for attempt in [ 1; 2 ] do\n        port.SendPrompt(sessionId, text, options)\n',
  )
  try {
    assert.match(scanRetryOwnership(fx.root).join('\n'), /nested under a local retry loop/)
  } finally {
    fx.close()
  }
})

test('WHAT[PAR-019] rejects retry classification from diagnostic text', () => {
  const fx = fixture(
    'Interaction/Dispatch/StringRetry.fs',
    'let retry error =\n    if error.Contains("timeout") then fallback ()\n',
  )
  try {
    assert.match(scanRetryOwnership(fx.root).join('\n'), /must not parse text/)
  } finally {
    fx.close()
  }
})
