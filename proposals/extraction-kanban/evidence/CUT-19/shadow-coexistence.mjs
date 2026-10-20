// CUT-19-a shadow check (README §10 data plan, cards/CUT-19.md §13): the same legacy/series fixture
// bytes are read (readStreamsAt) and reorganized (convergeWriterFiles) by the old hand-written verdict
// (base dist) and by wanxiangli/segments (candidate dist), each in its own temp copy; read results,
// reorganize results and the resulting directory bytes must be equal. decodeWriterText is compared on
// random texts too. Only temp fixture dirs; no real .git/wanxiangshu is touched.
// usage: node shadow-coexistence.mjs <base-dist> <candidate-dist> [rounds]
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { CanonicalEventCodec } from 'wanxiangli/events'
import { WriterSegments } from 'wanxiangli/segments'

const [baseDist, candDist, roundsArg] = process.argv.slice(2)
const load = async dist => import(resolve(dist, 'Persistence/EventStore/ProcessEventLog.js'))
const base = await load(baseDist)
const cand = await load(candDist)
const codec = new CanonicalEventCodec()
const enc = new TextEncoder()
const line = id => codec.encode({ id, stream: 's', type: 'JournalEnvelope', parents: [], payload: { n: id }, payloadRefs: [] })
let seed = 1919
const random = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const pick = list => list[Math.floor(random() * list.length)]
const ids = ['a', 'b', 'c', 'd', 'e', 'f']
const textOf = () => {
  let text = ''
  const count = Math.floor(random() * 4)
  for (let i = 0; i < count; i += 1) text += line(pick(ids))
  const r = random()
  if (r < 0.08 && text.length > 0) text = text.slice(0, Math.floor(random() * text.length))
  else if (r < 0.12) text += '\n'
  else if (r < 0.16) text += '{"x":1}\n'
  return text
}
const show = value => JSON.stringify(value, (_, v) => (v instanceof Uint8Array ? `bytes:${v.length}` : v))
const outcome = (fn, ...args) => {
  try {
    const result = fn(...args)
    if (result?.tag === 0) return { ok: Array.isArray(result.fields[0]) || result.fields[0]?.head !== undefined || typeof result.fields[0] === 'object' ? summarize(result.fields[0]) : result.fields[0] }
    return { error: show(result?.fields?.[0]) }
  } catch (error) { return { threw: String(error?.message ?? error) } }
}
const listOf = value => (value == null ? [] : Array.isArray(value) ? value : [...value])
const summarize = value => listOf(value).map(item => (Array.isArray(item) ? [item[0], listOf(item[1]).map(e => e.EventId)] : item?.EventId ?? item))
const snapshot = dir => readdirSync(dir).sort().map(name => [name, readFileSync(join(dir, name), 'utf8')])
const verdicts = {}
const tally = { verdicts, rounds: Number(roundsArg ?? 1500), decodeEqual: 0, readEqual: 0, convergeEqual: 0, differ: 0, kinds: {}, examples: [] }
for (let round = 0; round < tally.rounds; round += 1) {
  const text = textOf()
  const left = outcome(base.ProcessEventLogModule_decodeWriterText, 'w.ndjson', text)
  const right = outcome(cand.ProcessEventLogModule_decodeWriterText, 'w.ndjson', text)
  if (show(left) === show(right)) tally.decodeEqual += 1
  else { tally.differ += 1; if (tally.examples.length < 5) tally.examples.push({ text, left, right }) }

  const seriesText = textOf()
  const legacyText = random() < 0.3 ? seriesText + textOf() : random() < 0.4 ? seriesText.slice(0, Math.floor(random() * (seriesText.length + 1))) : textOf()
  const series = enc.encode(seriesText)
  const cut = random() < 0.5 ? series.length : Math.floor(random() * (series.length + 1))
  const files = { 'w.ndjson': enc.encode(legacyText), 'w-0000.ndjson': series.subarray(0, cut) }
  if (cut < series.length) files['w-0001.ndjson'] = series.subarray(cut)
  const verdict = new WriterSegments().coexistence(files['w.ndjson'], series)
  verdicts[verdict] = (verdicts[verdict] ?? 0) + 1
  const sides = [base, cand].map(side => {
    const root = mkdtempSync(join(tmpdir(), 'cut19a-shadow-'))
    const commonDir = join(root, '.Herdchestra')
    const events = join(commonDir, 'wanxiangshu', 'events')
    mkdirSync(events, { recursive: true })
    for (const [name, bytes] of Object.entries(files)) writeFileSync(join(events, name), bytes)
    const strip = value => JSON.parse(show(value).split(root).join('<root>'))
    const read = strip(outcome(side.ProcessEventLogModule_readStreamsAt, commonDir, Date.now()))
    const converge = strip(outcome(side.ProcessEventLogModule_convergeWriterFiles, commonDir, 'w'))
    const after = snapshot(events)
    rmSync(root, { recursive: true, force: true })
    return { read, converge, after }
  })
  const kind = sides[1].read.error ? (sides[1].read.error.includes('diverged') ? 'diverged' : 'readError') : sides[1].after.length === 1 ? 'convergedToOneForm' : 'kept'
  tally.kinds[kind] = (tally.kinds[kind] ?? 0) + 1
  const readSame = show(sides[0].read) === show(sides[1].read)
  const convergeSame = show(sides[0].converge) === show(sides[1].converge) && show(sides[0].after) === show(sides[1].after)
  if (readSame) tally.readEqual += 1
  if (convergeSame) tally.convergeEqual += 1
  if (!readSame || !convergeSame) { tally.differ += 1; if (tally.examples.length < 5) tally.examples.push({ files: Object.fromEntries(Object.entries(files).map(([k, v]) => [k, new TextDecoder().decode(v)])), base: sides[0], cand: sides[1] }) }
}
console.log(JSON.stringify(tally, null, 2))
process.exitCode = tally.differ === 0 ? 0 : 1
