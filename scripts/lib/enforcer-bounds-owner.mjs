const normalize = (path) => path.replace(/\\/g, '/')

const ownerPath = 'Cycle/Model.fs'
const requiredConsumers = ['Cycle/Decode.fs', 'Surface.fs']
const limitDefinition = /\blet\s+(?:(?:private|internal)\s+)?Max(?:BlogText|Evidence)Bytes\s*=/
const rawLimit = /(?:512\s*\*\s*1024|128\s*\*\s*1024|524288|131072)/
const directByteDecision = /(?:LlmFacing\.byteCount|Encoding\.UTF8\.GetByteCount)[^\n]{0,160}>/
const sharedDecision = /EnforcerCycle\.validateContentBounds\s+LlmFacing\.byteCount/

export const inspectEnforcerBoundsSources = (entries) => {
  const sources = new Map(entries.map(({ path, text }) => [normalize(path), text]))
  const problems = []
  const owner = sources.get(ownerPath)

  if (owner === undefined) return [`${ownerPath}: bounds owner missing`]
  if (!/\blet\s+MaxBlogTextBytes\s*=\s*512\s*\*\s*1024/.test(owner)) {
    problems.push(`${ownerPath}: canonical 512 KiB text limit missing`)
  }
  if (!/\blet\s+MaxEvidenceBytes\s*=\s*128\s*\*\s*1024/.test(owner)) {
    problems.push(`${ownerPath}: canonical 128 KiB evidence limit missing`)
  }
  if (!/\blet\s+validateContentBounds\b/.test(owner)) {
    problems.push(`${ownerPath}: validateContentBounds decision missing`)
  }

  for (const consumerPath of requiredConsumers) {
    const consumer = sources.get(consumerPath)
    if (consumer === undefined) problems.push(`${consumerPath}: required bounds consumer missing`)
    else if (!sharedDecision.test(consumer)) problems.push(`${consumerPath}: must consume EnforcerCycle.validateContentBounds`)
  }

  for (const [path, text] of sources) {
    if (path === ownerPath) continue
    if (limitDefinition.test(text)) problems.push(`${path}: duplicates a bounds constant`)
    if (rawLimit.test(text)) problems.push(`${path}: duplicates a raw bounds threshold`)
    if (directByteDecision.test(text)) problems.push(`${path}: duplicates the byte-bound decision formula`)
  }

  return problems
}
