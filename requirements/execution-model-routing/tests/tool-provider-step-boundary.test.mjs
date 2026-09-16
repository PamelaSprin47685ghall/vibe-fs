import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const root = new URL('../../../', import.meta.url)
const source = (path) => readFile(new URL(path, root), 'utf8')

test('WHAT[EMR-010] EMR_010_managed_tool_execution_ends_the_current_provider_step_before_tool_body', async () => {
  const [binding, registry] = await Promise.all([
    source('src/Wanxiangshu/OpenCode/Host/SessionExecutionBinding.fs'),
    source('src/Wanxiangshu/OpenCode/Tools/ToolRegistry.fs'),
  ])

  assert.match(
    binding,
    /let endProviderStepAtToolBoundary[\s\S]*ProviderRunIdentity option[\s\S]*ModelRouting\.endProviderStep/,
    'the exact provider-attempt binding owns conversion from tool context identity to capacity step end',
  )

  const boundaryCall = 'SessionExecutionBinding.endProviderStepAtToolBoundary'
  const boundaryIndex = registry.indexOf(boundaryCall)

  assert.ok(boundaryIndex >= 0, 'ToolRegistry must cross the provider→tool capacity boundary')
  assert.match(
    registry,
    /let providerToolBoundary[\s\S]*SessionExecutionBinding\.endProviderStepAtToolBoundary/,
    'provider→tool handoff is a named outer execution stage',
  )
  assert.match(
    registry,
    /match providerToolBoundary ctx with[\s\S]*\| Ok\(\) -> return! execute(?:Tracked|AfterBoundary) args ctx/,
    'all later gates execute only after the provider boundary succeeds',
  )
  assert.match(
    registry,
    /let executeAfterBoundary[\s\S]*if isStrengthReplica ctx then[\s\S]*else[\s\S]*return! executeEstablished args ctx/,
    'strength and role/admission gates remain downstream of the provider boundary',
  )
  assert.match(
    registry,
    /match spec\.Admission with[\s\S]*OfficeRole[\s\S]*executeOffice[\s\S]*PrivateAttachment[\s\S]*executePrivateAttachment/,
    'the declared tool authority, not a guessed one, selects the admission path downstream of the boundary',
  )
  assert.match(
    registry,
    /let executeKnownRole[\s\S]*officeAdmission ctx role[\s\S]*return! original args ctx/,
    'after the outer handoff and gates, ToolRegistry still delegates to the original tool body',
  )
  assert.match(
    registry,
    /let executePrivateAttachment[\s\S]*if attachmentAdmission ctx then[\s\S]*return! original args ctx/,
    'an internal leaf tool also reaches the original body only after the provider boundary',
  )

  const boundarySlice = registry.slice(Math.max(0, boundaryIndex - 500), boundaryIndex + 1000)
  assert.doesNotMatch(
    boundarySlice,
    /DateTime|setTimeout|timer|sleep|TimeoutMs|milliseconds?/i,
    'provider→tool handoff is causal and must not depend on elapsed time',
  )
})
