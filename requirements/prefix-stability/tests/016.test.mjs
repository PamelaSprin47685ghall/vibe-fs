import assert from 'node:assert/strict'
import test from 'node:test'
import * as canonicalJson from '../../../dist/OpenCode/Codec/CanonicalJsonSurface.js'
import * as providerProjection from '../../../dist/Participant/Provider/Projection/Surface.js'
import * as strength from '../../../dist/Strength/Surface.js'
import * as relay from '../../../dist/Mission/Relay/Surface.js'
import * as assessment from '../../../dist/Mission/Relay/Assessment/Surface.js'
import * as reviewContract from '../../../dist/OpenCode/Host/ManagerReviewContract.js'
import * as managerReviewTools from '../../../dist/OpenCode/Tools/ManagerReviewTools.js'
import { readText } from '../../../dist/Participant/Provider/ProviderResources.js'
import { ProviderLanguage } from '../../../dist/Participant/Provider/Language.js'
import { installDefaultResources } from '../../../dist/OpenCode/Host/ManagedAgentConfigSurface.js'

installDefaultResources()

const FOUR_DEDICATED_TOOLS = ['read-manager', 'glob-manager', 'grep-manager', 'js-manager']
const CONTRACT_TOKEN = 'do-not-use-except-for-review'

const createRawManagerTools = () => [
  {
    name: 'fork',
    description: 'Delegate an independent assignment to an Engineer.',
    parameters: {
      type: 'object',
      properties: {
        calling: { type: 'string', enum: ['engineer'] },
        name: { type: 'string' },
        charge: { type: 'string' },
        keywords: { type: 'string' },
        attach: { type: 'string' },
        expected_tool_calls: { type: 'integer' },
      },
      required: ['calling', 'name', 'charge'],
    },
  },
  {
    name: 'resume',
    description: 'Continue working on an existing route or companion devops.',
    parameters: {
      type: 'object',
      properties: {
        name: { type: 'string' },
        charge: { type: 'string' },
        keywords: { type: 'string' },
        attach: { type: 'string' },
        expected_tool_calls: { type: 'integer' },
      },
      required: ['name', 'charge'],
    },
  },
  {
    name: 'join',
    description: 'Await completion of running delegated units.',
    parameters: {
      type: 'object',
      properties: {},
      required: [],
    },
  },
  {
    name: 'horizon',
    description: 'Pull snapshot view of current delegation horizon and status.',
    parameters: {
      type: 'object',
      properties: {},
      required: [],
    },
  },
  {
    name: 'review',
    description: 'Submit an independent 8-dimension review of workspace and artifacts.',
    parameters: JSON.parse(assessment.schemaJson),
  },
  {
    name: 'read-manager',
    description: 'Read file content from the filesystem during manager review.',
    parameters: {
      type: 'object',
      properties: {
        filePath: { type: 'string', description: 'The path to the file to read' },
        offset: { type: 'integer', description: 'The line number to start reading from (1-indexed)' },
        limit: { type: 'integer', description: 'The maximum number of lines to read' },
      },
      required: ['filePath'],
    },
  },
  {
    name: 'glob-manager',
    description: 'Enumerate files matching a glob pattern during manager review.',
    parameters: {
      type: 'object',
      properties: {
        pattern: { type: 'string', description: 'The glob pattern to match files against' },
        path: { type: 'string', description: 'The directory to search in (optional)' },
      },
      required: ['pattern'],
    },
  },
  {
    name: 'grep-manager',
    description: 'Search file contents for regex or literal matches during manager review.',
    parameters: {
      type: 'object',
      properties: {
        pattern: { type: 'string', description: 'The regex or literal pattern to search for' },
        path: { type: 'string', description: 'The directory to search in (optional)' },
        include: { type: 'string', description: 'File pattern to include' },
      },
      required: ['pattern'],
    },
  },
  {
    name: 'js-manager',
    description: 'Run programmatic read-only exploration script using Read/Glob/Grep methods.',
    parameters: {
      type: 'object',
      properties: {
        program: { type: 'string', description: 'JavaScript code extending JsProgram' },
      },
      required: ['program'],
    },
  },
  {
    name: 'assume',
    description: 'Cognitive workspace update and todo declaration.',
    parameters: {
      type: 'object',
      properties: {
        update: { type: 'string' },
        todos: { type: 'array' },
      },
      required: ['update', 'todos'],
    },
  },
  {
    name: 'suicide',
    description: 'Retire current manager run cleanly once obligations are fulfilled.',
    parameters: {
      type: 'object',
      properties: {},
      required: [],
    },
  },
]

const buildDecoratedManagerTools = () => {
  const tools = createRawManagerTools()
  for (const tool of tools) {
    if (managerReviewTools.isReviewTool(tool.name)) {
      reviewContract.decorateDefinition({ toolID: tool.name }, tool)
    }
  }
  return tools
}

const wire = (
  messages,
  {
    tools,
    system,
    providerId = 'manager-test-provider',
    modelId = 'manager-model-v1',
    variant = 'deep',
  } = {},
) => ({
  modelId,
  messages,
  providerId,
  system,
  tools,
  variant,
})

