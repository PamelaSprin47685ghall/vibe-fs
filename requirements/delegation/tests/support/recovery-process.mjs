import assert from 'node:assert/strict'
import { mkdtempSync, mkdirSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { runVerificationToolProbe } from '../../../../scripts/lib/verification-tool-probe.mjs'
import { createIsolatedEnv } from '../../../verification-system/tests/e2e/support/isolated-env.js'

export async function runRecoveryProcess(scenario, signal, diagnostic) {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-sync-recovery-'))
  const workspace = join(directory, 'workspace')
  const receiptPath = join(directory, 'receipt.json')
  mkdirSync(workspace)
  const env = {
    ...process.env,
    ...createIsolatedEnv({ scenarioDir: directory, llmUrl: 'http://127.0.0.1:0/v1' }),
  }
  delete env.NODE_TEST_CONTEXT
  try {
    await runVerificationToolProbe(process.execPath, [
      fileURLToPath(new URL('./recovery-process.fixture.mjs', import.meta.url)),
      workspace,
      scenario,
      receiptPath,
    ], { cwd: process.cwd(), env, signal })
    const receipt = JSON.parse(readFileSync(receiptPath, 'utf8'))
    assert.notEqual(receipt.pid, process.pid, 'routing initialization runs in an independent process')
    assert.throws(() => process.kill(receipt.pid, 0), error => error.code === 'ESRCH', 'the actual recovery process has exited')
    assert.equal(receipt.scenario, scenario)
    return receipt
  } catch (error) {
    if (typeof error.stderr === 'string') {
      diagnostic?.(error.stderr)
      error.message += `\n${error.stderr}`
    }
    throw error
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
}
