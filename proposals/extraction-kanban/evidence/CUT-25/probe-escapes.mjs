// Base-red probe: each case asks a model program to reach the host `process` object.
// Run with: node probe-escapes.mjs <dist root>
import { mkdtempSync, writeFileSync, existsSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
const distRoot = process.argv[2]
const { run, caseName, render } = await import(join(distRoot, 'Repository/Programming/Js/WorkflowSurface.js'))
const dir = mkdtempSync(join(tmpdir(), 'cut25-probe-'))
writeFileSync(join(dir, 'a.txt'), 'hello\n')
const reach = (expr) => `const p = (${expr}); return { pid: typeof (p && p.pid), canFs: typeof (p && p.getBuiltinModule) };`
const cases = {
  'global api binding': reach(`api.constructor.constructor('return process')()`),
  'global deadline ref': reach(`__deadlineRef.constructor.constructor('return process')()`),
  'global this': reach(`globalThis.constructor.constructor('return process')()`),
  'returned array (this.ls)': `const r = await this.ls('.'); ${reach(`r.constructor.constructor('return process')()`)}`,
  'thrown host failure': `let e; try { await this.cat('missing.txt') } catch (x) { e = x } ${reach(`e.__jsFailure ? e.constructor.constructor('return process')() : null`)}`,
  'api member function': reach(`this._api.cat.constructor('return process')()`),
  'write outside via escape': `const r = await this.ls('.'); const p = r.constructor.constructor('return process')(); if (p && p.getBuiltinModule) p.getBuiltinModule('fs').writeFileSync(${JSON.stringify(join(dir, '..', 'cut25-escaped-' + process.pid))}, 'x'); return { wrote: !!(p && p.getBuiltinModule) };`,
}
const results = {}
for (const [name, body] of Object.entries(cases)) {
  const src = `class Js extends JsProgram {\n  async run() {\n    ${body}\n  }\n}`
  try {
    const o = await run(dir, 'Engineer', 'en', src, 2000, Date.now() + 60000, 1 << 20, null)
    results[name] = `${caseName(o)} ${render(o).replace(/\n/g, ' ').slice(0, 160)}`
  } catch (e) { results[name] = `threw ${e.message}` }
}
const escapedFile = join(dir, '..', 'cut25-escaped-' + process.pid)
results['file written outside workspace'] = existsSync(escapedFile)
rmSync(escapedFile, { force: true }); rmSync(dir, { recursive: true, force: true })
console.log(JSON.stringify(results, null, 2))
