// Rule-level differential for sub-scope ④: the pre-cut host decision RelayNarrativeTransform.staleRetirement
// (master 2eab66e14, transcribed line by line below from the F#, with the journal lookups already resolved into
// plain evidence) vs wanxiangli/projection retired(). The host-level evidence is the work/host-boundary suite
// (verdicts identical) plus mutants E/F on the compiled host; this harness covers the decision space densely.
// MUT=ge|first|nocut flips one rule on the old side to show the comparison is sensitive.
import { Projection } from 'wanxiangli/projection'
const lib = Projection()
const MUT = process.env.MUT ?? ''
function oldStale({ relayed, acceptedRequest, activeRoot, gatePhysical, messages }) {
  if (!relayed) return false                                       // relayedCut |> Option.filter ...
  const isUser = (m) => m.role != null && m.role.toLowerCase() === 'user'   // messageRoleIsUser
  let lastUserIndex = null                                         // List.indexed |> List.rev |> tryPick
  for (let i = messages.length - 1; i >= 0; i--) if (isUser(messages[i])) { lastUserIndex = i; break }
  if (MUT === 'first') { lastUserIndex = messages.findIndex(isUser); if (lastUserIndex < 0) lastUserIndex = null }
  const physical = lastUserIndex === null ? null : messages[lastUserIndex].id   // tryItem |> bind messageId
  let cutIndex = messages.findIndex((m) => m.cut); if (cutIndex < 0) cutIndex = null   // cutToolIndex
  const physicalAfterCut = lastUserIndex !== null && cutIndex !== null &&
    (MUT === 'ge' ? lastUserIndex >= cutIndex : lastUserIndex > cutIndex)            // afterCut
  const freshRoot = physicalAfterCut ? activeRoot : null
  const successorIdentity = physical !== null &&
    (freshRoot === physical || gatePhysical === physical || (physicalAfterCut && acceptedRequest))
  const cutPresent = MUT === 'nocut' ? false : cutIndex !== null
  const accepted = physical === null ? null : messages[lastUserIndex].accepted  // ChatExecutionProjection.byKey physical
  const acceptedRunIdentity = !cutPresent && physical !== null && accepted !== null && activeRoot !== null && accepted === activeRoot
  return !(successorIdentity || acceptedRunIdentity)
}
let seed = Number(process.argv[2] ?? 1)
const rnd = (n) => { seed = (seed + 0x6d2b79f5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return (((t ^ (t >>> 14)) >>> 0) % n) }
const pick = (a) => a[rnd(a.length)]
const ids = ['u1', 'u2', 'u3', null]
const rounds = Number(process.argv[3] ?? 20000)
let cmp = 0, diverge = 0, stale = 0
for (let r = 0; r < rounds; r++) {
  const messages = Array.from({ length: rnd(6) }, () => ({
    role: pick(['user', 'User', 'USER', 'assistant', 'tool', 'system', null]),
    id: pick(ids), cut: rnd(5) === 0, accepted: pick([null, null, 'u1', 'u2', 'u3']),
  }))
  const request = { relayed: rnd(6) !== 0, acceptedRequest: rnd(2) === 0, activeRoot: pick(ids), gatePhysical: pick(ids), messages }
  const a = oldStale(request), b = lib.retired(request)
  cmp++; if (a) stale++
  if (a !== b) { diverge++; if (diverge <= 3) console.log('DIVERGE', a, b, JSON.stringify(request)) }
}
console.log(JSON.stringify({ seed: process.argv[2] ?? 1, mutant: MUT || null, comparisons: cmp, divergences: diverge, staleByOld: stale }))
