import assert from 'node:assert/strict'
import test from 'node:test'
import * as runtime from '../../../dist/Process/Surface.js'

const {
  describeRun,
  run: executeRun,
  runToolName,
} = await import('../../../dist/OpenCode/Tools/ExecutorToolSurface.js')

const { contextDecode, contextView } = await import('../../../dist/OpenCode/Codec/ToolHostSurface.js')

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

const toolModule = { tool: { schema: fakeSchema } }

const context = (sessionID = 'ses-exec') => ({ sessionID })

const run = (args, ctx = context(), recovery = '') =>
  executeRun(toolModule, {}, args, ctx, recovery)

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

const SPOOL_COMMAND = "printf 'abcdefghijklmnopqrstuvwxyz0123456789'"

const SPOOL_BUDGET = { command: SPOOL_COMMAND, output_budget_bytes: 4 }

test('WHAT[process-execution-005] the real supervised host preserves literal environment overrides and direct argv', async () => {
  const sentinel = 'WANXIANGSHU_PROCESS_HOST_SENTINEL'
  const previous = process.env[sentinel]
  process.env[sentinel] = 'explicit-host-inheritance'
  try {
    const environment = Object.fromEntries([['__proto__', 'literal'], ['constructor', 'data'], ['EMPTY', ''], [sentinel, 'explicit-override']])
    const args = ['hello world', ';touch never', '$(not-a-command)', '', '用户🧭']
    const script = 'process.stdout.write(JSON.stringify({args:process.argv.slice(1),env:process.env}))'
    const request = runtime.command(process.execPath, ['-e', script, ...args], process.cwd(), undefined, environment)
    const result = await runtime.run(request, runtime.estimate(10, 65536, 'medium'), runtime.context(undefined, 30000), runtime.createCancellationToken(false))
    assert.equal(result.ok, true, JSON.stringify(result))
    assert.equal(result.value.kind, 'Completed')
    assert.equal(result.value.exitCode, 0)
    const actual = JSON.parse(result.value.stdout)
    assert.deepEqual(actual.args, args)
    for (const [key, value] of Object.entries(environment)) assert.equal(actual.env[key], value, key)
    assert.equal(actual.env.PATH, process.env.PATH)
  } finally {
    if (previous === undefined) delete process.env[sentinel]
    else process.env[sentinel] = previous
  }
})

test('WHAT[process-execution-005] environment encoding rejects accessors without evaluating them', async () => {
  let reads = 0
  const environment = Object.defineProperty({}, 'VALUE', { get() { reads++; return 'never' } })
  assert.throws(() => runtime.command(process.execPath, [], undefined, undefined, environment), /data strings/)
  assert.equal(reads, 0)
})

