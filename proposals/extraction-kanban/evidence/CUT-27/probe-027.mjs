import { join, resolve } from 'node:path'
for (const d of process.argv.slice(2)) {
  const cb = await import(join(resolve(d), 'Repository/Knowledge/Casebook/Surface.js'))
  let bad = 0; for (let b = 40; b < 80; b++) { const c = cb.truncateDiffForBudget('😀'.repeat(60), b); if (!c.text.isWellFormed() || c.text.length > b) bad++ }
  const cap = await import(join(resolve(d), 'Repository/Knowledge/Casebook/Capture.js'))
  const o = c => JSON.stringify(cap.CasebookCapture_ofExecCommand(c) ?? null)
  console.log(d, 'truncation ill-formed budgets:', bad, '| head -c 100 f ->', o('head -c 100 f'), '| cat `x` ->', o('cat `x`'))
}
