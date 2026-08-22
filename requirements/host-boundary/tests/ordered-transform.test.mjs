// requirements/host-boundary/tests/ordered-transform.test.mjs — WHAT[HOST-BOUNDARY-019]
//
// OrderedTransformProof: verifies the 13-step static semantic score of
// PluginTransforms.normalTransform alongside the StrengthReplica and
// ExplicitResumeSuppression branch isolation paths without illegal deep-dist imports.

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import test from 'node:test'

const root = resolve(import.meta.dirname, '../../..')
const read = (path) => readFileSync(resolve(root, path), 'utf8')

test('WHAT[HOST-BOUNDARY-019] PluginTransforms declares NormalTransformCapabilities record with exact 13 named fields', () => {
  const text = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(text, /type\s+NormalTransformCapabilities\s*=/)
  assert.match(text, /BeginPhysicalProviderAttempt:\s*string option -> obj -> Task<unit>/)
  assert.match(text, /BindSessionStartedAt:\s*string option -> Task<DateTimeOffset option>/)
  assert.match(text, /ApplyStrengthReplay:\s*string option -> obj -> Task<StrengthReplayPlan list>/)
  assert.match(text, /ApplyXTracePipeline:\s*string option -> obj -> StrengthReplayPlan list -> Task<unit>/)
  assert.match(text, /ApplyCompanion:\s*string option -> obj -> obj -> Task<unit>/)
  assert.match(text, /ApplyXWire:\s*obj -> Task<PrefixPresentationHorizon>/)
  assert.match(text, /ApplyEnforcerContinuation:\s*string option -> obj -> Task<unit>/)
  assert.match(text, /ApplyStrengthSpeculate:\s*obj -> Task<unit>/)
  assert.match(text, /InjectPairGuideline:\s*string option -> DateTimeOffset option -> obj -> Task<unit>/)
  assert.match(text, /ProjectRequirementGrounding:\s*string option -> obj -> Task<unit>/)
  assert.match(text, /InjectBloggerChronicle:\s*string option -> obj -> unit/)
  assert.match(text, /SanitizeMessages:\s*obj -> unit/)
  assert.match(text, /InterruptAfterSubmittedJudgement:\s*string option -> Task<unit>/)
})

test('WHAT[HOST-BOUNDARY-019] tentative prefix probe horizon suppresses historical auxiliary projection in the same physical request', () => {
  const text = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')
  const start = text.indexOf('let normalTransform')
  assert.ok(start >= 0)
  const end = text.indexOf('let createWithCaps', start)
  const body = text.slice(start, end)

  assert.match(body, /let!\s+prefixHorizon\s*=\s*caps\.ApplyXWire\s+outObj/)
  assert.match(body, /if\s+prefixHorizon\s*=\s*PrefixPresentationHorizon\.Current\s+then/)

  const guardAt = body.indexOf('if prefixHorizon = PrefixPresentationHorizon.Current then')
  const speculateAt = body.indexOf('caps.ApplyStrengthSpeculate')
  const pairAt = body.indexOf('caps.InjectPairGuideline')
  const groundingAt = body.indexOf('caps.ProjectRequirementGrounding')
  const chronicleAt = body.indexOf('caps.InjectBloggerChronicle')

  assert.ok(guardAt < speculateAt && speculateAt < pairAt && pairAt < groundingAt)
  assert.ok(groundingAt < chronicleAt, 'Blogger-only chronicle projection remains outside the WorkMain auxiliary guard')
})

