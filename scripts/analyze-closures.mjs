import { readdirSync } from 'node:fs'
import { planOwnerCompile } from './lib/owner-compile.mjs'

const root = 'src/Wanxiangshu'
const projects = readdirSync(root)
  .filter((n) => /^Wanxiangshu\.Owner\..+\.fsproj$/.test(n))
  .map((n) => `${root}/${n}`)

const stats = []
for (const p of projects) {
  try {
    const plan = planOwnerCompile({ projectPath: p, aggregatePath: `${root}/Wanxiangshu.fsproj` })
    stats.push({ project: p, projects: plan.projectPaths.length, files: plan.compileItems.length })
  } catch (e) {
    stats.push({ project: p, error: e.message })
  }
}
stats.sort((a, b) => (b.files || 0) - (a.files || 0))
console.log(JSON.stringify(stats, null, 2))
