// Thin shim kept isolated under tests/: `compile-impact.mjs` was retired
// in W4, but this suite's semantics (focused compile + `--output` +
// --scratch hash) must still live. This runner speaks the same
// compileIncremental contract as `scripts/build.mjs` — no manifest writes,
// no dist writes — while still running one real Fable compile per call.
// Test code stays untouched; only the entrypoint rewires to a script that
// is in-scope for tests to drive.
import { resolve, dirname } from 'node:path'

import {
  compileIncremental,
  detectChangedFiles,
  DEFAULT_SCRATCH_ROOT,
} from '../../../../scripts/lib/owner-compile.mjs'

const args = process.argv.slice(2)

const props = []
const changed = []
for (let index = 0; index < args.length; index++) {
  const arg = args[index]
  if (arg === '--aggregate' || arg === '--projects' || arg === '--props' || arg === '--scratch' || arg === '-o' || arg === '--output') {
    props.push({ name: arg === '-o' ? '--output' : arg, value: args[++index] })
  } else if (arg.startsWith('--')) {
    console.error(`unknown flag: ${arg}`)
    process.exit(1)
  } else {
    changed.push(arg)
  }
}

const aggregateIndex = props.findIndex((arg) => arg.name === '--aggregate')
if (aggregateIndex === -1) {
  console.error('--aggregate required')
  process.exit(1)
}

const aggregatePath = resolve(props[aggregateIndex].value)
const scratchRoot = resolve(props.find((arg) => arg.name === '--scratch')?.value ?? DEFAULT_SCRATCH_ROOT)
const rootPropsPath = resolve(props.find((arg) => arg.name === '--props')?.value ?? 'Directory.Build.props')
const outputDir = resolve(props.find((arg) => arg.name === '--output')?.value ?? 'out')

let changedPaths = changed
if (changedPaths.length === 0) {
  changedPaths = detectChangedFiles({ aggregatePath, outputDir }).changedPaths
}

if (changedPaths.length === 0) {
  console.log('[impact-cli] no inputs changed')
  process.exit(0)
}

const result = await compileIncremental({
  changedPaths,
  aggregatePath,
  scratchRoot,
  rootPropsPath,
  outputDir,
})
if (!result.ok) {
  console.error(result.stderr ?? result.stdout ?? 'compile failed')
  process.exit(result.code ?? 1)
}
console.log(`[impact-compile] compiled ${result.mode} impact (${result.compileItems.length} items)`)
