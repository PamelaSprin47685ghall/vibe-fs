#!/usr/bin/env node

import { existsSync, readFileSync } from 'node:fs'
import { dirname, extname, isAbsolute, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

import { parseModule } from '../lib/js-syntax.mjs'
import { walk } from '../lib/walk.mjs'

const normalize = (value) => value.replace(/\\/g, '/')

const declarationExports = (declaration) => {
  if (!declaration) return []
  if (declaration.id?.type === 'Identifier') return [declaration.id.name]
  if (Array.isArray(declaration.declarations)) {
    return declaration.declarations.flatMap((entry) => entry.id?.type === 'Identifier' ? [entry.id.name] : [])
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
    if (node.type !== 'ExportNamedDeclaration') continue
    for (const name of declarationExports(node.declaration)) names.add(name)
    for (const specifier of node.specifiers ?? []) names.add(specifier.exported.name ?? specifier.exported.value)
  }
  return names
}

const relativeTarget = (importer, specifier) => {
  const target = resolve(dirname(importer), specifier)
  return extname(target) ? target : `${target}.js`
}

const inside = (root, target) => {
  const path = relative(root, target)
  return path === '' || (!path.startsWith('..') && !isAbsolute(path))
}

export function validateModuleLinkage(distRoot) {
  const root = resolve(distRoot)
  const files = walk(root, ['.js'])
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

  for (const [file, program] of programs) {
    const importer = normalize(relative(root, file))
    for (const node of program.body) {
      if (node.type !== 'ImportDeclaration' || typeof node.source.value !== 'string') continue
      const specifier = node.source.value
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
      for (const imported of node.specifiers) {
        if (imported.type === 'ImportNamespaceSpecifier') continue
        const name = imported.type === 'ImportDefaultSpecifier'
          ? 'default'
          : imported.imported.name ?? imported.imported.value
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

export function run({ root = process.cwd() } = {}) {
  const distRoot = resolve(root, 'dist')
  const violations = validateModuleLinkage(distRoot)
  if (violations.length > 0) {
    console.error(`js-module-linkage: FAILED — ${violations.length} violation(s)`)
    for (const violation of violations) console.error(`  ${violation}`)
    return 1
  }
  console.log(`js-module-linkage: OK — ${walk(distRoot, ['.js']).length} emitted modules linked`)
  return 0
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  process.exitCode = run()
}
