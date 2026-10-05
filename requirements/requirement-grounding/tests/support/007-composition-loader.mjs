import assert from 'node:assert/strict'

export async function load(url, context, nextLoad) {
  const loaded = await nextLoad(url, context)
  const mutation = process.env.WANXIANGSHU_GROUNDING_COMPOSITION_MUTATION
  if (url.endsWith('/dist/OpenCode/Host/PairProgrammingThoughtTransform.js') && mutation === 'reversed-delivery') {
    const source = String(loaded.source)
    const placement = '= projectCapturedGuidance(captures, markerTexts, message)'
    assert.equal(source.split(placement).length - 1, 1, 'mutation identifies the production canonical guidance placement')
    return { ...loaded, source: source.replace(placement, '= undefined') }
  }
  if (!url.endsWith('/dist/OpenCode/Plugin/PluginTransforms.js')) return loaded
  const guidance = 'caps.InjectPairGuideline(projectionSessionIdOpt, sessionStartedAt, outObj)'
  const grounding = 'caps.ProjectRequirementGrounding(projectionSessionIdOpt, outObj)'
  const source = String(loaded.source)
  assert.equal(source.split(guidance).length - 1, 1, 'mutation identifies the actual production guidance call')
  assert.equal(source.split(grounding).length - 1, 1, 'mutation identifies the actual production grounding call')
  switch (mutation) {
    case 'missing-guidance':
      return { ...loaded, source: source.replace(guidance, 'Promise.resolve()') }
    case 'missing-grounding':
      return { ...loaded, source: source.replace(grounding, 'Promise.resolve()') }
    case 'reversed-calls':
    case 'reversed-delivery':
      return { ...loaded, source: source.replace(guidance, '__GROUNDING_FIRST__').replace(grounding, guidance).replace('__GROUNDING_FIRST__', grounding) }
    default:
      throw new Error('unknown grounding composition mutation')
  }
}
