import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

import * as HookPolicySurface from '../../../dist/OpenCode/Host/HookPolicySurface.js'

const ROOT = fileURLToPath(new URL('../../..', import.meta.url))
const read = (path) => readFileSync(join(ROOT, path), 'utf8')
const policySource = read('src/Wanxiangshu/OpenCode/Host/HookPolicy.fs')
const policySurfaceSource = read('src/Wanxiangshu/OpenCode/Host/HookPolicySurface.fs')
const hooksSource = read('src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs')

const metadataRows = [...policySource.matchAll(/\| HookKey\.([A-Z][A-Za-z]+) ->\s*\n\s*\{ HostKey = "([^"]+)"/g)]
const registrations = [...hooksSource.matchAll(/registeredHook\s+HookKey\.([A-Z][A-Za-z]+)/g)]

const rowNames = metadataRows.map((match) => match[1])
const rowKeys = metadataRows.map((match) => match[2])
const registeredNames = registrations.map((match) => match[1])

test('WHAT[HOST-BOUNDARY-024] registered Hook keys and closed policy rows have exact one-to-one closure', () => {
  assert.deepEqual(registeredNames.slice().sort(), rowNames.slice().sort())
  assert.equal(new Set(rowNames).size, rowNames.length)
  assert.equal(new Set(rowKeys).size, rowKeys.length)
  assert.equal(new Set(registeredNames).size, registeredNames.length)
  assert.equal(rowKeys.includes('tool'), false, 'the tool collection is not a Host Hook')
})

test('WHAT[HOST-BOUNDARY-024] registration is explicit static composition through the policy score', () => {
  assert.doesNotMatch(hooksSource, /hooks\?[^\s]+\s*<-/)
  assert.doesNotMatch(hooksSource, /\bpolicyAwareHook\b/)
  assert.doesNotMatch(hooksSource, /List\.(?:map|fold|iter).*registeredHook/)
  assert.equal(registrations.length, metadataRows.length)
})

test('WHAT[HOST-BOUNDARY-024] rejects degradable security or workflow hook', () => {
  assert.equal(HookPolicySurface.acceptsPolicy('Security', 'BestEffortDiagnostic'), false)
  assert.equal(HookPolicySurface.acceptsPolicy('Workflow', 'BestEffortDiagnostic'), false)
  assert.equal(HookPolicySurface.acceptsPolicy('Invariant', 'BestEffortDiagnostic'), false)
  assert.equal(HookPolicySurface.acceptsPolicy('AuditOnly', 'BestEffortDiagnostic'), true)
})

test('WHAT[HOST-BOUNDARY-024] Hook authority cannot express identity mutation or admission bypass', () => {
  assert.doesNotMatch(policySource, /MutateIdentity|BypassAdmission/)
  for (const row of HookPolicySurface.rows()) {
    assert.ok(['NoIdentityAccess', 'ObserveIdentity'].includes(row.identity))
    assert.ok(['NoAdmissionAccess', 'OwnedAdmissionGate'].includes(row.admission))
  }
})

test('WHAT[HOST-BOUNDARY-024] optional Casebook failure preserves the critical result and emits the existing diagnostic', () => {
  assert.match(
    policySurfaceSource,
    /HookPolicy\.observeOptional Diagnostic\.emit OptionalHookEffect\.CasebookObservation effect/,
    'the registered surface must inject the existing diagnostic sink',
  )
  const previous = process.env.WANXIANGSHU_DIAG
  process.env.WANXIANGSHU_DIAG = '1'
  const originalWrite = process.stderr.write.bind(process.stderr)
  const captured = []
  process.stderr.write = (chunk) => { captured.push(String(chunk)); return true }

  try {
    const criticalResult = { accepted: true }
    const result = HookPolicySurface.runOptionalCasebookEffect(criticalResult, () => {
      throw new Error('casebook unavailable')
    })

    assert.equal(result, criticalResult)
    assert.equal(captured.some((line) => line.includes('plugin-hook-casebook-observation-failed')), true)
  } finally {
    process.stderr.write = originalWrite
    if (previous === undefined) delete process.env.WANXIANGSHU_DIAG
    else process.env.WANXIANGSHU_DIAG = previous
  }
})
