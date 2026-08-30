import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { scanText } from '../../../scripts/checks/dsl-ownership.mjs'

const readFixture = (name) => readFileSync(new URL(`./fixtures/${name}`, import.meta.url), 'utf8')

test('WHAT[STRUCTURED-WORKFLOW-001] FLOW_001_direct_task_workflow_is_allowed', () => {
  const source = [
    'module Workflow',
    'let run readSnapshot = task {',
    '    let! snapshot = readSnapshot ()',
    '    return snapshot',
    '}',
  ].join('\n')

  assert.deepEqual(scanText(source, 'Application/Workflow.fs'), [])
})

test('WHAT[STRUCTURED-WORKFLOW-002] FLOW_006_second_runtime_patterns_are_rejected', () => {
  const samples = [
    ['type WorkflowCommand =', 'second-runtime-protocol'],
    ['type WorkflowReply =', 'second-runtime-protocol'],
    ['    | Step of WorkflowCommand * (WorkflowReply -> Workflow)', 'second-runtime-protocol'],
    ["    | Suspend of 'instruction * (obj -> Workflow)", 'second-runtime-protocol'],
    ['module WorkflowInterpreter =', 'business-interpreter'],
  ]

  for (const [source, expectedGate] of samples) {
    assert.deepEqual(
      scanText(source, 'Application/Workflow.fs').map(({ gate }) => gate),
      [expectedGate],
      source,
    )
  }
})

test('WHAT[STRUCTURED-WORKFLOW-006] FLOW_017_composition_keeps_domain_results_and_rejects_child_program_counters', () => {
  const composed = [
    'module ParentWorkflow',
    'let run child = task {',
    '    let! outcome = child ()',
    '    match outcome with',
    '    | Approved verdict -> return verdict',
    '    | Rejected reason -> return raise (System.Exception reason)',
    '}',
  ].join('\n')

  assert.deepEqual(scanText(composed, 'Mission/Parent/Workflow.fs'), [])

  const leakedProgramCounter = [
    'module ChildWorkflow',
    'type ChildProgress = { CurrentStage: bool }',
    'let advance state = state',
  ].join('\n')

  assert.ok(
    scanText(leakedProgramCounter, 'Mission/Child/Workflow.fs').some(({ gate }) => gate === 'program-counter'),
    'a child workflow must not expose execution position for its parent to drive',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] stored_and_cross_module_execution_positions_are_rejected', () => {
  const samples = [
    'type Child = { NextAction: string }',
    'type Child = { NextStep: string }',
    'type Child = { ResumeAtStage: string }',
    'type Child = { StepIndex: int }',
    'type Child = { ContinueToken: string }',
    'let resumeAtCheckpoint = 3',
  ]
  for (const declaration of samples) {
    const source = ['module ChildSurface', declaration].join('\n')
    assert.ok(
      scanText(source, 'src/Wanxiangshu/Mission/Child/Surface.fs').some(({ gate }) => gate === 'program-counter'),
      declaration,
    )
  }
})

test('WHAT[STRUCTURED-WORKFLOW-003] positively_classified_external_NextAction_is_not_a_PC', () => {
  const source = [
    'module Protocol',
    '/// DSL-class: ExternalSignal — external wire response, not an execution cursor.',
    'type ErrorView =',
    '    { Code: string',
    '      NextAction: string }',
  ].join('\n')
  assert.deepEqual(scanText(source, 'src/Wanxiangshu/Sphinx/Wire.fs'), [])
})

test('WHAT[STRUCTURED-WORKFLOW-003] flow_driving_stored_positions_cannot_evade_by_rename', () => {
  for (const field of [
    'ContinuationAddress',
    'ResumePoint',
    'CurrentInstruction',
    'OpaqueOrdinal',
  ]) {
    const source = [
      'module ChildSurface',
      `type Child = { ${field}: int }`,
      'let resume child =',
      `    match child.${field} with`,
      '    | 0 -> validate ()',
      '    | _ -> dispatch ()',
    ].join('\n')
    assert.ok(
      scanText(source, 'src/Wanxiangshu/Mission/Child/Surface.fs').some(
        ({ gate }) => gate === 'program-counter',
      ),
      field,
    )
  }

  const source = [
    'module ChildSurface',
    'type Instruction =',
    '    | Validate',
    '    | Dispatch',
    'type Child = { OpaqueChoice: Instruction }',
    'let resume child =',
    '    match child.OpaqueChoice with',
    '    | Validate -> validate ()',
    '    | Dispatch -> dispatch ()',
  ].join('\n')
  assert.ok(
    scanText(source, 'src/Wanxiangshu/Mission/Child/Surface.fs').some(
      ({ gate }) => gate === 'program-counter',
    ),
    'a DU-typed stored instruction cannot evade by renaming the field',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] multiline_record_cursor_cannot_evade_program_counter_gate', () => {
  const hits = scanText(
    readFixture('execution-position-multiline.fs'),
    'src/Wanxiangshu/Mission/Child/Surface.fs',
  )
  assert.ok(
    hits.some(({ gate }) => gate === 'program-counter'),
    'a standard record whose opening brace follows = must retain its branch-driving field',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] every_branch_discriminant_type_cannot_evade_by_rename', () => {
  for (const fixture of [
    'execution-position-string.fs',
    'execution-position-bool.fs',
    'execution-position-foreign-du.fs',
  ]) {
    const hits = scanText(readFixture(fixture), 'src/Wanxiangshu/Mission/Child/Surface.fs')
    assert.ok(
      hits.some(({ gate }) => gate === 'program-counter'),
      `${fixture}: branch evidence must not depend on a field or type spelling`,
    )
  }
})

