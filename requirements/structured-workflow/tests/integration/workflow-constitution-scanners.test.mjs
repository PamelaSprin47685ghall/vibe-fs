// Real-scan proofs for the workflow constitution.
//
// These lanes drive the FCS-backed scanners against a real F# project — the whole
// production tree for the composition-root, plugin and decorator scanners, and the
// semantic-decorator fixture project for the resolved-application evidence. One lane
// costs a real F# project check (5s to ~110s). The unit tier's verdict-silence budget
// is 5s by law, which no FCS lane can satisfy; the integration orchestrator owns its
// own silence criterion, so the real-scan lanes live here while every fixture-level
// proof stays in tests/workflow-constitution.test.mjs.

import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  scanCompositionRootApplications,
  scanRepo as scanRootRepo,
} from '../../../../scripts/checks/composition-root-invariant.mjs'
import {
  scanSemanticDecoratorEvidence,
  scanSemanticDecorators,
  scanRepo as scanDecoratorRepo,
} from '../../../../scripts/checks/semantic-decorator-invariant.mjs'
import { scanRepo as scanPluginRepo } from '../../../../scripts/checks/plugin-transforms-invariant.mjs'

const semanticDecoratorFcsFixture = fileURLToPath(new URL('../fixtures/semantic-decorator-fcs/', import.meta.url))
const repositoryScratchRoot = fileURLToPath(new URL('../../../../.fable-build/', import.meta.url))

test('WHAT[STRUCTURED-WORKFLOW-004] real_composition_root_scanner_is_GREEN', () => {
  const applicationUses = scanCompositionRootApplications()
  const resolvedCeCall = applicationUses.find((application) =>
    application.consumerPath === 'src/Wanxiangshu/OpenCode/Host/HostSignalBootstrap.fs'
      && application.sourceAnchor === 'continueStartedLifecycle'
      && application.declarationPaths.includes(application.consumerPath))
  assert.ok(resolvedCeCall)
  const rootSource = readFileSync(new URL(`../../../../${resolvedCeCall.consumerPath}`, import.meta.url), 'utf8')
  assert.match(rootSource.split('\n')[resolvedCeCall.startLine - 1], /\bdo!\s+continueStartedLifecycle\b/)

  assert.deepEqual(scanRootRepo(undefined, applicationUses), [])
})

test('WHAT[STRUCTURED-WORKFLOW-004] real_plugin_and_decorator_scanners_are_GREEN', () => {
  assert.deepEqual(scanPluginRepo(), [])
  assert.deepEqual(scanDecoratorRepo(), [])
})

test('WHAT[STRUCTURED-WORKFLOW-008] real_FCS_resolves_pipeline_and_nested_function_port_applications', () => {
  mkdirSync(repositoryScratchRoot, { recursive: true })
  const scratchRoot = mkdtempSync(join(repositoryScratchRoot, 'semantic-decorator-fixture-'))
  try {
    const file = 'requirements/structured-workflow/tests/fixtures/semantic-decorator-fcs/ReviewerPipeline.fs'
    const evidence = scanSemanticDecoratorEvidence({
      projectFile: join(semanticDecoratorFcsFixture, 'Fixture.fsproj'),
      productionRoot: semanticDecoratorFcsFixture,
      scratchRoot,
      resultPath: join(scratchRoot, 'symbol-uses.json'),
    })
    const applications = evidence.applicationUses
    const resolvedOperationCalls = applications.filter((application) =>
      application.consumerPath === file && application.resolvedTarget === 'operation')
    assert.equal(resolvedOperationCalls.length, 12)
    assert.ok(resolvedOperationCalls.every((application) => application.declarationPaths.includes(file)))
    assert.equal(evidence.lambdaExpressions.find((lambda) =>
      lambda.consumerPath === file && lambda.startLine === 17)?.invokedBy.length, 1)
    assert.equal(evidence.lambdaExpressions.find((lambda) =>
      lambda.consumerPath === file && lambda.startLine === 27)?.invokedBy.length, 0)
    assert.deepEqual(evidence.localFunctionBindings.find((binding) =>
      binding.consumerPath === file && binding.name === 'run')?.invokedBy.map(({ startLine }) => startLine), [24])

    const source = readFileSync(join(semanticDecoratorFcsFixture, 'ReviewerPipeline.fs'), 'utf8')
    const hits = scanSemanticDecorators(source, file, applications, evidence)
      .filter((hit) => hit.kind === 'unowned-trace-change')
    assert.deepEqual(hits.map((hit) => hit.message.match(/^\w+/)?.[0]), [
      'reviewerPipelineTwice',
      'nestedSiblingTwice',
      'nestedSelfTwice',
      'immediateLambdaTwice',
      'localInvokedTwice',
    ])
  } finally {
    rmSync(scratchRoot, { recursive: true, force: true })
  }
})
