export async function load(url, context, nextLoad) {
  const loaded = await nextLoad(url, context)
  if (!url.endsWith('/dist/Context/Prefix/Wire.js')) return loaded
  const source = loaded.source.toString()
  const expression = 'ProjectionRenderer_cutoffDigest(sha256Hex, snapshot, cutoff)'
  if (source.split(expression).length !== 2) {
    throw new Error('production Wire digest expression must occur exactly once')
  }
  const replacement = process.env.WANXIANGSHU_PREFIX_DIGEST_MUTATION === 'hash'
    ? 'ProjectionRenderer_cutoffDigest(() => "incorrect-digest", snapshot, cutoff)'
    : 'ProjectionRenderer_cutoffDigest(sha256Hex, snapshot, cutoff - 1)'
  return { ...loaded, source: source.replace(expression, replacement) }
}