test('WHAT[STRUCTURED-WORKFLOW-003] exported_discriminant_cannot_select_a_returned_operation', () => {
  const hits = scanText(
    readFixture('execution-position-returned-function.fs'),
    'src/Wanxiangshu/Reviewer/Surface.fs',
  )
  assert.ok(
    hits.some(({ gate }) => gate === 'program-counter'),
    'Cursor.Address selecting runA/runB for another caller is a stored execution position',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] exported_discriminant_cannot_drive_resolved_member_calls', () => {
  const hits = scanText(
    readFixture('execution-position-member-call.fs'),
    'src/Wanxiangshu/Reviewer/Surface.fs',
  )
  assert.ok(
    hits.some(({ gate }) => gate === 'program-counter'),
    'a lower-case receiver member call such as port.Send is executable branch evidence',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] compiler_resolved_cross_module_flow_is_rejected', () => {
  const file = 'src/Wanxiangshu/Reviewer/Surface.fs'
  const source = readFixture('execution-position-resolved-cross-module.fs')
  const functionEvidence = {
    symbolUses: [
      {
        consumerPath: file,
        symbol: 'Foreign.runA',
        line: 8,
        column: 11,
        inferredType: 'Microsoft.FSharp.Core.unit -> System.String',
      },
      {
        consumerPath: file,
        symbol: 'Foreign.runB',
        line: 9,
        column: 11,
        inferredType: 'Microsoft.FSharp.Core.unit -> System.String',
      },
    ],
    applicationUses: [],
  }
  assert.ok(
    scanText(source, file, functionEvidence).some(({ gate }) => gate === 'program-counter'),
    'resolved Foreign.runA/runB values selected for a caller must fail',
  )

  const memberEvidence = {
    symbolUses: [],
    applicationUses: [13, 14].map((line) => ({
      consumerPath: file,
      resolvedTarget: 'Foreign.Port.Send',
      startLine: line,
      inferredType: 'System.String -> Microsoft.FSharp.Core.unit',
    })),
  }
  assert.ok(
    scanText(source, file, memberEvidence).some(({ gate }) => gate === 'program-counter'),
    'a member declared in another file remains executable branch evidence',
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] non_branching_domain_fields_are_not_program_counters', () => {
  assert.deepEqual(
    scanText(
      readFixture('execution-position-domain-fields.fs'),
      'src/Wanxiangshu/Domain/Profile.fs',
    ),
    [],
  )
})

test('WHAT[STRUCTURED-WORKFLOW-003] immutable_domain_discriminants_stay_green', () => {
  assert.deepEqual(
    scanText(
      readFixture('execution-position-domain-branch.fs'),
      'src/Wanxiangshu/Domain/Profile.fs',
    ),
    [],
  )

  const file = 'src/Wanxiangshu/Domain/Profile.fs'
  const source = [
    'type Profile = { IsVerified: bool }',
    'let badge profile =',
    '    match profile.IsVerified with',
    '    | true -> Foreign.badge "verified"',
    '    | false -> Foreign.badge "unverified"',
  ].join('\n')
  const compilerEvidence = {
    symbolUses: [],
    applicationUses: [4, 5].map((line) => ({
      consumerPath: file,
      resolvedTarget: 'Foreign.badge',
      startLine: line,
      inferredType: 'System.String -> Domain.Badge',
    })),
  }
  assert.deepEqual(scanText(source, file, compilerEvidence), [])
})

test('WHAT[STRUCTURED-WORKFLOW-003] classified_protocol_and_physical_positions_stay_green', () => {
  const samples = [
    [
      '/// DSL-class: ExternalSignal — position in the peer protocol.',
      'type PeerFrame = { CurrentInstruction: int }',
      'let decode frame =',
      '    match frame.CurrentInstruction with',
      '    | 0 -> peerOpened ()',
      '    | _ -> peerClosed ()',
    ],
    [
      '/// DSL-class: PhysicalHandle — position in a device-owned ring.',
      'type RingHandle = { ResumePoint: int }',
      'let poll handle =',
      '    match handle.ResumePoint with',
      '    | 0 -> pollHead ()',
      '    | _ -> pollTail ()',
    ],
  ]
  for (const lines of samples) {
    const source = ['module Boundary', ...lines].join('\n')
    assert.deepEqual(scanText(source, 'src/Wanxiangshu/Process/Wire.fs'), [])
  }
})

test('WHAT[STRUCTURED-WORKFLOW-003] classified_physical_handle_stays_green_with_compiler_calls', () => {
  const file = 'src/Wanxiangshu/Process/Port.fs'
  const source = [
    'module Boundary',
    '/// DSL-class: PhysicalHandle — owner DevicePort, law physical delivery, proof port contract.',
    'type RingHandle = { ResumePoint: int }',
    'let poll (handle: RingHandle) (port: Foreign.Port) =',
    '    match handle.ResumePoint with',
    '    | 0 -> port.Send "head"',
    '    | _ -> port.Send "tail"',
  ].join('\n')
  const compilerEvidence = {
    symbolUses: [],
    applicationUses: [6, 7].map((line) => ({
      consumerPath: file,
      resolvedTarget: 'Foreign.Port.Send',
      startLine: line,
      inferredType: 'System.String -> Microsoft.FSharp.Core.unit',
    })),
  }
  assert.deepEqual(scanText(source, file, compilerEvidence), [])
})
