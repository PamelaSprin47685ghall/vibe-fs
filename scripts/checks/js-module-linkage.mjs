#!/usr/bin/env node

import { existsSync, readFileSync } from 'node:fs'
import { isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath, pathToFileURL } from 'node:url'

import { parseModule, patternNames, walkSyntax } from '../lib/js-syntax.mjs'
import { walk } from '../lib/walk.mjs'

const normalize = (value) => value.replace(/\\/g, '/')

const declarationExports = (declaration) => {
  if (!declaration) return []
  if (declaration.id?.type === 'Identifier') return [declaration.id.name]
  if (Array.isArray(declaration.declarations)) {
    return declaration.declarations.flatMap((entry) => patternNames(entry.id))
  }
  return []
}

const moduleExports = (program) => {
  const names = new Set()
  for (const node of program.body) {
    if (node.type === 'ExportDefaultDeclaration') {
      names.add('default')
      continue
    }
    if (node.type === 'ExportAllDeclaration' && node.exported) {
      names.add(node.exported.name ?? node.exported.value)
    }
    if (node.type !== 'ExportNamedDeclaration') continue
    for (const name of declarationExports(node.declaration)) names.add(name)
    for (const specifier of node.specifiers ?? []) names.add(specifier.exported.name ?? specifier.exported.value)
  }
  return names
}

const relativeTarget = (importer, specifier) => {
  return fileURLToPath(new URL(specifier, pathToFileURL(importer)))
}

const moduleEdges = (program) => {
  const edges = []
  walkSyntax(program, (node) => {
    if (!['ImportDeclaration', 'ImportExpression', 'ExportNamedDeclaration', 'ExportAllDeclaration'].includes(node.type)) return
    if (typeof node.source?.value !== 'string') return
    edges.push({
      specifier: node.source.value,
      names: (node.specifiers ?? []).flatMap((entry) => {
        if (entry.type === 'ImportNamespaceSpecifier') return []
        if (entry.type === 'ImportDefaultSpecifier') return ['default']
        const name = entry.imported ?? entry.local
        return [name.name ?? name.value]
      }),
    })
  })
  return edges
}

const inside = (root, target) => {
  const path = relative(root, target)
  return path !== '..' && !normalize(path).startsWith('../') && !isAbsolute(path)
}

export function validateModuleLinkage(distRoot, files = walk(resolve(distRoot), ['.js'])) {
  const root = resolve(distRoot)
  const programs = new Map()
  const exportsByFile = new Map()
  const violations = []

  for (const file of files) {
    let program
    try {
      program = parseModule(readFileSync(file, 'utf8'))
    } catch (error) {
      violations.push(`${normalize(relative(root, file))}: invalid emitted ESM: ${error.message}`)
      continue
    }
    programs.set(file, program)
    exportsByFile.set(file, moduleExports(program))
  }

  // Re-export cycles converge because each step only adds known export names.
  let changed = true
  while (changed) {
    changed = false
    for (const [file, program] of programs) {
      const names = exportsByFile.get(file)
      for (const node of program.body) {
        if (node.type !== 'ExportAllDeclaration' || node.exported || !node.source.value.startsWith('.')) continue
        const target = relativeTarget(file, node.source.value)
        for (const name of exportsByFile.get(target) ?? []) {
          if (name === 'default' || names.has(name)) continue
          names.add(name)
          changed = true
        }
      }
    }
  }

  for (const [file, program] of programs) {
    const importer = normalize(relative(root, file))
    for (const { specifier, names } of moduleEdges(program)) {
      if (!specifier.startsWith('.')) continue

      const target = relativeTarget(file, specifier)
      if (!inside(root, target)) {
        violations.push(`${importer}: relative import '${specifier}' escapes dist package closure`)
        continue
      }
      if (!existsSync(target) || !exportsByFile.has(target)) {
        violations.push(`${importer}: relative import '${specifier}' resolves to missing emitted module`)
        continue
      }

      const available = exportsByFile.get(target)
      for (const name of names) {
        if (!available.has(name)) {
          violations.push(
            `${importer}: ${normalize(relative(root, target))} is missing named export '${name}'`,
          )
        }
      }
    }
  }

  return violations.sort()
}

export async function validateModuleLoadability(distRoot, files = walk(resolve(distRoot), ['.js'])) {
  const root = resolve(distRoot)
  const productionModules = files.filter((file) => !normalize(file).includes('fable_modules'))
  const failures = []

  for (const file of productionModules) {
    try {
      const fileUrl = pathToFileURL(file).href
      await import(fileUrl)
    } catch (error) {
      const importer = normalize(relative(root, file))
      const firstLine = (error?.message || String(error)).split('\n')[0]
      failures.push(`${importer}: failed to load emitted module: ${firstLine}`)
    }
  }

  return failures.sort()
}
export async function check(context) {
  const root = context?.root ?? process.cwd()
  const distRoot = resolve(root, 'dist')
  const linkageViolations = validateModuleLinkage(distRoot)
  const loadabilityViolations = await validateModuleLoadability(distRoot)
  const violations = [...linkageViolations, ...loadabilityViolations].sort()
  return {
    issues: violations.map((v) => ({
      code: 'js-module-linkage',
      message: v,
    })),
  }
}

export async function runCli(argv = process.argv.slice(2)) {
  const root = process.cwd()
  const distRoot = resolve(root, 'dist')
  const files = walk(distRoot, ['.js'])
  const linkageViolations = validateModuleLinkage(distRoot, files)
  const loadabilityViolations = await validateModuleLoadability(distRoot, files)
  const violations = [...linkageViolations, ...loadabilityViolations].sort()

  if (violations.length > 0) {
    violations.sort()
    console.error(`js-module-linkage: FAILED — ${violations.length} violation(s)`)
    for (const violation of violations) console.error(`  ${violation}`)
    return 1
  }
  console.log(`js-module-linkage: OK — ${files.length} emitted modules linked and loaded`)
  return 0
}

export async function run({ root = process.cwd() } = {}) {
  const distRoot = resolve(root, 'dist')
  const files = walk(distRoot, ['.js'])
  const linkageViolations = validateModuleLinkage(distRoot, files)
  const loadabilityViolations = await validateModuleLoadability(distRoot, files)
  const violations = [...linkageViolations, ...loadabilityViolations].sort()

  if (violations.length > 0) {
    violations.sort()
    console.error(`js-module-linkage: FAILED — ${violations.length} violation(s)`)
    for (const violation of violations) console.error(`  ${violation}`)
    return 1
  }
  console.log(`js-module-linkage: OK — ${files.length} emitted modules linked and loaded`)
  return 0
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  runCli().then((code) => { process.exitCode = code })
}