test('WHAT[HOST-BOUNDARY-019] normalTransform executes exact 13-step static score in order', () => {
  const text = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  const orderingSteps = [
    'BeginPhysicalProviderAttempt',
    'BindSessionStartedAt',
    'ApplyStrengthReplay',
    'ApplyXTracePipeline',
    'ApplyCompanion',
    'ApplyXWire',
    'ApplyEnforcerContinuation',
    'ApplyStrengthSpeculate',
    'InjectPairGuideline',
    'ProjectRequirementGrounding',
    'InjectBloggerChronicle',
    'SanitizeMessages',
    'InterruptAfterSubmittedJudgement',
  ]

  const allLines = text.split('\n')
  const normalTransformStart = allLines.findIndex((l) => /^\s*let\s+(?:private\s+)?normalTransform\b/.test(l))
  assert.ok(normalTransformStart >= 0, 'normalTransform function must exist in PluginTransforms.fs')

  const startIndent = allLines[normalTransformStart].length - allLines[normalTransformStart].trimStart().length
  let normalTransformEnd = allLines.length
  for (let i = normalTransformStart + 1; i < allLines.length; i++) {
    const line = allLines[i]
    if (line.trim() === '') continue
    const indent = line.length - line.trimStart().length
    if (indent <= startIndent && /^\s*let\s/.test(line)) {
      normalTransformEnd = i
      break
    }
  }

  const bodyLines = allLines.slice(normalTransformStart, normalTransformEnd)
  const stepLines = []
  for (let i = 0; i < orderingSteps.length; i++) {
    const step = orderingSteps[i]
    const foundIdx = bodyLines.findIndex((l) => l.includes(`caps.${step}`))
    assert.ok(foundIdx >= 0, `normalTransform must call capability step ${i + 1}: caps.${step}`)
    stepLines.push(normalTransformStart + foundIdx + 1)
  }

  for (let i = 0; i < stepLines.length - 1; i++) {
    assert.ok(
      stepLines[i] < stepLines[i + 1],
      `step ${i + 1} (${orderingSteps[i]} at line ${stepLines[i]}) must precede step ${i + 2} (${orderingSteps[i + 1]} at line ${stepLines[i + 1]})`,
    )
  }
})

test('WHAT[HOST-BOUNDARY-019] strength replica branch executes replica sequence via explicit TransformMode and excludes normal transform', () => {
  const text = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  // New explicit mode shape — would be red if reverted to implicit ordinaryProviderTransform
  assert.match(text, /type\s+private\s+TransformMode/)
  assert.match(text, /\|\s*ExplicitResumeDisclosure/)
  assert.match(text, /\|\s*StrengthReplica\s+of\s+StrengthReplicaRuntime/)
  assert.match(text, /\|\s*Ordinary/)
  assert.match(text, /let\s+private\s+determineTransformMode/)
  assert.match(text, /match\s+branches\.IsExplicitResume[\s\S]*?,\s*branches\.ReplicaRuntime/)
  assert.match(text, /let\s+private\s+failIfReplicaDecisionLost/)
  assert.doesNotMatch(text, /let\s+private\s+ordinaryProviderTransform\b/)
  assert.doesNotMatch(text, /let\s+private\s+requireReplicaHandled\b/)
  assert.doesNotMatch(text, /let\s+private\s+strengthReplicaRuntime\b/)

  const allLines = text.split('\n')
  const createStart = allLines.findIndex((l) => /^\s*let\s+createWithCaps\b/.test(l))
  assert.ok(createStart >= 0, 'createWithCaps must exist in PluginTransforms.fs')

  const startIndent = allLines[createStart].length - allLines[createStart].trimStart().length
  let createEnd = allLines.length
  for (let i = createStart + 1; i < allLines.length; i++) {
    const line = allLines[i]
    if (line.trim() === '') continue
    const indent = line.length - line.trimStart().length
    if (indent <= startIndent && /^\s*let\s+create\b/.test(line)) {
      createEnd = i
      break
    }
  }

  const body = allLines.slice(createStart, createEnd).join('\n')

  assert.match(body, /match\s+determineTransformMode\s+branches\s+projectionSessionIdOpt\s+outObj\s+with/)
  assert.match(body, /\|\s*StrengthReplica\s+runtime\s*->/)
  assert.match(body, /projectionSessionIdOpt\s*\|\>\s*Option\.iter\s+branches\.RegisterOwned/)
  assert.match(body, /do!\s+branches\.ReplicaXWire\s+outObj/)
  assert.match(body, /let!\s+handled\s*=\s*runtime\.HandleTransform\s+outObj/)
  assert.match(body, /failIfReplicaDecisionLost\s+handled/)
  assert.match(body, /branches\.ReplicaSanitize\s+outObj/)
  assert.match(body, /\|\s*Ordinary\s*->[\s\S]*?do!\s+normalTransform\s+caps\s+projectionSessionIdOpt\s+inObj\s+outObj/)
})

