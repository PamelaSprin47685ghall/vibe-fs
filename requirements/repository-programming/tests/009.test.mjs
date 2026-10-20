import test from 'node:test'
import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync, rmSync, symlinkSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { parse } from 'smol-toml'
import { execute, caseName, render } from '../../../dist/Repository/Programming/Js/WorkflowSurface.js'
import { createApiFor, api as apiFor } from '../../../dist/Repository/Programming/Js/RuntimeSurface.js'
import { withSearchLauncher } from '../../process-execution/tests/support/search-launcher.mjs'

const searchProgram = body => `class Js extends JsProgram { async run() { ${body} } }`
const runSearch = (directory, args) => execute(
  directory, 'Engineer', 'en', 'rg-engineer',
  { program: searchProgram(`return await this.rg(${JSON.stringify(args)});`) },
  1000, Date.now() + 5000, 1 << 20, null,
)

test('WHAT[repository-programming-009] registered Engineer searches through the real plugin without Exec capability', async () => {
  const { withExecutablePlugin, acceptAuthorityRoot } = await import('../../verification-system/tests/support/plugin-fixture.mjs')
  await withExecutablePlugin(async (hooks, directory, created, runtime) => {
    const sessionID = 'registered-engineer-search'
    const messageID = 'search-user'
    await acceptAuthorityRoot(runtime, sessionID, 'engineer', messageID)
    await hooks['chat.message']({ sessionID, messageID, agent: 'engineer' }, {
      message: { id: messageID, role: 'user', agent: 'engineer' },
      parts: [{ type: 'text', text: 'Search the repository.' }],
    })
    writeFileSync(join(directory, 'search-input'), 'actual-engineer-needle\n')
    const wire = await hooks.tool['todowrite-for-engineer'].execute({
      program: searchProgram(`if (typeof js.call !== 'undefined' || typeof js.Callable !== 'undefined') throw new Error('unexpected Exec'); return await this.rg(['actual-engineer-needle', 'search-input']);`),
    }, { sessionID, agent: 'engineer', messageID: 'search-assistant', callID: 'search-call' })
    const data = parse(wire).data
    assert.equal(data.exitCode, 0, wire)
    assert.equal(data.messages.filter(message => message.type === 'match').length, 1)
  })
})

