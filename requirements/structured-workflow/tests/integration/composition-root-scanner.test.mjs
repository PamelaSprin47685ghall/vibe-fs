import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'

import {
  scanCompositionRootApplications,
  scanRepo,
} from '../../../../scripts/checks/composition-root-invariant.mjs'

test('WHAT[STRUCTURED-WORKFLOW-004] real_composition_root_scanner_is_GREEN', () => {
  const applicationUses = scanCompositionRootApplications()
  const resolvedCeCall = applicationUses.find((application) =>
    application.consumerPath === 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs'
      && application.sourceAnchor === 'continueStartedLifecycle'
      && application.declarationPaths.includes(application.consumerPath))
  assert.ok(resolvedCeCall)
  const rootSource = readFileSync(new URL(`../../../../${resolvedCeCall.consumerPath}`, import.meta.url), 'utf8')
  assert.match(rootSource.split('\n')[resolvedCeCall.startLine - 1], /\bdo!\s+continueStartedLifecycle\b/)

  assert.deepEqual(scanRepo(undefined, applicationUses), [])
})
