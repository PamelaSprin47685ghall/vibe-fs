import { readFileSync } from 'node:fs'

const summary = JSON.parse(readFileSync('artifacts/coverage/coverage-summary.json', 'utf8'))
const files = summary.files
  .filter((f) => !f.path.includes('fable_modules'))
  .map((f) => ({
    path: f.path.replace(process.cwd() + '/', ''),
    total: f.totalLineCount,
    covered: f.coveredLineCount,
    uncovered: f.totalLineCount - f.coveredLineCount,
    pct: f.coveredLinePercent,
  }))
  .filter((f) => f.pct < 80)
  .sort((a, b) => b.uncovered - a.uncovered)

const prod = summary.files.filter((f) => !f.path.includes('fable_modules'))
const total = prod.reduce((acc, f) => acc + f.totalLineCount, 0)
const covered = prod.reduce((acc, f) => acc + f.coveredLineCount, 0)
console.log('Total production lines:', total, 'covered:', covered, 'pct:', (covered / total) * 100)
console.log('Files below 80% sorted by uncovered lines:')
for (const f of files.slice(0, 80)) {
  console.log(
    f.pct.toFixed(2).padStart(6) + '% ' +
    f.uncovered.toString().padStart(4) + '/' + f.total.toString().padStart(4) + ' ' +
    f.path,
  )
}