test('WHAT[repository-programming-009] actual rg gives a Grep-only office JSON matches, ignored files and native no-match status', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-real-rg-'))
  try {
    execFileSync('git', ['init', '--quiet', directory])
    mkdirSync(join(directory, 'src'))
    writeFileSync(join(directory, '.gitignore'), 'ignored.txt\n')
    writeFileSync(join(directory, 'src/a.txt'), '\uFEFFneedle 中文😀\r\n')
    writeFileSync(join(directory, 'ignored.txt'), 'needle\n')
    const outcome = await runSearch(directory, ['needle', '.'])
    assert.equal(caseName(outcome), 'Succeeded', render(outcome))
    const data = parse(render(outcome)).data
    assert.equal(data.exitCode, 0)
    const matches = data.messages.filter(message => message.type === 'match')
    assert.equal(matches.length, 1)
    assert.ok(matches[0].data.path.text.endsWith('src/a.txt'))
    assert.match(matches[0].data.lines.text, /中文😀\r\n$/)
    const absent = await runSearch(directory, ['no-such-needle', '.'])
    assert.equal(caseName(absent), 'Succeeded', render(absent))
    assert.equal(parse(render(absent)).data.exitCode, 1)
    assert.equal(apiFor(createApiFor(directory, ['Grep'])).js.call, undefined)
    assert.equal(typeof apiFor(createApiFor(directory, ['Grep'])).js.rg, 'function')
    assert.equal(apiFor(createApiFor(directory, ['Read', 'Glob'])).js.rg, undefined)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-009] file lists preserve newline paths and explicit dash patterns, globs and pattern files work', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-rg-paths-'))
  try {
    writeFileSync(join(directory, 'line\nbreak.txt'), '-needle\n')
    writeFileSync(join(directory, 'a.fs'), 'needle\n')
    writeFileSync(join(directory, 'patterns'), 'needle\n')
    writeFileSync(join(directory, '-file'), 'needle\n')
    for (const args of [['--files', '-g', '*.txt', '.'], ['-l', '-e', '-needle', '.']]) {
      const outcome = await runSearch(directory, args)
      assert.equal(caseName(outcome), 'Succeeded', render(outcome))
      const paths = parse(render(outcome)).data.paths
      assert.equal(paths.length, 1)
      assert.ok(paths[0].endsWith('line\nbreak.txt'))
    }
    const patternFile = await runSearch(directory, ['-f', 'patterns', '--glob=*.fs', '.'])
    assert.equal(caseName(patternFile), 'Succeeded', render(patternFile))
    assert.equal(parse(render(patternFile)).data.messages.filter(message => message.type === 'match').length, 1)
    const absolute = await runSearch(directory, ['needle', join(directory, 'a.fs')])
    assert.equal(caseName(absolute), 'Succeeded', render(absolute))
    assert.equal(parse(render(absolute)).data.exitCode, 0)
    const dashPath = await runSearch(directory, ['needle', '--', '-file'])
    assert.equal(caseName(dashPath), 'Succeeded', render(dashPath))
    assert.equal(parse(render(dashPath)).data.exitCode, 0)
    const flagPattern = await runSearch(directory, ['--regexp=--files', '.'])
    assert.equal(caseName(flagPattern), 'Succeeded', render(flagPattern))
    assert.equal(parse(render(flagPattern)).data.exitCode, 1)
    assert.ok(Array.isArray(parse(render(flagPattern)).data.messages))
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-009] unsafe argv, missing paths and external side files are refused before search', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-rg-refusal-'))
  const external = mkdtempSync(join(tmpdir(), 'wxs-rg-outside-'))
  try {
    writeFileSync(join(external, 'private'), 'needle\n')
    symlinkSync(external, join(directory, 'escape'))
    for (const args of [
      ['needle'], [1, '.'], ['bad\u0000pattern', '.'], ['--pre', 'touch unwanted', 'needle', '.'],
      ['--hostname-bin=sh', 'needle', '.'], ['-L', 'needle', '.'],
      ['--count', 'needle', '.'], ['--vimgrep', 'needle', '.'],
      ['--type-list'], ['needle', external], ['needle', 'escape/private'],
      ['-f', join(external, 'private'), '.'], ['--ignore-file', join(external, 'private'), 'needle', '.'],
    ]) {
      const outcome = await runSearch(directory, args)
      assert.equal(caseName(outcome), 'Failed', JSON.stringify(args) + render(outcome))
      assert.match(render(outcome), /INVALID_ARGUMENT|PATH_DENIED/)
    }
  } finally {
    rmSync(directory, { recursive: true, force: true })
    rmSync(external, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-009] inherited rg configuration is disabled and missing rg is an explicit failure', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-rg-environment-'))
  const external = mkdtempSync(join(tmpdir(), 'wxs-rg-config-'))
  const previousConfig = process.env.RIPGREP_CONFIG_PATH
  const previousPath = process.env.PATH
  try {
    writeFileSync(join(directory, 'a.txt'), 'needle\n')
    const config = join(external, 'config')
    writeFileSync(config, '--glob=!*\n')
    process.env.RIPGREP_CONFIG_PATH = config
    const outcome = await runSearch(directory, ['needle', '.'])
    assert.equal(caseName(outcome), 'Succeeded', render(outcome))
    assert.equal(parse(render(outcome)).data.exitCode, 0)
    mkdirSync(join(directory, 'empty-bin'))
    process.env.PATH = join(directory, 'empty-bin')
    const missing = await runSearch(directory, ['needle', '.'])
    assert.equal(caseName(missing), 'Failed', render(missing))
    assert.match(render(missing), /RG_FAILED/)
  } finally {
    if (previousConfig === undefined) delete process.env.RIPGREP_CONFIG_PATH
    else process.env.RIPGREP_CONFIG_PATH = previousConfig
    if (previousPath === undefined) delete process.env.PATH
    else process.env.PATH = previousPath
    rmSync(directory, { recursive: true, force: true })
    rmSync(external, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-009] invalid JSON and invalid UTF-8 paths cannot become successful query results', async () => {
  for (const [args, source] of [
    [['needle', '.'], "process.stdout.write('invalid JSON\\n')"],
    [['--files', '.'], 'process.stdout.write(Buffer.from([255, 0]))'],
  ]) {
    const directory = mkdtempSync(join(tmpdir(), 'wxs-rg-invalid-'))
    try {
      await withSearchLauncher(directory, source, async () => {
        const outcome = await runSearch(directory, args)
        assert.equal(existsSync(join(directory, '.search-launcher-entered')), true, 'invalid output comes from an actual query')
        assert.equal(caseName(outcome), 'Failed', render(outcome))
        assert.match(render(outcome), /RG_FAILED/)
      })
    } finally {
      rmSync(directory, { recursive: true, force: true })
    }
  }
})