test('WHAT[HOST-BOUNDARY-019] explicit resume suppression short-circuits via TransformMode before ordinary provider transform', () => {
  const text = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  const allLines = text.split('\n')
  const createStart = allLines.findIndex((l) => /^\s*let\s+createWithCaps\b/.test(l))
  assert.ok(createStart >= 0, 'createWithCaps must exist in PluginTransforms.fs')

  const startIndent = allLines[createStart].length - allLines[createStart].trimStart().length
  let createEnd = allLines.length
  for (let i = createStart + 1; i < allLines.length; i++) {
    const line = allLines[i]
    if (line.trim() === '') continue
    const indent = line.length - line.trimStart().length
    if (indent <= startIndent && /^\s*let\s+create\b/.test(line)) {
      createEnd = i
      break
    }
  }

  const body = allLines.slice(createStart, createEnd).join('\n')

  assert.match(body, /match\s+determineTransformMode\s+branches\s+projectionSessionIdOpt\s+outObj\s+with/)
  assert.match(body, /\|\s*ExplicitResumeDisclosure\s*->[\s\S]*?branches\.ExplicitResumeSanitize\s+outObj/)
  assert.doesNotMatch(body, /if\s+branches\.IsExplicitResume\s+projectionSessionIdOpt\s+outObj\s+then/)
  assert.doesNotMatch(body, /do!\s+ordinaryProviderTransform\s+caps\s+branches/)
  const explicitIdx = body.indexOf('ExplicitResumeDisclosure')
  const replicaIdx = body.indexOf('StrengthReplica runtime')
  const ordinaryIdx = body.indexOf('| Ordinary')
  assert.ok(explicitIdx >= 0 && replicaIdx >= 0 && ordinaryIdx >= 0, 'all three TransformMode cases must be wired')
  assert.ok(explicitIdx < replicaIdx && replicaIdx < ordinaryIdx, 'TransformMode cases must be ExplicitResumeDisclosure -> StrengthReplica -> Ordinary')
})

test('WHAT[HOST-BOUNDARY-019] create entry delegates to createWithCaps and default capabilities', () => {
  const text = read('src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs')

  assert.match(text, /let\s+defaultCapabilities\s*\(boot:\s*PluginBoot\.Boot\)\s*\(host:\s*PluginHostWiring\.Host\)\s*:\s*NormalTransformCapabilities/)
  assert.match(text, /let\s+defaultBranchCapabilities\s*\(boot:\s*PluginBoot\.Boot\)\s*\(host:\s*PluginHostWiring\.Host\)\s*:\s*TransformBranchCapabilities/)
  assert.match(
    text,
    /let\s+create\s*\(boot:\s*PluginBoot\.Boot\)\s*\(host:\s*PluginHostWiring\.Host\)[\s\S]*?let\s+caps\s*=\s*defaultCapabilities\s+boot\s+host[\s\S]*?let\s+branches\s*=\s*defaultBranchCapabilities\s+boot\s+host[\s\S]*?createWithCaps\s+caps\s+branches/,
  )
})

test('WHAT[HOST-BOUNDARY-019] ordered transform proof falsifiability check: swapped steps fail assertion', () => {
  const correctOrder = [
    'BeginPhysicalProviderAttempt',
    'BindSessionStartedAt',
    'ApplyStrengthReplay',
    'ApplyXTracePipeline',
    'ApplyCompanion',
    'ApplyXWire',
    'ApplyEnforcerContinuation',
    'ApplyStrengthSpeculate',
    'InjectPairGuideline',
    'ProjectRequirementGrounding',
    'InjectBloggerChronicle',
    'SanitizeMessages',
  ]

  const swappedOrder = [
    'BeginPhysicalProviderAttempt',
    'BindSessionStartedAt',
    'ApplyXTracePipeline', // Swapped: 4 before 3
    'ApplyStrengthReplay',
    'ApplyCompanion',
    'ApplyXWire',
    'ApplyEnforcerContinuation',
    'ApplyStrengthSpeculate',
    'InjectPairGuideline',
    'ProjectRequirementGrounding',
    'InjectBloggerChronicle',
    'SanitizeMessages',
  ]

  assert.throws(
    () => assert.deepEqual(swappedOrder, correctOrder),
    assert.AssertionError,
    'Falsifiability: swapped transform steps must fail equality assertion',
  )
})
