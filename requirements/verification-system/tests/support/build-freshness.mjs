import { existsSync, statSync } from 'node:fs'
import { relative, resolve } from 'node:path'

import { loopDetectorRepositoryInputFiles } from '../../../../scripts/lib/loop-detector-repository-corpus.mjs'
import { walk } from '../../../../scripts/lib/walk.mjs'
import { selectProductionModules } from './coverage-policy.mjs'

const newestFile = (files) => {
  let newest = null
  for (const file of files) {
    let stat
    try {
      stat = statSync(file)
    } catch {
      continue
    }
    if (newest === null || stat.mtimeMs > newest.mtimeMs) newest = { file, mtimeMs: stat.mtimeMs }
  }
  return newest
}

export const checkBuildFreshness = ({
  productionRoot = 'src/Wanxiangshu',
  buildRoot = 'dist',
  repositoryRoot,
  repositoryInputs = loopDetectorRepositoryInputFiles(repositoryRoot),
} = {}) => {
  const sources = collectBuildInputs({ productionRoot, repositoryInputs })
  if (sources.length === 0) return { ok: false, reason: `no build inputs found for ${productionRoot}/` }

  if (!existsSync(buildRoot)) {
    return { ok: false, reason: `${buildRoot}/ does not exist — run: npm run format-build-test` }
  }

  const artifacts = selectProductionModules(walk(buildRoot, ['.js']))
    .map((file) => resolve(file))

  if (artifacts.length === 0) {
    return { ok: false, reason: `${buildRoot}/ has no compiled output — run: npm run format-build-test` }
  }

  const newestSource = newestFile(sources)
  const newestArtifact = newestFile(artifacts)

  if (newestSource.mtimeMs > newestArtifact.mtimeMs) {
    const staleBy = Math.round((newestSource.mtimeMs - newestArtifact.mtimeMs) / 1000)
    return {
      ok: false,
      reason: [
        `${buildRoot}/ is stale by ${staleBy}s — run: npm run format-build-test`,
        `  newest source:   ${relative('.', newestSource.file)}`,
        `  newest artifact: ${relative('.', newestArtifact.file)}`,
      ].join('\n'),
    }
  }

  return { ok: true, sources: sources.length, artifacts: artifacts.length }
}

export const collectBuildInputs = ({
  productionRoot = 'src/Wanxiangshu',
  repositoryRoot,
  repositoryInputs = loopDetectorRepositoryInputFiles(repositoryRoot),
} = {}) => {
  const sources = [
    ...walk(productionRoot, ['.fs']),
    ...walk(productionRoot, ['.fsproj']),
    ...repositoryInputs,
  ].map((file) => resolve(file))

  return [...new Set(sources)]
}
