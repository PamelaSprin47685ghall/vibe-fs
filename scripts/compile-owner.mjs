#!/usr/bin/env node

import { basename, resolve } from 'node:path'
import {
  compileOwnerProject,
  materializeOwnerCompile,
  planOwnerCompile,
  DEFAULT_AGGREGATE_PATH,
  DEFAULT_ROOT_PROPS_PATH,
  DEFAULT_SCRATCH_ROOT,
} from './lib/owner-compile.mjs'

const OWNER_PROJECT_BASENAME_PATTERN = /^Wanxiangshu\.Owner\..+\.fsproj$/

function printHelp() {
  console.log(`
Usage: node scripts/compile-owner.mjs <project.fsproj> [options]

Arguments:
  <project.fsproj>       Path to candidate owner fsproj file (required; basename must match Wanxiangshu.Owner.*.fsproj)

Options:
  --aggregate <path>     Path to aggregate Wanxiangshu.fsproj (default: src/Wanxiangshu/Wanxiangshu.fsproj)
  --scratch <path>       Scratch build root (default: .fable-build/owner-compile)
  --props <path>         Root Directory.Build.props path (default: Directory.Build.props)
  --output, -o <path>    Output directory for compiled artifacts
  --plan-only            Only compute and print compilation plan as JSON
  --materialize-only     Only materialize scratch project and print metadata as JSON
  --watch                Keep one Fable process watching the materialized flat project
  --help, -h             Show this help message
`)
}

function parseArgs(args) {
  let projectPath = null
  let aggregatePath = DEFAULT_AGGREGATE_PATH
  let scratchRoot = DEFAULT_SCRATCH_ROOT
  let rootPropsPath = DEFAULT_ROOT_PROPS_PATH
  let outputDir = null
  let planOnly = false
  let materializeOnly = false
  let watch = false

  for (let i = 0; i < args.length; i++) {
    const arg = args[i]
    if (arg === '--help' || arg === '-h') {
      printHelp()
      process.exit(0)
    } else if (arg === '--plan-only') {
      planOnly = true
    } else if (arg === '--materialize-only') {
      materializeOnly = true
    } else if (arg === '--watch') {
      watch = true
    } else if (arg === '--aggregate' || arg.startsWith('--aggregate=')) {
      let val
      if (arg.startsWith('--aggregate=')) {
        val = arg.slice('--aggregate='.length)
      } else {
        val = args[++i]
      }
      if (!val || val.startsWith('-')) {
        console.error(`Error: option '--aggregate' requires a value`)
        printHelp()
        process.exit(1)
      }
      aggregatePath = resolve(val)
    } else if (arg === '--scratch' || arg.startsWith('--scratch=')) {
      let val
      if (arg.startsWith('--scratch=')) {
        val = arg.slice('--scratch='.length)
      } else {
        val = args[++i]
      }
      if (!val || val.startsWith('-')) {
        console.error(`Error: option '--scratch' requires a value`)
        printHelp()
        process.exit(1)
      }
      scratchRoot = resolve(val)
    } else if (arg === '--props' || arg.startsWith('--props=')) {
      let val
      if (arg.startsWith('--props=')) {
        val = arg.slice('--props='.length)
      } else {
        val = args[++i]
      }
      if (!val || val.startsWith('-')) {
        console.error(`Error: option '--props' requires a value`)
        printHelp()
        process.exit(1)
      }
      rootPropsPath = resolve(val)
    } else if (arg === '--output' || arg === '-o' || arg.startsWith('--output=') || arg.startsWith('-o=')) {
      let val
      if (arg.startsWith('--output=')) {
        val = arg.slice('--output='.length)
      } else if (arg.startsWith('-o=')) {
        val = arg.slice('-o='.length)
      } else {
        val = args[++i]
      }
      if (!val || val.startsWith('-')) {
        console.error(`Error: option '${arg.split('=')[0]}' requires a value`)
        printHelp()
        process.exit(1)
      }
      outputDir = resolve(val)
    } else if (arg.startsWith('-')) {
      console.error(`Unknown option: ${arg}`)
      printHelp()
      process.exit(1)
    } else {
      if (projectPath !== null) {
        console.error(`Unexpected extra argument: ${arg}`)
        printHelp()
        process.exit(1)
      }
      projectPath = resolve(arg)
    }
  }

  if (!projectPath) {
    console.error('Error: target owner project .fsproj argument is required')
    printHelp()
    process.exit(1)
  }

  const candidateBasename = basename(projectPath)
  if (!OWNER_PROJECT_BASENAME_PATTERN.test(candidateBasename)) {
    console.error(`Error: candidate project basename must match "Wanxiangshu.Owner.*.fsproj", got: ${candidateBasename}`)
    process.exit(1)
  }

  return {
    projectPath,
    aggregatePath,
    scratchRoot,
    rootPropsPath,
    outputDir,
    planOnly,
    materializeOnly,
    watch,
  }
}

async function main() {
  const options = parseArgs(process.argv.slice(2))

  if (options.planOnly) {
    try {
      const plan = planOwnerCompile({
        projectPath: options.projectPath,
        aggregatePath: options.aggregatePath,
      })
      console.log(JSON.stringify({
        candidatePath: plan.candidatePath,
        candidateBasename: plan.candidateBasename,
        aggregatePath: plan.aggregatePath,
        projectPaths: plan.projectPaths,
        compileItems: plan.compileItems,
      }, null, 2))
      process.exit(0)
    } catch (err) {
      console.error(`[owner-compile:plan] FAILED: ${err.message}`)
      process.exit(1)
    }
  }

  if (options.materializeOnly) {
    try {
      const plan = planOwnerCompile({
        projectPath: options.projectPath,
        aggregatePath: options.aggregatePath,
      })
      const materialized = materializeOwnerCompile(plan, {
        scratchRoot: options.scratchRoot,
        rootPropsPath: options.rootPropsPath,
        outputDir: options.outputDir,
      })
      console.log(JSON.stringify(materialized, null, 2))
      process.exit(0)
    } catch (err) {
      console.error(`[owner-compile:materialize] FAILED: ${err.message}`)
      process.exit(1)
    }
  }

  try {
    const result = await compileOwnerProject({
      projectPath: options.projectPath,
      aggregatePath: options.aggregatePath,
      scratchRoot: options.scratchRoot,
      rootPropsPath: options.rootPropsPath,
      outputDir: options.outputDir,
      watch: options.watch,
    })

    if (!result.ok) {
      process.exit(result.code || 1)
    }
  } catch (err) {
    console.error(`[owner-compile] FAILED: ${err.message}`)
    process.exit(1)
  }
}

await main()
