// Split from tests/unit/tools/executor-tool.test.mjs (cutover Wave 2a); owner: output-distillation
// RUN spooled path: the run tool's output beyond output_budget_bytes is distilled,
// and the provider wire exposes the distillation account — never chunk statistics.
//
// DISTILL-013 (不返回 chunk 统计仪表盘): the spooled map/reduce account carries no
// chunk_count / total_bytes / spool_path; overflow degrades to a partial report
// carried as instructions rather than a thrown exception.
//
// The wire prose assertions are language-sensitive; pin the provider language
// before any module import so the account renders in English (HOST-026 binding).

process.env.WANXIANGSHU_PROVIDER_LANGUAGE = 'en'

import assert from 'node:assert/strict'
import test from 'node:test'

import { listItems, sessionId } from '../../verification-system/tests/support/domain.mjs'

const {
  HostToolArguments_$ctor_4E60E31B: makeArgs,
  HostToolContext,
  ToolHostCodec_factory,
} = await import('../../../dist/OpenCode/Codec/ToolHostCodec.js')
const { runSpec } = await import('../../../dist/OpenCode/Tools/ExecutorTool.js')
const toolRuntimeModule = await import('../../../dist/OpenCode/Tools/ToolRuntimeScope.js')
const { ToolRuntimeScope } = toolRuntimeModule
const attachFamilyRecovery = Object.entries(toolRuntimeModule).find(([k]) => k.startsWith('ToolRuntimeScope__AttachFamilyRecovery_'))?.[1]
const {
  FamilyRecovery,
  NonEmpty_one: nonEmptyOne,
  RecoveryBlock,
} = await import('../../../dist/Execution/Session/Recovery/Model.js')

const chain = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => chain(`${kind}-described`, extra),
  optional: () => chain(`${kind}-optional`, extra),
})
const fakeSchema = {
  string: () => chain('string'),
  number: () => chain('number'),
  enum: (values) => chain('enum', { values }),
  boolean: () => chain('boolean'),
}
const factory = ToolHostCodec_factory({ tool: { schema: fakeSchema } })

const context = (session = 'ses-exec') =>
  new HostToolContext(session, undefined, undefined, undefined, undefined, () => () => {})

const scope = ({ sessions } = {}) =>
  new ToolRuntimeScope(
    sessions ?? {},
    undefined,
    undefined,
    undefined,
    new Map(),
    () => undefined,
    new Set(),
    new Map(),
    undefined,
    undefined,
    undefined,
    undefined,
    undefined,
  )

const parseToml = (text) =>
  Object.fromEntries(
    text
      .split('\n')
      .filter((line) => /^[a-z_0-9]+ = /.test(line))
      .map((line) => {
        const [name, ...rest] = line.split(' = ')
        const raw = rest.join(' = ')
        return [name, raw.startsWith('"') ? JSON.parse(raw) : raw]
      }),
  )

const run = (runtimeScope, args, ctx = context()) => runSpec(factory, runtimeScope).Execute(makeArgs(args), ctx)

const SPOOL_COMMAND = "printf 'abcdefghijklmnopqrstuvwxyz0123456789'"
const SPOOL_BUDGET = { command: SPOOL_COMMAND, output_budget_bytes: 4 }

test('WHAT[DISTILL-013] RUN_spooled_output_runs_distillation_without_chunk_statistics', async () => {
  const runtimeScope = scope()
  // First permit request (the tool's own gate) waits; every later request from
  // the map/reduce runtime hard-blocks so chunk forks/awaits fail fast into a
  // partial account instead of retrying until the await budget expires.
  let calls = 0
  attachFamilyRecovery(runtimeScope, async () => {
    calls += 1
    return calls === 1
      ? new FamilyRecovery(1, [nonEmptyOne(new RecoveryBlock(6, [sessionId('ses-exec')]))])
      : new FamilyRecovery(2, [nonEmptyOne(new RecoveryBlock(6, [sessionId('ses-exec')]))])
  })

  const text = await run(runtimeScope, SPOOL_BUDGET)
  const result = parseToml(text)

  assert.equal(result.exit_code, '0')
  assert.equal(result.chunk_count, undefined, 'provider must not expose chunk_count')
  assert.equal(result.total_bytes, undefined, 'provider must not expose total_bytes')
  assert.equal(result.spool_path, undefined, 'provider must not expose spool_path')
  // No journal → the distiller chunk fork fails fast; the account degrades to a
  // partial report carried as instructions, never a thrown exception.
  assert.match(text, /Condensation|Most recent raw output/i)
})