test('WHAT[process-execution-005] invalid explicit environment is rejected instead of silently running with ambient values', async () => {
  const { mkdtempSync, existsSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const directory = mkdtempSync(join(tmpdir(), 'wxs-env-reject-'))
  const marker = join(directory, 'effect')
  const script = `require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'ran')`
  try {
    for (const [key, value] of [['BROKEN=KEY', 'value'], ['', 'value'], ['PATH', 'bad\0override'], ['VALUE', '\ud800']]) {
      const environment = Object.fromEntries([[key, value]])
      const request = runtime.command(process.execPath, ['-e', script], directory, undefined, environment)
      const result = await runtime.run(request, runtime.estimate(10, 1024, 'medium'), runtime.context(undefined, 30000), runtime.createCancellationToken(false))
      assert.equal(result.ok, false, JSON.stringify({ key, result }))
      assert.equal(existsSync(marker), false)
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[process-execution-011] RUN_surface_names_the_provider_execution_verb', () => {
  assert.equal(runToolName, 'run')
  const tool = describeRun(toolModule)
  assert.equal(tool.name, 'run')
  assert.match(tool.description, /deadline_seconds (?:and|与) output_budget_bytes/)
  assert.deepEqual(tool.arguments, ['command', 'deadline_seconds', 'output_budget_bytes', 'world_lock'])
})

test('WHAT[process-execution-011] RUN_host_context_codec_exposes_plain_snapshot', () => {
  const decoded = contextDecode({ sessionID: 'ses-exec', agent: 'devops' })
  assert.deepEqual(contextView(decoded), {
    sessionId: 'ses-exec',
    agent: 'devops',
    toolCallId: null,
    providerRunId: null,
    promptText: null,
  })
})

test('WHAT[process-execution-011] RUN_missing_command_is_rejected_before_spawn', async () => {
  const result = await run({})
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /# (?:Missing command|缺少 command)/)
})

test('WHAT[process-execution-011] RUN_blank_command_is_rejected_before_spawn', async () => {
  const result = await run({ command: '   ' })
  assert.doesNotMatch(result, /\berror\s*=/)
  assert.match(result, /# (?:Missing command|缺少 command)/)
})

test('WHAT[process-execution-011] RUN_blank_session_surfaces_natural_execution_consequence_before_spawn', async () => {
  const result = await run({ command: 'true' }, context(''))
  assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  assert.match(result, /(?:cannot run from this execution context|无法在此执行上下文中运行)/i)
})

test('WHAT[process-execution-011] RUN_deadline_overrun_returns_the_fixed_timeout_consequence', async () => {
  const result = await run({ command: 'sleep 5', deadline_seconds: 0.01, output_budget_bytes: 16 })
  assert.doesNotMatch(result, /TimeoutExceeded|\berror\s*=/)
  assert.match(result, /(?:Termination was requested|已请求终止)/)
  assert.doesNotMatch(result, /was stopped|已被停止/)
})

test('WHAT[process-execution-011] RUN_world_lock_is_accepted', async () => {
  const result = parseToml(await run({ command: "printf 'ok'", world_lock: true }))
  assert.equal(result.exit_code, '0')
  assert.equal(result.stdout, 'ok')
})

test('WHAT[process-execution-011] RUN_spooled_request_without_authority_fails_before_execution_without_identity_leak', async () => {
  const result = await run(SPOOL_BUDGET, context(''))
  assert.doesNotMatch(result, /sessionID|\berror\s*=/i)
  assert.match(result, /(?:cannot run from this execution context|无法在此执行上下文中运行)/i)
})

test('WHAT[process-execution-011] RUN_spooled_output_family_blocked_surfaces_recovery_consequence', async () => {
  const result = await run(SPOOL_BUDGET, context(), 'blocked')
  assert.doesNotMatch(result, /RECOVERY_BLOCKED|\berror\s*=/)
  assert.match(result, /(?:large output cannot be reconciled while recovery is blocked|恢复受阻期间无法调和其大额输出)/i)
})

{
  const { run, nodeCommand } = await import('./support/executor.mjs')
  const { mkdtempSync, existsSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')

  const rejectsBeforeSpawn = async execute => {
    const dir = mkdtempSync(join(tmpdir(), 'wxs-pre-spawn-'))
    const marker = join(dir, 'spawned')
    const command = nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(marker)}, 'ran')`)
    try {
      for (const invalid of [{ deadline_seconds: 0 }, { deadline_seconds: Infinity }, { output_budget_bytes: -1 }, { output_budget_bytes: 0.5 }]) {
        const result = await execute({ command, ...invalid })
        assert.match(result, /(?:must|必须)/)
        assert.equal(existsSync(marker), false)
      }
    } finally {
      rmSync(dir, { recursive: true, force: true })
    }
  }
  test('WHAT[process-execution-011] run rejects invalid budgets before command side effects', () => rejectsBeforeSpawn(run))
}

{
  const runtime = await import('../../../dist/Process/Surface.js')
  const { mkdtempSync, mkdirSync, writeFileSync, rmSync } = await import('node:fs')
  const { tmpdir } = await import('node:os')
  const { join } = await import('node:path')
  const { spawnSync } = await import('node:child_process')

  test('WHAT[process-execution-011] CUT_02_PATH_entries_resolve_from_target_cwd_in_search_order', async () => {
    const originalCwd = process.cwd()
    const originalPath = process.env.PATH
    const directory = mkdtempSync(join(tmpdir(), 'wxs-path-'))
    const host = join(directory, 'host')
    const target = join(directory, 'target')
    const absolute = join(directory, 'absolute')
    const executable = 'path-probe'
    try {
      for (const [folder, label] of [[host, 'host'], [join(host, 'bin'), 'host-bin'], [target, 'target'], [join(target, 'bin'), 'target-bin'], [absolute, 'absolute']]) {
        mkdirSync(folder, { recursive: true })
        writeFileSync(join(folder, executable), `#!/bin/sh\nprintf '${label}'\n`, { mode: 0o755 })
      }
      process.chdir(host)
      for (const [pathValue, expected] of [['.', 'target'], ['', 'target'], ['bin', 'target-bin'], ['missing:bin', 'target-bin'], ['bin:.', 'target-bin'], ['.:bin', 'target'], [':bin', 'target'], ['bin:', 'target-bin'], [`${absolute}:bin`, 'absolute'], [`bin:${absolute}`, 'target-bin']]) {
        process.env.PATH = pathValue
        const native = spawnSync('/bin/sh', ['-c', `exec ${executable}`], { cwd: target, env: { ...process.env }, encoding: 'utf8' })
        assert.equal(native.error, undefined, pathValue)
        assert.equal(native.status, 0, pathValue)
        assert.equal(native.stdout, expected, pathValue)
        const result = await runtime.run(
          runtime.command(executable, [], target, undefined),
          runtime.estimate(2, 4096, 'medium'),
          runtime.context(target, 5000),
          runtime.createCancellationToken(false),
        )
        assert.equal(result.ok, true, `${pathValue}: ${JSON.stringify(result)}`)
        assert.equal(result.value.exitCode, native.status, pathValue)
        assert.equal(result.value.stdout, native.stdout, pathValue)
      }
    } finally {
      process.chdir(originalCwd)
      if (originalPath === undefined) delete process.env.PATH
      else process.env.PATH = originalPath
      rmSync(directory, { recursive: true, force: true })
    }
  })
}
