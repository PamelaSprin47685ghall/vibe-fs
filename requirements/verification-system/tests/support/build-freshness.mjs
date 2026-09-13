import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { assertBuildFresh, collectCompilerInputs, collectOutputs } from '../../../../scripts/lib/build-state.mjs'

const defaultRepoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..')

export const checkBuildFreshness = ({
  root = defaultRepoRoot,
  productionRoot = 'src/Wanxiangshu',
  buildRoot = 'dist',
  repositoryRoot,
} = {}) => {
  const targetRoot = repositoryRoot ?? root
  try {
    const result = assertBuildFresh({ root: targetRoot })
    const compilerInputs = collectCompilerInputs(targetRoot)
    const outputs = collectOutputs(path.resolve(targetRoot, buildRoot))
    return {
      ok: true,
      generation: result.generation,
      sources: compilerInputs.length,
      artifacts: Object.keys(outputs).length,
      ...result,
    }
  } catch (err) {
    return {
      ok: false,
      code: err.code ?? 'stale',
      path: err.path,
      reason: err.reason ?? err.message,
    }
  }
}

export const collectBuildInputs = ({
  root = defaultRepoRoot,
  repositoryRoot,
} = {}) => {
  const targetRoot = repositoryRoot ?? root
  return collectCompilerInputs(targetRoot).map((e) => path.resolve(targetRoot, e.path))
}
