// Reference transliteration of the removed Seat.latestAssistantProse/assistantProse/nonBlank
// (base e6d490d7a: List.rev |> List.tryPick, String.Equals(role,"assistant",OrdinalIgnoreCase),
// String.IsNullOrWhiteSpace) vs wanxiangli/advisor answer, over seeded random message lists.
import { pathToFileURL } from 'node:url'
const { AdvisorLaw } = await import(pathToFileURL(process.argv[2]))
const law = new AdvisorLaw()
const old = (ms) => { for (const m of [...ms].reverse()) { if (m.role != null && m.role.toLowerCase() === 'assistant' && !/^\s*$/.test(m.text)) return m.text } return null }
let seed = 7, mism = 0, nonNull = 0
const rnd = () => ((seed = (seed * 1103515245 + 12345) >>> 0) / 2 ** 32)
const pick = (xs) => xs[Math.floor(rnd() * xs.length)]
for (let i = 0; i < 20000; i++) {
  const ms = Array.from({ length: Math.floor(rnd() * 6) }, () => ({ role: pick(['assistant', 'Assistant', 'ASSISTANT', 'user', 'tool', null]), text: pick(['', ' ', '\n\t', 'advice', ' x ', '名']) }))
  const a = old(ms), b = law.answer(ms); if (a !== b) mism++; if (b !== null) nonNull++
}
console.log(JSON.stringify({ cases: 20000, nonNull, mismatches: mism }))
