import { join } from 'node:path'
import { runProgram, caseName, render, parseData, nodeCommand } from './executor.mjs'

const [mode, directory] = process.argv.slice(2)
const witness = join(directory, 'target.json')
const command = `exec ${nodeCommand(`require('node:fs').writeFileSync(${JSON.stringify(witness)}, JSON.stringify({ pid: process.pid, group: process.ppid })); process.stdout.write('READY'); setInterval(() => {}, 1000)`)}`
const next = mode === 'next'
  ? 'if (ctx.step === 1) return null; entered = true; while (true) {}'
  : `return ctx.step === 0 ? ${JSON.stringify(command)} : null;`
const watch = mode === 'watch'
  ? 'if (ctx.remainingMs === 0) return false; if (!ctx.stdout) return true; entered = true; while (true) {}'
  : 'return ctx.remainingMs > 0;'
const sanity = mode === 'sanity'
  ? 'entered = true; while (true) {}'
  : 'return { nextDone: () => this.next({ step: 1 }), watchVeto: () => this.watch({ remainingMs: 0 }) };'
const outcome = await runProgram(directory, `let entered = false;
  const result = await js.run(class extends js.Runnable {
    maxDuration = ${mode === 'watch' ? 1000 : 250};
    interval = 25;
    next(ctx) { ${next} }
    watch(ctx) { ${watch} }
    sanity() { ${sanity} }
  });
  return { entered, result };`)
process.stdout.write(JSON.stringify({ kind: caseName(outcome), wire: render(outcome), data: caseName(outcome) === 'Succeeded' ? parseData(outcome) : null }))
