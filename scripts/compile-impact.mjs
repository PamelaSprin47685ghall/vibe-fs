#!/usr/bin/env node

import { dirname, resolve } from 'node:path'
import {
  compileIncremental,
  compileOwnerProject,
  detectChangedFiles,
  materializeOwnerCompile,
  planImpactCompile,
  DEFAULT_AGGREGATE_PATH,
  DEFAULT_ROOT_PROPS_PATH,
} from './lib/owner-compile.mjs'

const DEFAULT_SCRATCH_ROOT = resolve('.fable-build/impact-compile')

function printHelp() {
  console.log(`
Usage: node scripts/compile-impact.mjs [changed-path]... [options]

Arguments:
  [changed-path]...       Changed source, signature, project, or toolchain paths (optional; auto-detected when omitted)

Options:
  --aggregate <path>      Aggregate Wanxiangshu.fsproj
  --projects <path>       Directory containing owner fsproj files
  --scratch <path>        Scratch build root
  --props <path>          Root Directory.Build.props path
  --output, -o <path>     Output directory for compiled artifacts
  --threshold <ratio>     Focused/full production-source cutoff (default: 0.6)
  --plan-only             Print the impact plan without materializing or compiling
  --materialize-only      Materialize the flat project without compiling
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

  if (planOnly && materializeOnly) {
    throw new Error('--plan-only and --materialize-only are mutually exclusive')
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
    let effectivePaths = options.changedPaths
    if (effectivePaths.length === 0) {
      const detection = detectChangedFiles({
        aggregatePath: options.aggregatePath,
        outputDir: options.outputDir,
      })
      effectivePaths = detection.changedPaths
    }

    if (effectivePaths.length === 0 && (options.planOnly || options.materializeOnly)) {
      if (options.planOnly) {
        console.log(JSON.stringify({
          mode: 'none',
          reason: 'no-changes-detected',
          changedPaths: [],
          rootProjectPaths: [],
          projectPaths: [],
          compileItems: [],
        }, null, 2))
        return
      }
      console.log('[impact-compile] no production compile impact')
      return
    }

    const plan = planImpactCompile({
      ...options,
      changedPaths: effectivePaths.length > 0 ? effectivePaths : [options.aggregatePath],
    })

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

    const result = await compileIncremental({
      changedPaths: options.changedPaths.length > 0 ? options.changedPaths : undefined,
      aggregatePath: options.aggregatePath,
      scratchRoot: options.scratchRoot,
      rootPropsPath: options.rootPropsPath,
      outputDir: options.outputDir,
      manifestPath: resolve(options.scratchRoot, 'impact-manifest.json'),
      fullThreshold: options.fullThreshold,
    })
    if (!result.ok) {
      process.exit(result.code || 1)
    }
    if (result.cached) {
      console.log('[impact-compile] up-to-date (cached)')
    } else {
      console.log(`[impact-compile] compiled ${result.mode} impact (${result.compileItems.length} items)`)
    }
  } catch (error) {
    console.error(`[impact-compile] FAILED: ${error.message}`)
    process.exit(1)
  }
}

await main()
