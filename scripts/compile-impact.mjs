#!/usr/bin/env node

import { dirname, resolve } from 'node:path'
import {
  compileOwnerProject,
  materializeOwnerCompile,
  planImpactCompile,
  DEFAULT_AGGREGATE_PATH,
  DEFAULT_ROOT_PROPS_PATH,
} from './lib/owner-compile.mjs'

const DEFAULT_SCRATCH_ROOT = resolve('.fable-build/impact-compile')

function printHelp() {
  console.log(`
Usage: node scripts/compile-impact.mjs <changed-path>... [options]

Arguments:
  <changed-path>...       Changed source, signature, project, or toolchain paths

Options:
  --aggregate <path>      Aggregate Wanxiangshu.fsproj
  --projects <path>       Directory containing owner fsproj files
  --scratch <path>        Scratch build root
  --props <path>          Root Directory.Build.props path
  --output, -o <path>     Output directory for compiled artifacts
  --threshold <ratio>     Focused/full production-source cutoff (default: 0.6)
  --plan-only             Print the impact plan without materializing or compiling
  --materialize-only      Materialize the flat project without compiling
  --watch                 Keep one Fable process watching the flat impact project
  --help, -h              Show this help message
`)
}

function readOption(args, index, name) {
  const arg = args[index]
  const inline = arg.startsWith(`${name}=`) ? arg.slice(name.length + 1) : null
  const value = inline ?? args[index + 1]
  if (!value || value.startsWith('-')) {
    throw new Error(`option '${name}' requires a value`)
  }
  return { value, consumed: inline === null ? 1 : 0 }
}

function parseArgs(args) {
  let aggregatePath = DEFAULT_AGGREGATE_PATH
  let projectDirectory = null
  let scratchRoot = DEFAULT_SCRATCH_ROOT
  let rootPropsPath = DEFAULT_ROOT_PROPS_PATH
  let outputDir = null
  let fullThreshold = 0.6
  let planOnly = false
  let materializeOnly = false
  let watch = false
  const changedPaths = []

  for (let index = 0; index < args.length; index += 1) {
    const arg = args[index]
    if (arg === '--help' || arg === '-h') {
      printHelp()
      process.exit(0)
    }
    if (arg === '--plan-only') {
      planOnly = true
      continue
    }
    if (arg === '--materialize-only') {
      materializeOnly = true
      continue
    }
    if (arg === '--watch') {
      watch = true
      continue
    }

    const outputAlias = arg === '-o' || arg.startsWith('-o=')
    const option = outputAlias ? '--output' : arg.split('=')[0]
    if (['--aggregate', '--projects', '--scratch', '--props', '--output', '--threshold'].includes(option)) {
      const { value, consumed } = readOption(args, index, outputAlias ? '-o' : option)
      index += consumed
      if (option === '--aggregate') aggregatePath = resolve(value)
      else if (option === '--projects') projectDirectory = resolve(value)
      else if (option === '--scratch') scratchRoot = resolve(value)
      else if (option === '--props') rootPropsPath = resolve(value)
      else if (option === '--output') outputDir = resolve(value)
      else fullThreshold = Number(value)
      continue
    }

    if (arg.startsWith('-')) {
      throw new Error(`unknown option '${arg}'`)
    }
    changedPaths.push(resolve(arg))
  }

  if (changedPaths.length === 0) {
    throw new Error('at least one changed path is required')
  }
  if (planOnly && materializeOnly) {
    throw new Error('--plan-only and --materialize-only are mutually exclusive')
  }
  if (watch && (planOnly || materializeOnly)) {
    throw new Error('--watch cannot be combined with --plan-only or --materialize-only')
  }

  return {
    aggregatePath,
    projectDirectory: projectDirectory ?? dirname(aggregatePath),
    scratchRoot,
    rootPropsPath,
    outputDir,
    fullThreshold,
    planOnly,
    materializeOnly,
    watch,
    changedPaths,
  }
}

async function main() {
  let options
  try {
    options = parseArgs(process.argv.slice(2))
  } catch (error) {
    console.error(`[impact-compile] ${error.message}`)
    printHelp()
    process.exit(1)
  }

  try {
    const plan = planImpactCompile(options)
    if (options.planOnly) {
      console.log(JSON.stringify({
        mode: plan.mode,
        reason: plan.reason,
        changedPaths: plan.changedPaths,
        rootProjectPaths: plan.rootProjectPaths,
        projectPaths: plan.projectPaths,
        compileItems: plan.compileItems,
      }, null, 2))
      return
    }

    if (plan.mode === 'none') {
      console.log('[impact-compile] no production compile impact')
      return
    }

    if (options.materializeOnly) {
      console.log(JSON.stringify(materializeOwnerCompile(plan, {
        scratchRoot: options.scratchRoot,
        rootPropsPath: options.rootPropsPath,
        outputDir: options.outputDir,
      }), null, 2))
      return
    }

    const result = await compileOwnerProject({
      compilePlan: plan,
      scratchRoot: options.scratchRoot,
      rootPropsPath: options.rootPropsPath,
      outputDir: options.outputDir,
      watch: options.watch,
    })
    if (!result.ok) {
      process.exit(result.code || 1)
    }
  } catch (error) {
    console.error(`[impact-compile] FAILED: ${error.message}`)
    process.exit(1)
  }
}

await main()
