#!/usr/bin/env node
// Test/Fable-boundary ratchet (Wave 1, Proposal ch. 19).
//
// The anti-corruption boundary is requirements/verification-system/tests/support/domain/
// (moved from tests/unit/support/domain/ during the requirements cutover) — the ONLY
// place tests may touch Fable internals (dist/fable_modules/**). Ordinary unit and
// integration tests must reach Fable shapes through the domain adapters.
//
// Existing violations (imports of dist/fable_modules/** in *.test.mjs that
// predate this check) are grandfathered in EMBEDDED_BASELINE and tolerated;
// NEW violations fail. This is a semantic anti-corruption boundary, not a
// size heuristic: compiler-runtime imports must not spread.
//
// Modes:
//   node scripts/checks/test-boundary.mjs
//       exit 1 when a *.test.mjs outside the baseline imports dist/fable_modules/**
//   node scripts/checks/test-boundary.mjs --generate [--out=<file>]
//       write the current violation set as a baseline JSON (for refreshing
//       EMBEDDED_BASELINE after legacy debt is paid down)

import { readFileSync, writeFileSync, existsSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { walk } from '../lib/walk.mjs'

const DEFAULT_OUT = join(dirname(fileURLToPath(import.meta.url)), 'test-boundary-baseline.json')
const FABLE_PATTERN = /dist[/\\]fable_modules/

// ── baseline (frozen at Wave 1 split; shrink with --generate) ──────────────
// Grandfathered direct imports of dist/fable_modules/** in *.test.mjs.
const EMBEDDED_BASELINE = {
  "tests/unit/agent/inquiry-permissions.test.mjs::import { toArray as setToArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'": "import { toArray as setToArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'",
  "tests/unit/enforcer/observation-pair.test.mjs::import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/host/session-flattening.test.mjs::import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/host/session-flattening.test.mjs::import { FSharpResult$2_Ok as ok } from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'": "import { FSharpResult$2_Ok as ok } from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'",
  "tests/unit/js-tools/js-bindings.test.mjs::import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'": "import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'",
  "tests/unit/js-tools/js-surface.test.mjs::import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'": "import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'",
  "tests/unit/js-tools/js-tool-host.test.mjs::import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'": "import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'",
  "tests/unit/js-tools/js-workflow.test.mjs::import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'": "import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/Set.js'",
  "tests/unit/plugin/tool-host-codec-full.test.mjs::readdirSync('dist/fable_modules').find((entry) => entry.startsWith('fable-library-js.')),": "readdirSync('dist/fable_modules').find((entry) => entry.startsWith('fable-library-js.')),",
  "tests/unit/process/large-gate.test.mjs::readdirSync('dist/fable_modules').find((entry) => entry.startsWith('fable-library-js.')),": "readdirSync('dist/fable_modules').find((entry) => entry.startsWith('fable-library-js.')),",
  "tests/unit/process/process-output.test.mjs::'../../../dist/fable_modules/fable-library-js.5.13.0/TimeSpan.js'": "'../../../dist/fable_modules/fable-library-js.5.13.0/TimeSpan.js'",
  "tests/unit/process/process-runner.test.mjs::const { fromSeconds } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/TimeSpan.js')": "const { fromSeconds } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/TimeSpan.js')",
  "tests/unit/process/pty-supervisor.test.mjs::import { StringBuilder__Append_Z721C83C5 } from '../../../dist/fable_modules/fable-library-js.5.13.0/System.Text.js'": "import { StringBuilder__Append_Z721C83C5 } from '../../../dist/fable_modules/fable-library-js.5.13.0/System.Text.js'",
  "tests/unit/session/causal-wait-bridge.test.mjs::import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/session/satellite-runtime.test.mjs::import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/session/satellite-runtime.test.mjs::} from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'": "} from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'",
  "tests/unit/strength/batch-collector.test.mjs::import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/durability-port.test.mjs::import { ofArray as toList } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/frame-projection.test.mjs::import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/host-canary-k0.test.mjs::import { toArray as mapEntries } from '../../../dist/fable_modules/fable-library-js.5.13.0/Map.js'": "import { toArray as mapEntries } from '../../../dist/fable_modules/fable-library-js.5.13.0/Map.js'",
  "tests/unit/strength/invisibility.test.mjs::import { ofArray as toList } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/lifecycle-recovery.test.mjs::import { FSharpResult$2_Ok as ok } from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'": "import { FSharpResult$2_Ok as ok } from '../../../dist/fable_modules/fable-library-js.5.13.0/Result.js'",
  "tests/unit/strength/lifecycle-recovery.test.mjs::import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/predictor-rollout.test.mjs::import { ofArray as toList } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/projection-adapter.test.mjs::import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/projection-algebra.test.mjs::import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/replica-transform.test.mjs::import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/strength/runtime.test.mjs::import { toArray as mapEntries } from '../../../dist/fable_modules/fable-library-js.5.13.0/Map.js'": "import { toArray as mapEntries } from '../../../dist/fable_modules/fable-library-js.5.13.0/Map.js'",
  "tests/unit/strength/store.test.mjs::import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'",
  "tests/unit/tools/file-tools.test.mjs::readdirSync('dist/fable_modules').find((entry) => entry.startsWith('fable-library-js.')),": "readdirSync('dist/fable_modules').find((entry) => entry.startsWith('fable-library-js.')),",
  "tests/unit/tools/join-tool-family.test.mjs::const { ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')": "const { ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')",
  "tests/unit/tools/join-tool-family.test.mjs::const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')": "const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')",
  "tests/unit/tools/list-tool.test.mjs::const { add: mapAdd, ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')": "const { add: mapAdd, ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')",
  "tests/unit/tools/list-tool.test.mjs::const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')": "const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')",
  "tests/unit/tools/oneshot-tools.test.mjs::import { uncurry2 } from '../../../dist/fable_modules/fable-library-js.5.13.0/Util.js'": "import { uncurry2 } from '../../../dist/fable_modules/fable-library-js.5.13.0/Util.js'",
  "tests/unit/tools/verdict-tool-extras.test.mjs::const { ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')": "const { ofList: mapOfList } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Map.js')",
  "tests/unit/tools/verdict-tool-extras.test.mjs::const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')": "const { compare } = await import('../../../dist/fable_modules/fable-library-js.5.13.0/Util.js')",
  "tests/integration/strength/lifecycle.test.mjs::import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'": "import { ofArray as toList, toArray as listItems } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'"
}

const args = process.argv.slice(2)
const argValue = (flag) => {
  const inline = args.find((a) => a.startsWith(`${flag}=`))
  if (inline) return inline.slice(flag.length + 1)
  const index = args.indexOf(flag)
  return index >= 0 ? args[index + 1] : undefined
}

/** { "<file>": "<trimmed violating line>", ... } for every violating line. */
const scanViolations = (root) => {
  const out = {}
  const scopes = [
    ['unit', join(root, 'tests', 'unit')],
    ['integration', join(root, 'tests', 'integration')],
    ['requirements', join(root, 'requirements')],
  ]
  for (const [, base] of scopes) {
    if (!existsSync(base)) continue
    for (const abs of walk(base, ['.test.mjs'])) {
      const rel = relative(root, abs).replace(/\\/g, '/')
      const lines = readFileSync(abs, 'utf8').split('\n')
      lines.forEach((line) => {
        if (FABLE_PATTERN.test(line)) {
          // Key on file + normalized content: a re-indented or re-ordered line
          // does not count as a NEW violation; changed content does.
          out[`${rel}::${line.trim()}`] = line.trim()
        }
      })
    }
  }
  return out
}

const main = () => {
  const root = resolve(argValue('--root') ?? '.')
  if (args.includes('--generate')) {
    const out = argValue('--out') ?? DEFAULT_OUT
    writeFileSync(out, `${JSON.stringify(scanViolations(root), null, 2)}\n`)
    const total = Object.keys(scanViolations(root)).length
    console.log(`test-boundary: baseline written to ${out} — ${total} grandfathered violation(s)`)
    return
  }
  // Prefer a refreshed baseline file if present; otherwise the embedded one.
  const baseline = existsSync(DEFAULT_OUT)
    ? JSON.parse(readFileSync(DEFAULT_OUT, 'utf8'))
    : EMBEDDED_BASELINE
  const current = scanViolations(root)
  const fresh = []
  for (const key of Object.keys(current)) {
    if (!(key in baseline)) fresh.push(current[key])
  }
  const gone = Object.keys(baseline).filter((key) => !(key in current))
  const total = Object.keys(current).length
  if (fresh.length > 0) {
    console.error(`test-boundary: ${fresh.length} NEW violation(s) (${total} total; ${Object.keys(baseline).length} baselined):`)
    for (const v of fresh.sort()) console.error(`  ${v}`)
    process.exit(1)
  }
  if (gone.length > 0) {
    console.log(`test-boundary: ok (${total} baseline violation(s), ${gone.length} resolved — run --generate to shrink the baseline)`)
  } else {
    console.log(`test-boundary: ok (${total} baseline violation(s) tolerated, no new direct Fable-module imports)`)
  }
}

main()
