import assert from 'node:assert/strict'
import { mkdirSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'

import {
  scanSemanticDecoratorEvidence,
  scanSemanticDecorators,
} from '../../../../scripts/checks/semantic-decorator-invariant.mjs'

const semanticDecoratorFcsFixture = fileURLToPath(new URL('../fixtures/semantic-decorator-fcs/', import.meta.url))
const repositoryScratchRoot = fileURLToPath(new URL('../../../../.fable-build/', import.meta.url))

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
