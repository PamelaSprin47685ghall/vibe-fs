// CUT-19 §15 shadow check (README §10 data plan): the same directory listing is grouped by the
// old hand-written law (base dist) and by wanxiangli/segments (candidate dist); results must be
// equal item by item. Only empty fixture files in a temp dir; no real .git/wanxiangshu is touched.
// usage: node shadow-segment-groups.mjs <base-dist> <candidate-dist> [rounds]
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'

const [baseDist, candDist, roundsArg] = process.argv.slice(2)
const load = async dist => (await import(resolve(dist, 'Persistence/EventStore/ProcessEventLog.js'))).ProcessEventLogModule_writerPhysicalMetadata
const base = await load(baseDist)
const cand = await load(candDist)
const groups = (fn, commonDir) => {
  try {
    const list = fn(commonDir)
    const out = []
    for (const item of list) out.push({ writerId: item.WriterId, fileNames: [...item.FileNames] })
    return { ok: out }
  } catch (error) { return { threw: String(error?.message ?? error) } }
}
let seed = 19
const random = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const writers = ['w', 'w-0000', 'x-y', 'z', 'a-0001-0000', '-', '']
const asciiTails = ['', '-0000', '-0001', '-0002', '-0003', '-9999', '-000', '-10000', '-00a0']
const unicodeTails = ['-٠٠٠٠', '-０００１']
const rounds = Number(roundsArg ?? 2000)
const tally = { rounds, equal: 0, differ: 0, asciiOnlyRounds: 0, unicodeRounds: 0, baseThrewUnicode: 0, candThrew: 0, examples: [] }
for (let round = 0; round < rounds; round += 1) {
  const useUnicode = random() < 0.2
  const tails = useUnicode ? [...asciiTails, ...unicodeTails] : asciiTails
  const names = new Set()
  const count = 1 + Math.floor(random() * 12)
  for (let i = 0; i < count; i += 1) {
    const name = writers[Math.floor(random() * writers.length)] + tails[Math.floor(random() * tails.length)] + (random() < 0.9 ? '.ndjson' : '.tmp')
    if (name !== '.tmp') names.add(name)
  }
  const hasUnicode = [...names].some(name => /[٠-٩０-９]/.test(name))
  hasUnicode ? (tally.unicodeRounds += 1) : (tally.asciiOnlyRounds += 1)
  const root = mkdtempSync(join(tmpdir(), 'cut19-shadow-'))
  const commonDir = join(root, '.Herdchestra')
  const events = join(commonDir, 'wanxiangshu', 'events')
  mkdirSync(events, { recursive: true })
  for (const name of names) writeFileSync(join(events, name), '')
  const left = groups(base, commonDir)
  const right = groups(cand, commonDir)
  rmSync(root, { recursive: true, force: true })
  if (right.threw) tally.candThrew += 1
  if (JSON.stringify(left) === JSON.stringify(right)) { tally.equal += 1; continue }
  if (hasUnicode && left.threw && right.ok) { tally.baseThrewUnicode += 1; continue }
  tally.differ += 1
  if (tally.examples.length < 5) tally.examples.push({ names: [...names], base: left, cand: right })
}
console.log(JSON.stringify(tally, null, 2))
process.exitCode = tally.differ === 0 && tally.candThrew === 0 ? 0 : 1
