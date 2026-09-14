// Thin shim kept isolated under tests/: `compile-impact.mjs` was retired
// in W4, but this suite's semantics (focused compile + `--output` +
// `--scratch` hash) must still live. This runner speaks the same
// compileIncremental contract as `scripts/build.mjs` — no manifest writes,
// no dist writes — while still running one real Fable compile per call.
import { resolve } from 'node:path'

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
  if (arg === '--projects' || arg === '--props' || arg === '--scratch' || arg === '-o' || arg === '--output') {
    props.push({ name: arg === '-o' ? '--output' : arg, value: args[++index] })
  } else if (arg.startsWith('--')) {
    console.error(`unknown flag: ${arg}`)
    process.exit(1)
  } else {
    changed.push(arg)
  }
}

const projectsIndex = props.findIndex((arg) => arg.name === '--projects')
if (projectsIndex === -1) {
  console.error('--projects required')
  process.exit(1)
}

// `--projects` is the directory `discoverOwnerProjects` walks for the
// `Wanxiangshu.Owner.*.fsproj` shard graph. `compileIncremental` keeps
// caller-supplied roots (build root / scratch / output) orthogonal.
const projectDirectory = resolve(props[projectsIndex].value)
const scratchRoot = resolve(props.find((arg) => arg.name === '--scratch')?.value ?? DEFAULT_SCRATCH_ROOT)
const rootPropsPath = resolve(props.find((arg) => arg.name === '--props')?.value ?? 'Directory.Build.props')
const outputDir = resolve(props.find((arg) => arg.name === '--output')?.value ?? 'out')

// Changed paths must be explicit — the production `build.mjs` detects via
// manifest diff; the fixture contract is that a CLI caller names the
// changed file.
let effectiveChanged = changed
if (effectiveChanged.length === 0) {
  const detection = detectChangedFiles({
    aggregatePath: null,
    projectDirectory,
    outputDir,
    manifestPath: resolve(scratchRoot, 'impact-manifest.json'),
  })
  effectiveChanged = detection.changedPaths
}

if (effectiveChanged.length === 0) {
  console.log('[impact-cli] no inputs changed')
  process.exit(0)
}

const result = await compileIncremental({
  changedPaths: effectiveChanged,
  root: projectDirectory,
  projectDirectory,
  scratchRoot,
  rootPropsPath,
  outputDir,
  // Pipe stdio: capture Fable diagnostics onto stderr on failure; on success
  // the boilerplate banner does not pollute the [impact-cli] log line.
  stdio: 'pipe',
})
if (result.ok) {
  // Emit the [owner-compile] success trace unconditionally — with
  // `stdio: 'pipe'` compileIncremental does not echo it, and the test
  // surface asserts on that banner as the "real Fable completed" marker.
  console.log(`[owner-compile] OK: Wanxiangshu.Impact.fsproj in ${result.elapsedMs}ms (fp:${result.fingerprint?.slice(0, 12) ?? 'n/a'})`)
}
if (!result.ok) {
  console.error(`[impact-cli] compile failed (code=${result.code})`)
  if (result.stderr) console.error(result.stderr)
  process.exit(result.code ?? 1)
}

console.log(`[impact-cli] compiled ${result.mode} impact (${result.compileItems?.length ?? 0} items)`)
