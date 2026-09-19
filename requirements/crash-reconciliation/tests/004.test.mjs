import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'
import { mkdtempSync as recoveryMkdtemp, rmSync as recoveryRm } from 'node:fs'
import { tmpdir as recoveryTmpdir } from 'node:os'
import { join as recoveryJoin } from 'node:path'
import * as recoveryHost from '../../../dist/OpenCode/Host/SessionRecoveryHostSurface.js'

const ROOT = new URL('../../../', import.meta.url).pathname

const withContinueHost = async (label, portOutcome, action) => {
  const directory = recoveryMkdtemp(recoveryJoin(recoveryTmpdir(), `wxs-continue-${label}-`))
  const host = await recoveryHost.bootRecoveryHost(directory, portOutcome)

  try {
    await action(host)
  } finally {
    recoveryHost.disposeRecoveryHost(host)
    recoveryRm(directory, { recursive: true, force: true })
  }
}

const continueSessionOf = (suffix) => `ses-continue-${suffix}`

const continuePhysicalOf = (suffix) => `msg-continue-${suffix}`

test('WHAT[crash-reconciliation-004] RECOVERY_FAMILY_dsl_module_and_private_permit_exist', () => {
  const src = readFileSync(join(ROOT, 'src/Wanxiangshu/Execution/Session/Recovery/Model.fs'), 'utf8')
  assert.match(src, /module SessionRecovery/)
  assert.match(src, /type FamilyRecoveryPermit\s*=\s*\n\s*private/)
  assert.match(src, /authorizeFamilyResume/)
  assert.doesNotMatch(src, /fromTask|Flow\.lift/)
})
