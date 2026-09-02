import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import {
  PHYSICAL_LISTENER_CONTRACTS,
  scanSemanticDecorators,
} from '../../../scripts/checks/semantic-decorator-invariant.mjs'
import {
  ORDERING_STEPS,
  scanPluginTransforms,
} from '../../../scripts/checks/plugin-transforms-invariant.mjs'

const readFixture = (name) => readFileSync(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')
const semanticDecoratorFcsFixture = fileURLToPath(new URL('./fixtures/semantic-decorator-fcs/', import.meta.url))

test('WHAT[STRUCTURED-WORKFLOW-008] anonymous_retry_is_RED_but_declared_bounded_retry_is_GREEN', () => {
  const anonymous = scanSemanticDecorators(readFixture('anonymous-retry.fs'), 'anonymous-retry.fs')
  assert.ok(anonymous.some((hit) => hit.kind === 'unowned-trace-change'))

  const declared = scanSemanticDecorators(readFixture('declared-retry.fs'), 'declared-retry.fs')
  assert.deepEqual(declared, [])

  const wrongTaxonomy = readFixture('declared-retry.fs')
    .replace('semantic-decorator-retry-bound: 1', 'semantic-decorator-invocation-bound: 2')
  assert.ok(scanSemanticDecorators(wrongTaxonomy, 'declared-retry.fs')
    .some((hit) => hit.kind === 'unowned-trace-change' && hit.message.includes('finite retry bound')))
})

test('WHAT[STRUCTURED-WORKFLOW-008] trace_policy_rename_cannot_hide_an_untyped_invoked_operation', () => {
  const source = [
    'module RenamedRetry',
    'let withRetry action input =',
    '    task {',
    '        try',
    '            return! action input',
    '        with _ ->',
    '            return! action input',
    '    }',
  ].join('\n')
  assert.ok(scanSemanticDecorators(source, 'RenamedRetry.fs').some((hit) => hit.kind === 'unowned-trace-change'))
})

test('WHAT[STRUCTURED-WORKFLOW-008] trace_policy_record_dependency_member_access_is_GREEN', () => {
  const source = readFixture('recovery-record-dependency.fs')
  assert.deepEqual(scanSemanticDecorators(source, 'recovery-record-dependency.fs'), [])
})

test('WHAT[STRUCTURED-WORKFLOW-008] decorator_owner_WHAT_and_exact_proof_are_authoritative', () => {
  const valid = readFixture('declared-retry.fs')
  const cases = [
    valid.replace('semantic-decorator-owner: structured-workflow', 'semantic-decorator-owner: imaginary-owner'),
    valid.replace('semantic-decorator-WHAT: STRUCTURED-WORKFLOW-008', 'semantic-decorator-WHAT: IMAGINARY-999'),
    valid.replace('anonymous_retry_is_RED_but_declared_bounded_retry_is_GREEN', 'title_that_does_not_exist'),
  ]
  for (const source of cases) {
    assert.ok(scanSemanticDecorators(source, 'ForgedDecorator.fs').some((hit) => hit.kind === 'unowned-trace-change'))
  }
})

test('WHAT[STRUCTURED-WORKFLOW-008] distinct_port_sequence_requires_an_honest_invocation_bound', () => {
  const source = readFileSync(new URL('../../../src/Wanxiangshu/Change/Host/GitAdapter.fs', import.meta.url), 'utf8')
  assert.deepEqual(scanSemanticDecorators(source, 'GitAdapter.fs'), [])

  const understated = source.replace(
    'semantic-decorator-invocation-bound: 2',
    'semantic-decorator-invocation-bound: 1',
  )
  assert.ok(scanSemanticDecorators(understated, 'GitAdapter.fs')
    .some((hit) => hit.kind === 'unowned-trace-change' && hit.message.includes('invocation bound covering 2 calls')))
})

test('WHAT[STRUCTURED-WORKFLOW-008] transparent_once_through_scope_is_legal', () => {
  const source = [
    'module Transparent',
    'let withDiagnosticScope operation input =',
    '    task {',
    '        use scope = Diagnostic.openScope "provider"',
    '        return! operation input',
    '    }',
  ].join('\n')
  assert.deepEqual(scanSemanticDecorators(source, 'Transparent.fs'), [])
})

test('WHAT[STRUCTURED-WORKFLOW-008] ordinary_one_shot_callback_in_a_failure_branch_is_legal', () => {
  const source = [
    'module OneShot',
    'let recover projection operation =',
    '    match projection with',
    '    | Error reason -> operation reason',
    '    | Ok value -> Task.FromResult value',
  ].join('\n')
  assert.deepEqual(scanSemanticDecorators(source, 'OneShot.fs'), [])
})

test('WHAT[STRUCTURED-WORKFLOW-008] distinct_callback_calls_are_RED_and_callback_forwarding_is_GREEN', () => {
  const source = readFixture('distinct-callback-calls.fs')
  assert.deepEqual(
    scanSemanticDecorators(source, 'distinct-callback-calls.fs')
      .filter((hit) => hit.kind === 'unowned-trace-change')
      .map((hit) => hit.message.match(/^\w+/)?.[0]),
    ['runDistinct', 'runTypedDistinct'],
  )

  const forwarding = [
    'module Forwarding',
    'let forwardCallback register callback scope = register callback scope',
  ].join('\n')
  assert.deepEqual(scanSemanticDecorators(forwarding, 'Forwarding.fs'), [])
})

test('WHAT[STRUCTURED-WORKFLOW-008] repeated_synchronous_invocation_is_RED_and_forward_once_is_GREEN', () => {
  const source = readFixture('synchronous-callback-calls.fs')
  const hits = scanSemanticDecorators(source, 'synchronous-callback-calls.fs')
    .filter((hit) => hit.kind === 'unowned-trace-change')
  assert.deepEqual(hits.map((hit) => hit.message.match(/^\w+/)?.[0]), ['invokeTwice'])
})

test('WHAT[STRUCTURED-WORKFLOW-008] resolved_pipeline_and_nested_function_port_reinvocation_is_RED', () => {
  const file = 'requirements/structured-workflow/tests/fixtures/semantic-decorator-fcs/ReviewerPipeline.fs'
  const source = readFileSync(join(semanticDecoratorFcsFixture, 'ReviewerPipeline.fs'), 'utf8')
  const application = (startLine, startColumn, endColumn) => ({
    consumerPath: file,
    resolvedTarget: 'operation',
    declarationPaths: [file],
    startLine,
    startColumn,
    endLine: startLine,
    endColumn,
    inferredType: "type 'a -> 'b",
  })
  const applications = [
    application(5, 8, 22),
    application(5, 24, 38),
    application(8, 8, 22),
    application(11, 17, 28),
    application(11, 31, 42),
    application(14, 8, 31),
    application(14, 19, 30),
  ]

  const hits = scanSemanticDecorators(source, file, applications)
    .filter((hit) => hit.kind === 'unowned-trace-change')
  assert.deepEqual(hits.map((hit) => hit.message.match(/^\w+/)?.[0]), [
    'reviewerPipelineTwice',
    'nestedSiblingTwice',
    'nestedSelfTwice',
  ])
  assert.ok(hits.every((hit) => hit.message.includes('invocation bound covering 2 calls')))
  assert.ok(hits.every((hit) => !hit.message.includes('finite retry bound')))
})

test('WHAT[STRUCTURED-WORKFLOW-008] compiler_flow_counts_max_paths_and_excludes_returned_lambdas', () => {
  const file = 'ResolvedPaths.fs'
  const source = [
    'module ResolvedPaths',
    'let exclusive operation choice =',
    '    match choice with',
    '    | true -> operation 1',
    '    | false -> operation 2',
    'let returned operation =',
    '    fun value ->',
    '        operation value',
    '        operation value',
    'let looping operation values =',
    '    for value in values do',
    '        operation value',
  ].join('\n')
  const application = (startLine) => ({
    consumerPath: file,
    resolvedTarget: 'operation',
    declarationPaths: [file],
    startLine,
    startColumn: 8,
    endLine: startLine,
    endColumn: 23,
    inferredType: "type 'a -> unit",
  })
  const applications = [4, 5, 8, 9, 12].map(application)
  const range = (startLine, endLine) => ({ startLine, startColumn: 0, endLine, endColumn: 80 })
  const flowEvidence = {
    matchExpressions: [{ consumerPath: file, clauses: [range(4, 4), range(5, 5)] }],
    conditionalExpressions: [],
    tryExpressions: [],
    lambdaExpressions: [{ consumerPath: file, body: range(8, 9) }],
    loopExpressions: [{ consumerPath: file, body: range(12, 12) }],
  }

  const hits = scanSemanticDecorators(source, file, applications, flowEvidence)
    .filter((hit) => hit.kind === 'unowned-trace-change')
  assert.deepEqual(hits.map((hit) => hit.message.match(/^\w+/)?.[0]), ['looping'])
  assert.match(hits[0].message, /finite retry bound/)
})

test('WHAT[STRUCTURED-WORKFLOW-008] dynamically_mutated_function_handler_collection_is_RED_by_itself', () => {
  const source = readFixture('dynamic-handler-collection.fs')
  assert.ok(scanSemanticDecorators(source, 'dynamic-handler-collection.fs')
    .some((hit) => hit.kind === 'generic-framework'))
})

test('WHAT[STRUCTURED-WORKFLOW-008] unregistered_physical_listener_loop_is_RED_and_exclusive_callbacks_are_GREEN', () => {
  const source = readFixture('non-decorator-callbacks.fs')
  const hits = scanSemanticDecorators(source, 'non-decorator-callbacks.fs')
  assert.ok(hits.some((hit) => hit.kind === 'unowned-trace-change' && hit.message.startsWith('listen ')))
  assert.ok(!hits.some((hit) => hit.message.startsWith('dispatch ')))
})

test('WHAT[STRUCTURED-WORKFLOW-008] repeated_unit_port_and_mutable_unit_handler_collection_are_RED', () => {
  const twice = scanSemanticDecorators(readFixture('unit-callback-twice.fs'), 'unit-callback-twice.fs')
  assert.ok(twice.some((hit) => hit.kind === 'unowned-trace-change' && hit.message.startsWith('twice ')))

  const mutable = scanSemanticDecorators(readFixture('dynamic-unit-handler-collection.fs'), 'dynamic-unit-handler-collection.fs')
  assert.ok(mutable.some((hit) => hit.kind === 'generic-framework'))
})

test('WHAT[STRUCTURED-WORKFLOW-008] Host_physical_listener_fanout_requires_its_exact_registered_site_and_law', () => {
  const contract = PHYSICAL_LISTENER_CONTRACTS.find(({ declaration }) => declaration === 'replayStickyTerminals')
  assert.deepEqual(
    { owner: contract?.owner, what: contract?.what, proof: contract?.proof },
    {
      owner: 'host-boundary',
      what: 'HOST-BOUNDARY-016',
      proof: 'requirements/host-boundary/tests/events-port.test.mjs::WHAT[HOST-BOUNDARY-016] EVT_terminal_notification_fans_out_once_to_each_live_physical_listener',
    },
  )

  const eventsPath = 'src/Wanxiangshu/OpenCode/Host/Events.fs'
  const source = readFileSync(new URL(`../../../${eventsPath}`, import.meta.url), 'utf8')
  assert.deepEqual(scanSemanticDecorators(source, eventsPath), [])

  const movedSite = source.replace(contract.site, contract.site.replace('listener (', 'listener  ('))
  assert.ok(scanSemanticDecorators(movedSite, eventsPath)
    .some((hit) => hit.kind === 'unowned-trace-change' && hit.message.startsWith('replayStickyTerminals ')))

  const collectionContracts = PHYSICAL_LISTENER_CONTRACTS.filter(({ kind }) => kind === 'collection')
  assert.deepEqual(
    collectionContracts.map(({ binding, owner, what }) => [binding, owner, what]),
    [
      ['ptyCompletionObservers', 'delegation', 'DELEG-019'],
      ['mailboxSenders', 'process-execution', 'PROC-003'],
    ],
  )
  for (const collection of collectionContracts) {
    assert.match(collection.proof, /^requirements\/.+\.test\.mjs::WHAT\[/)
    const collectionSource = readFileSync(new URL(`../../../${collection.file}`, import.meta.url), 'utf8')
    assert.deepEqual(scanSemanticDecorators(collectionSource, collection.file), [])
    const staleCollection = collectionSource.replace(collection.sites[1], collection.sites[1].replace('.Add ', '.Add  '))
    assert.ok(scanSemanticDecorators(staleCollection, collection.file).some((hit) => hit.kind === 'generic-framework'))
  }
})

test('WHAT[STRUCTURED-WORKFLOW-008] loop_reinvocation_without_policy_is_RED', () => {
  const source = [
    'module Looping',
    'let poll operation inputs =',
    '    task {',
    '        for input in inputs do',
    '            do! operation input',
    '    }',
  ].join('\n')
  assert.ok(scanSemanticDecorators(source, 'Looping.fs').some((hit) => hit.kind === 'unowned-trace-change'))
})

test('WHAT[STRUCTURED-WORKFLOW-008] generic_and_dynamic_middleware_are_RED', () => {
  const generic = scanSemanticDecorators('type IWorkflowDecorator = abstract Invoke: (unit -> unit) -> unit', 'Generic.fs')
  assert.ok(generic.some((hit) => hit.kind === 'generic-framework'))

  const dynamic = scanSemanticDecorators([
    'module Dynamic',
    'let registerMiddleware middleware = pipeline.Add middleware',
  ].join('\n'), 'Dynamic.fs')
  assert.ok(dynamic.some((hit) => hit.kind === 'generic-framework'))

  const renamed = scanSemanticDecorators(readFixture('generic-handler-framework.fs'), 'generic-handler-framework.fs')
  assert.ok(renamed.some((hit) => hit.kind === 'generic-framework'))
})

test('WHAT[STRUCTURED-WORKFLOW-004] PluginTransforms_accepts_typed_mode_and_static_score', () => {
  const source = [
    'module PluginTransforms',
    'type private TransformMode = ExplicitResumeDisclosure | StrengthReplica | Ordinary',
    'let private determineTransformMode value = Ordinary',
    'let normalTransform value =',
    ...ORDERING_STEPS.map((step) => `    ${step} value`),
    'let dispatch value =',
    '    match determineTransformMode value with',
    '    | ExplicitResumeDisclosure -> value',
    '    | StrengthReplica -> value',
    '    | Ordinary -> value',
  ].join('\n')
  assert.deepEqual(scanPluginTransforms(source, 'PluginTransforms.fs'), [])
})

test('WHAT[STRUCTURED-WORKFLOW-004] PluginTransforms_order_requires_executable_calls', () => {
  const source = readFixture('fake-plugin-transform-order.fs')
  assert.ok(scanPluginTransforms(source, 'fake-plugin-transform-order.fs').some((hit) => hit.kind === 'ordering'))

  const dormant = readFixture('dormant-plugin-transform-order.fs')
  assert.ok(scanPluginTransforms(dormant, 'dormant-plugin-transform-order.fs').some((hit) => hit.kind === 'ordering'))
})


