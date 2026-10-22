// CUT-11 sub-scope 3: static scan of who may start a Host send and who owns its claim.
// Usage: node association-scan.mjs <repo-root>
// Facts checked (exit 1 if any fails):
//  1. dispatch claims (claimAgentOwnerRoot / claimContinuation) are created only in Interaction/Dispatch/Send.fs
//     (RuntimeSurface.fs is the JS proof surface, excluded) and every Send.fs SendPrompt carries the PromptKey metadata;
//  2. the Work delivery prompt (ToolRuntimeScope ExecutionSpawn.prompt) carries no metadata, so it never carries a PromptKey
//     and no dispatch claim can name it;
//  3. Work delivery only prompts after DeliverySessionBound (PlanTools binds before promptSession) and its restart path
//     never resends (PluginRecoveryWiring only observes / binds).
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
const root = process.argv[2]
const src = join(root, 'src/Wanxiangshu')
const files = []
const walk = (d) => { for (const n of readdirSync(d)) { const p = join(d, n); statSync(p).isDirectory() ? walk(p) : p.endsWith('.fs') && files.push(p) } }
walk(src)
const results = []
const fact = (name, ok, detail) => results.push({ name, ok, detail })
const claimSites = []
for (const f of files) {
  const rel = relative(root, f)
  readFileSync(f, 'utf8').split('\n').forEach((l, i) => {
    if (/PromptAuthorityRun\.(claimAgentOwnerRoot|claimContinuation)\b/.test(l) && !rel.endsWith('Authority/RuntimeSurface.fs')) claimSites.push(`${rel}:${i + 1}`)
  })
}
fact('claims created only in Dispatch/Send.fs', claimSites.length > 0 && claimSites.every((s) => s.startsWith('src/Wanxiangshu/Interaction/Dispatch/Send.fs:')), claimSites)
const send = readFileSync(join(src, 'Interaction/Dispatch/Send.fs'), 'utf8')
const sendMeta = [...send.matchAll(/Metadata = (.*)/g)].map((m) => m[1].trim())
fact('every Send.fs prompt carries PromptKey metadata', sendMeta.length > 0 && sendMeta.every((m) => /^Some\(this\.Metadata key /.test(m)), sendMeta)
const trs = readFileSync(join(src, 'OpenCode/Tools/ToolRuntimeScope.fs'), 'utf8')
const spawn = trs.slice(trs.indexOf('module private ExecutionSpawn'), trs.indexOf('let create (sessions'))
fact('Work delivery prompt carries no metadata (no PromptKey)', /Metadata = None/.test(spawn) && !/PromptKey|Metadata = Some/.test(spawn), (spawn.match(/Metadata = .*/) ?? [])[0])
const plan = readFileSync(join(src, 'Mission/Planning/PlanTools.fs'), 'utf8')
const cbp = plan.slice(plan.indexOf('let private createBindAndPrompt'), plan.indexOf('let private createBindAndPrompt') + 3000)
fact('delivery binds the session before prompting', cbp.includes('promptBoundDelivery') && cbp.indexOf('bindObservedExecutionSession') > 0 && cbp.indexOf('bindObservedExecutionSession') < cbp.indexOf('promptBoundDelivery'), 'createBindAndPrompt: bind → promptBoundDelivery')
const rec = readFileSync(join(src, 'OpenCode/Plugin/PluginRecoveryWiring.fs'), 'utf8')
fact('restart recovery of a pending delivery never prompts', !/promptSession|SendPrompt|PromptExecutionSession/.test(rec), 'PluginRecoveryWiring.fs has no prompt call')
console.log(JSON.stringify(results, null, 1))
process.exit(results.every((r) => r.ok) ? 0 : 1)
