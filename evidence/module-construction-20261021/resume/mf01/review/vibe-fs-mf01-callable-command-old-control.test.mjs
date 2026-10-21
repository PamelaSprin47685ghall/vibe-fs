import test from 'node:test'
import assert from 'node:assert/strict'
import * as fs from 'node:fs'
import * as os from 'node:os'
import * as path from 'node:path'
import { execute, caseName, render } from '/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/dist/Repository/Programming/Js/WorkflowSurface.js'

for (const kind of ['class-instance', 'plain-toJSON']) {
  test(`old compiled Workflow accepts own spawn fields from ${kind} without toJSON projection`, async () => {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'mf01-old-command-'))
    try {
      const command = kind === 'class-instance'
        ? "class Command { constructor() { this.name = 'probe'; this.spawn = 'printf OLD_COMMAND > command.marker'; } } const command = new Command();"
        : "const command = { name: 'probe', spawn: 'printf OLD_COMMAND > command.marker', toJSON() { toJSONCalls++; return { different: 1 }; } };"
      const source = `class Js extends JsProgram { async run() {
        let toJSONCalls = 0; ${command}
        const result = await this.call(class extends this.Callable {
          maxDuration = 3000; interval = 10;
          next(ctx) {
            if (ctx.step === 0) return command;
            if (ctx.step === 1) return 'while [ ! -f command.marker ]; do sleep 0.01; done';
            return null;
          }
          watch(ctx) { return ctx.proof ? false : true; }
          sanity() { return { nextDone: () => [{ step: 999 }], watchVeto: () => [{ proof: true }] }; }
        });
        return { result, toJSONCalls };
      } }`
      const result = await execute(directory, 'DevOps', 'en', 'mf01-command-old-control', { program: source }, 5000, Date.now() + 60000, 1 << 20, null)
      const text = render(result)
      console.log(JSON.stringify({ evidence: 'actual-old-compiled-workflow', kind, case: caseName(result), rendered: text }))
      assert.equal(caseName(result), 'Succeeded', text)
      assert.match(text, /status = "completed"/)
      assert.match(text, /toJSONCalls = 0/)
      assert.equal(fs.readFileSync(path.join(directory, 'command.marker'), 'utf8'), 'OLD_COMMAND')
    } finally { fs.rmSync(directory, { recursive: true, force: true }) }
  })
}