test('WHAT[repository-programming-009] consecutive queries consume the current absolute program deadline without borrowing another budget', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-rg-deadline-'))
  const { setTimeout: delay } = await import('node:timers/promises')
  const starts = join(directory, 'query-starts')
  const phase = join(directory, 'query-phase')
  const source = `const fs = require('node:fs'); fs.appendFileSync(${JSON.stringify(starts)}, process.pid + '\\n'); const second = fs.existsSync(${JSON.stringify(phase)}); fs.writeFileSync(${JSON.stringify(phase)}, 'yes'); setTimeout(() => console.log(JSON.stringify({ type: 'summary', data: { matches: 0 } })), second ? 10000 : 0)`
  try {
    await withSearchLauncher(directory, source, async () => {
      const outcome = await execute(directory, 'Engineer', 'en', 'rg-deadline', {
        program: searchProgram(`await this.rg(['needle', '.']); await this.write('first-ok', 'yes'); return await this.rg(['needle', '.']);`),
      }, 1000, Date.now() + 4000, 1 << 20, null)
      assert.equal(existsSync(join(directory, 'first-ok')), true, render(outcome))
      assert.equal(caseName(outcome), 'Failed', render(outcome))
      assert.match(render(outcome), /RG_FAILED|PROGRAM_TIMEOUT|ABANDONED/)
      const pids = readFileSync(starts, 'utf8').trim().split('\n').map(Number)
      assert.equal(pids.length, 2, 'the second actual query starts with the remaining budget')
      const alive = pid => {
        try { process.kill(pid, 0); return true } catch (error) {
          if (error.code === 'ESRCH') return false
          throw error
        }
      }
      const deadline = performance.now() + 2500
      while (pids.some(alive) && performance.now() < deadline) await delay(10)
      assert.equal(pids.some(alive), false)
    })
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[repository-programming-009] excessive actual rg output fails instead of reporting truncated JSON success', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-rg-overflow-'))
  try {
    writeFileSync(join(directory, 'large.txt'), 'needle 中文😀\n'.repeat(10000))
    const outcome = await runSearch(directory, ['needle', '.'])
    assert.equal(caseName(outcome), 'Failed', render(outcome))
    assert.match(render(outcome), /RG_FAILED/)
    assert.match(render(outcome), /limit|window|narrow/)
  } finally {
    rmSync(directory, { recursive: true, force: true })
  }
})

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { mkdtempSync, writeFileSync, rmSync, mkdirSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { createApi, api: apiOf, stagedCount, stagedKinds, run } = await import("../../../dist/Repository/Programming/Js/RuntimeSurface.js");
const { generate } = await import("../../../dist/Repository/Programming/Js/GeneratorSurface.js");

const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-bindings-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const coderSurface = () => generate('Coder', ['Read', 'Write', 'Edit', 'Glob', 'Grep'], 'en')

test('WHAT[repository-programming-009] JS010_bindings_grep_returns_matches', async () => {
  const { dir, cleanup } = sandbox()
  try {
    writeFileSync(join(dir, 'a.txt'), 'one two one', 'utf8')
    const api = apiOf(createApi(dir))
    assert.equal(api.js.grep, undefined)
    const listed = api.js.ls('.')
    assert.equal(listed.ok, true)
    const hits = []
    for (const entry of listed.entries.filter((e) => e.endsWith('.txt'))) {
      const read = api.js.cat(entry)
      assert.equal(read.ok, true)
      for (const [begin, end] of api.js.whereAll(read.text, 'one').spans) {
        hits.push({ path: entry, text: read.text.slice(begin, end) })
      }
    }
    assert.deepEqual(hits.map((m) => m.text), ['one', 'one'])
    assert.deepEqual(hits.map((m) => m.path), ['a.txt', 'a.txt'])
  } finally {
    cleanup()
  }
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { join } = await import("node:path");
const { readUtf8, glob, findAnchor, requireUnique, grep, commitPlan, rollbackPlan } = await import("../../../dist/Repository/Programming/Js/FilesystemSurface.js");

const exact = (text) => ({ kind: 'exact', text })
const regex = (text) => ({ kind: 'regex', text })
const sandbox = () => {
  const dir = mkdtempSync(join(tmpdir(), 'wxs-jstools-'))
  return { dir, cleanup: () => rmSync(dir, { recursive: true, force: true }) }
}
const ok = (result) => result.ok
const codeOf = (result) => result.code
const unwrap = (result) => {
  assert.equal(result.ok, true, `expected Ok, got ${JSON.stringify(result.error)}`)
  return result.value
}

test('WHAT[repository-programming-009] JS020_grep_returns_line_column_and_skips_ignored', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'src'))
    mkdirSync(join(dir, 'dist'))
    writeFileSync(join(dir, 'src', 'a.fs'), 'alpha\nTODO: one\n', 'utf8')
    writeFileSync(join(dir, 'dist', 'skip.js'), 'TODO: hidden\n', 'utf8')
    writeFileSync(join(dir, '.gitignore'), '/dist/\n', 'utf8')

    const listing = unwrap(await grep(dir, regex('TODO:.+'), 'src/**/*.fs'))
    const hits = listing.matches
    assert.equal(hits.length, 1)
    assert.equal(hits[0].path, 'src/a.fs')
    assert.equal(hits[0].line, 2)
    assert.equal(hits[0].column, 1)
    assert.equal(hits[0].text, 'TODO: one')
    assert.equal('truncated' in listing, false)
  } finally {
    cleanup()
  }
})

test('WHAT[repository-programming-009] grep character-class candidates cannot leak to another path prefix', async () => {
  const { dir, cleanup } = sandbox()
  try {
    mkdirSync(join(dir, 'src'))
    mkdirSync(join(dir, 'other'))
    for (const path of ['src/]', 'src/|', 'src/a', 'other/file]']) {
      writeFileSync(join(dir, path), 'needle\n', 'utf8')
    }
    const hits = unwrap(await grep(dir, exact('needle'), '/src/[]|]')).matches
    assert.deepEqual(hits.map(hit => hit.path), ['src/]', 'src/|'])
  } finally { cleanup() }
})
}