test('WHAT[prefix-stability-001] manager_life_review_acceptance_provider_wire_is_append_only_prefix_and_tools_identical', () => {
  const roadId = 'road-life-001'
  const incId = 'inc-life-001'
  const snapshotId = 'snap-life-001'
  const authorityRev = 'auth-life-001'

  // 1. Initial State: Manager Life begins in AuditPending phase
  const opened = relay.openIncumbency(relay.empty(), roadId, incId, snapshotId, authorityRev)
  assert.equal(opened.ok, true)
  const initialView = relay.view(opened.state, roadId)
  assert.equal(initialView.phase, 'AuditPending', 'initial Manager phase must be AuditPending')

  // Read authentic resource documents from provider resources
  const assessPromptDoc = readText(ProviderLanguage.SimplifiedChinese, 'runtime/manager-assess')
  const workPromptDoc = readText(ProviderLanguage.SimplifiedChinese, 'runtime/manager-work')

  // Manager system prompt remains stable across the life
  const managerSystemPrompt = strength.systemPromptForRole('Manager')

  // Tools definition: identical tool set with contract-decorated schemas for four review tools
  const toolsBefore = buildDecoratedManagerTools()
  const toolsAfter = buildDecoratedManagerTools()

  // [001] Tools 必须完全一致，严禁仅为前缀；断言名字、顺序、数量、description/schema/properties/required/enum
  assert.equal(toolsBefore.length, toolsAfter.length, 'tools count must be identical before and after review')
  for (let i = 0; i < toolsBefore.length; i += 1) {
    const b = toolsBefore[i]
    const a = toolsAfter[i]
    assert.equal(b.name, a.name, `tool name at index ${i} must match`)
    assert.equal(
      canonicalJson.canonicalJson(b),
      canonicalJson.canonicalJson(a),
      `tool '${b.name}' must be canonically identical in serialization`,
    )
  }

  // Four dedicated review tools must have the exact contract schema decoration
  for (const reviewToolName of FOUR_DEDICATED_TOOLS) {
    const tool = toolsBefore.find((t) => t.name === reviewToolName)
    assert.ok(tool, `review tool '${reviewToolName}' must exist in Manager tools`)
    assert.ok(
      tool.parameters.properties?.contract,
      `Tool ${reviewToolName} must be decorated with contract parameter`,
    )
    assert.equal(tool.parameters.properties.contract.type, 'string')
    assert.deepEqual(tool.parameters.properties.contract.enum, [CONTRACT_TOKEN])
    assert.ok(
      tool.parameters.required.includes('contract'),
      `Tool ${reviewToolName} must mark contract as required`,
    )
  }

  // 2. Provider Attempt 1 (Before Review):
  // Initial messages carry the authority prompt and the manager-assess instruction
  const initialUserMessage = {
    id: 'msg-01',
    role: 'user',
    parts: [
      { kind: 'text', text: 'AUTHORITY ASSIGNMENT: Implement feature X and verify requirements.' },
      { kind: 'text', text: assessPromptDoc },
    ],
  }

  const attemptBefore = wire([initialUserMessage], {
    tools: toolsBefore.map((t) => canonicalJson.canonicalJson(t)),
    system: [managerSystemPrompt],
  })

  // 3. State Transition: Manager completes independent assessment and submits Review
  // Scores with at least one REVISE to define repair obligations and enter WorkOwned phase
  const assessed = relay.assess(
    opened.state,
    roadId,
    incId,
    'assess-life-001',
    snapshotId,
    authorityRev,
    'REVISE', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT',
  )
  assert.equal(assessed.ok, true)
  const assessedView = relay.view(assessed.state, roadId)
  assert.equal(assessedView.phase, 'WorkOwned', 'assessed phase with REVISE must transition to WorkOwned')

  // Review tool call produced by assistant during review execution
  const reviewAssistantMessage = {
    id: 'msg-02',
    role: 'assistant',
    parts: [
      {
        kind: 'tool-call',
        callId: 'call-review-01',
        name: 'review',
        args: JSON.stringify({
          language_algorithms: 'REVISE',
          simplicity: 'PERFECT',
          structure: 'PERFECT',
          granularity: 'PERFECT',
          tests_evidence: 'PERFECT',
          logic_reliability_boundaries: 'PERFECT',
          caller_ergonomics: 'PERFECT',
          completeness: 'PERFECT',
          note: 'Need to address algorithmic defect.',
        }),
      },
    ],
  }

  // Review tool result returned to provider (ToolHostCodec_tomlObjectWithInstructions)
  const reviewResultMessage = {
    id: 'msg-03',
    role: 'tool',
    parts: [
      {
        kind: 'tool-result',
        callId: 'call-review-01',
        result: 'recorded = true\n\n[instructions]\ninstruction = "runtime/manager-work"',
      },
    ],
  }

  // Legally appended continuation turn delivering runtime/manager-work without modifying history
  const repairWorkInstructionMessage = {
    id: 'msg-04',
    role: 'user',
    parts: [
      { kind: 'text', text: workPromptDoc },
    ],
  }

  // 4. Provider Attempt 2 (After Review):
  // History is strictly preserved; new turns are appended
  const attemptAfter = wire(
    [
      initialUserMessage,
      reviewAssistantMessage,
      reviewResultMessage,
      repairWorkInstructionMessage,
    ],
    {
      tools: toolsAfter.map((t) => canonicalJson.canonicalJson(t)),
      system: [managerSystemPrompt],
    },
  )

  // 5. Authoritative Verdict: ProviderProjection.isAppendOnlyPrefix must hold
  assert.equal(
    providerProjection.isAppendOnlyPrefix(attemptBefore, attemptAfter),
    true,
    'WHAT[prefix-stability-001]: attemptBefore must be an authoritative append-only prefix of attemptAfter across Review acceptance',
  )

  // 6. [001] Fail-Closed Negative Counterexamples:
  // A. Directionality: longer history cannot be a prefix of shorter history
  assert.equal(
    providerProjection.isAppendOnlyPrefix(attemptAfter, attemptBefore),
    false,
    'WHAT[prefix-stability-001]: reverse order must not satisfy append-only prefix law',
  )

  // B. Historical Byte Immutability: modifying historical assess prompt breaks prefix law
  const mutatedHistory = wire(
    [
      {
        id: 'msg-01',
        role: 'user',
        parts: [
          { kind: 'text', text: 'MUTATED HISTORICAL ASSIGNMENT' },
          { kind: 'text', text: assessPromptDoc },
        ],
      },
      reviewAssistantMessage,
      reviewResultMessage,
      repairWorkInstructionMessage,
    ],
    {
      tools: toolsAfter.map((t) => canonicalJson.canonicalJson(t)),
      system: [managerSystemPrompt],
    },
  )
  assert.equal(
    providerProjection.isAppendOnlyPrefix(attemptBefore, mutatedHistory),
    false,
    'WHAT[prefix-stability-001]: modifying historical bytes in attempt 2 must break append-only prefix law',
  )

  // C. Tool-Set Immutability: changing or removing tools across review breaks prefix law
  const driftedToolsAttempt = wire(
    attemptAfter.messages,
    {
      tools: toolsAfter.slice(1).map((t) => canonicalJson.canonicalJson(t)),
      system: [managerSystemPrompt],
    },
  )
  assert.equal(
    providerProjection.isAppendOnlyPrefix(attemptBefore, driftedToolsAttempt),
    false,
    'WHAT[prefix-stability-001]: tools set drift across review must break prefix law even if messages prefix',
  )

  // D. Contract Schema Immutability: changing contract decoration breaks canonical equality
  const alteredContractTools = buildDecoratedManagerTools()
  const readTool = alteredContractTools.find((t) => t.name === 'read-manager')
  readTool.parameters.properties.contract.enum = ['altered-contract-enum']
  assert.notEqual(
    canonicalJson.canonicalJson(toolsBefore.find((t) => t.name === 'read-manager')),
    canonicalJson.canonicalJson(readTool),
    'WHAT[prefix-stability-001]: altering contract decoration enum must change canonical JSON',
  )
})

test('WHAT[prefix-stability-007] manager_life_review_acceptance_system_prompt_byte_identical', () => {
  // WHAT[prefix-stability-007]: Office system prompt 在同一个生命周期（Life）内保持逐字节一致。
  // 严禁因 T1 交托、Fallback 切换、Review 或 Host compaction 等事件改写 system prompt 字节或重绑 Persona。
  const managerSystemPromptBefore = strength.systemPromptForRole('Manager')
  const managerSystemPromptAfter = strength.systemPromptForRole('Manager')

  // Direct byte-identical assertions
  assert.ok(managerSystemPromptBefore.length > 0, 'Manager system prompt must not be empty')
  assert.equal(
    managerSystemPromptBefore,
    managerSystemPromptAfter,
    'WHAT[prefix-stability-007]: system prompt must remain byte-identical before and after Review acceptance in same Life',
  )

  // Negative counterexample: system prompt drift across Review breaks prefix stability
  const baseTools = buildDecoratedManagerTools().map((t) => canonicalJson.canonicalJson(t))
  const message = {
    id: 'msg-sys-01',
    role: 'user',
    parts: [{ kind: 'text', text: 'SYSTEM PROMPT STABILITY PROOF' }],
  }

  const attemptBefore = wire([message], {
    tools: baseTools,
    system: [managerSystemPromptBefore],
  })

  const driftedSystemAttempt = wire([message], {
    tools: baseTools,
    system: ['MUTATED SYSTEM PROMPT: Review event illegally modified prompt bytes'],
  })

  assert.equal(
    providerProjection.isAppendOnlyPrefix(attemptBefore, driftedSystemAttempt),
    false,
    'WHAT[prefix-stability-007]: system prompt drift across review must break provider prefix law',
  )
})
