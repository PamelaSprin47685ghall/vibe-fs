#!/usr/bin/env node
// JS-SEMANTIC-SURFACE-003/005 manifest gate.
//
// Registration grants no authority by itself. Every registered module must be
// owned by a current requirement, governed by current WHAT laws with PROOF
// evidence, implemented by a compiled source file, and imported by a real
// executable contract test.

import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { pathToFileURL } from 'node:url'

import { SURFACE_CONSUMERS, SURFACE_MANIFEST } from '../lib/test-surface-scan.mjs'
import { isFunction, parseModule, walkSyntax } from '../lib/js-syntax.mjs'
import { scanTestSource, whatHeadings } from '../lib/requirement-trace.mjs'
import { walk } from '../lib/walk.mjs'

export const WHAT_ID = /^#{1,6}\s+([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9]+)?)\b/gm

const normalize = (path) => path.replace(/\\/g, '/')
const escapeRegExp = (text) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
const read = (root, path) => readFileSync(join(root, path), 'utf8')

/** Extract the requirements package slug from a test file path. */
const packageOfTestFile = (file, requirementsRoot) => {
  const rel = normalize(file).replace(normalize(requirementsRoot) + '/', '')
  const segments = rel.split('/')
  return segments.length > 1 && segments[1] === 'tests' ? segments[0] : null
}

/** Render a path relative to root for error messages. */
const relativePath = (file, root) => normalize(file).replace(normalize(root) + '/', '')

const WHAT_TAG = /WHAT\[([A-Z][A-Z0-9-]*-\d{3}(?:[A-Z]|-[A-Z0-9]+)?)\]/g

export const whatIds = (text) => {
  WHAT_ID.lastIndex = 0
  WHAT_TAG.lastIndex = 0
  return [
    ...new Set([
      ...[...text.matchAll(WHAT_ID)].map((match) => match[1]),
      ...[...text.matchAll(WHAT_TAG)].map((match) => match[1]),
    ]),
  ]
}

/** A PROOF row is executable evidence, not a prose mention in WHY/HOW. */
export const proofHasLaw = (text, law) =>
  text.split('\n').some((line) => line.includes('|') && new RegExp(`\\b${escapeRegExp(law)}\\b`).test(line))

/** Require a direct static/dynamic import in a .test.mjs source, not a comment. */
export const importsSurface = (source, module) => {
  return analyzeSurface(source, module).imports.length > 0
}

/**
 * A contract import is evidence only when its binding is used after the import.
 * Merely importing an emitted module from a dead helper does not prove an
 * executable semantic contract.
 *
 * Lexical binding-use check: strips comments/strings, then proves the imported
 * binding appears as an identifier in executable code after the import clause.
 * Recognizes default, namespace, and named import forms (including `as`).
 */
export const usesSurface = (source, module) => {
  return analyzeSurface(source, module).uses.length > 0
}

const moduleSpecifierMatches = (value, module) =>
  typeof value === 'string' && (value === `dist/${module}` || value.endsWith(`/dist/${module}`))

const patternIdentifiers = (pattern) => {
  if (!pattern) return []
  switch (pattern.type) {
    case 'Identifier': return [pattern]
    case 'AssignmentPattern': return patternIdentifiers(pattern.left)
    case 'RestElement': return patternIdentifiers(pattern.argument)
    case 'ArrayPattern': return pattern.elements.flatMap(patternIdentifiers)
    case 'ObjectPattern': return pattern.properties.flatMap((property) =>
      property.type === 'RestElement' ? patternIdentifiers(property.argument) : patternIdentifiers(property.value))
    default: return []
  }
}

const isBlockScope = (node) =>
  node?.type === 'Program'
  || node?.type === 'BlockStatement'
  || node?.type === 'CatchClause'
  || node?.type === 'ForStatement'
  || node?.type === 'ForInStatement'
  || node?.type === 'ForOfStatement'
  || node?.type === 'SwitchStatement'
  || node?.type === 'StaticBlock'

const nearestScope = (ancestors, predicate) => {
  for (let index = ancestors.length - 1; index >= 0; index--) {
    if (predicate(ancestors[index])) return { node: ancestors[index], depth: index }
  }
  return null
}

const strictObjectTargets = (pattern) => {
  if (pattern.type !== 'ObjectPattern') return null
  const targets = []
  for (const property of pattern.properties) {
    if (property.type !== 'Property' || property.computed || property.kind !== 'init' || property.method || property.value.type !== 'Identifier') return null
    targets.push(property.value)
  }
  return targets
}

const contains = (outer, inner) => outer && outer.start <= inner.start && inner.end <= outer.end
const unwrapAwait = (node) => node?.type === 'AwaitExpression' ? node.argument : node

const referenceIdentifier = (node, parent) => {
  if (node.type !== 'Identifier' || parent === null) return false
  if (parent.type === 'MemberExpression' && parent.property === node && !parent.computed) return false
  if (parent.type === 'Property' && parent.key === node && !parent.computed && !parent.shorthand) return false
  if ((parent.type === 'MethodDefinition' || parent.type === 'PropertyDefinition') && parent.key === node && !parent.computed) return false
  if (parent.type === 'LabeledStatement' || parent.type === 'BreakStatement' || parent.type === 'ContinueStatement') return false
  if (parent.type === 'MetaProperty' || parent.type === 'ImportSpecifier' || parent.type === 'ImportDefaultSpecifier') return false
  if (parent.type === 'ImportNamespaceSpecifier' || parent.type === 'ExportSpecifier') return false
  return true
}

const assignmentTarget = (node, ancestors) => ancestors.some((ancestor) =>
  (ancestor.type === 'AssignmentExpression' && contains(ancestor.left, node))
  || (ancestor.type === 'UpdateExpression' && contains(ancestor.argument, node))
  || ((ancestor.type === 'ForInStatement' || ancestor.type === 'ForOfStatement') && contains(ancestor.left, node))
  || (ancestor.type === 'UnaryExpression' && ancestor.operator === 'delete' && contains(ancestor.argument, node)))

const terminalCall = (node, ancestors) => {
  for (let index = ancestors.length - 1; index >= 0; index--) {
    const ancestor = ancestors[index]
    if (ancestor.type !== 'CallExpression' || !contains(ancestor, node)) continue
    const callee = ancestor.callee.type === 'ChainExpression' ? ancestor.callee.expression : ancestor.callee
    return !(contains(callee, node) && callee.type === 'MemberExpression' && !callee.computed && callee.property.type === 'Identifier' && callee.property.name === 'bind')
  }
  return false
}

const inNonterminalInitializer = (node, ancestors) => ancestors.some((ancestor) =>
  ancestor.type === 'VariableDeclarator' && contains(ancestor.init, node) && !terminalCall(node, ancestors))

const voidRead = (node, ancestors) => ancestors.some((ancestor) =>
  ancestor.type === 'UnaryExpression' && ancestor.operator === 'void' && contains(ancestor.argument, node) && !terminalCall(node, ancestors))

const literalBoolean = (node) => node?.type === 'Literal' && typeof node.value === 'boolean' ? node.value : null

const staticallyUnreachable = (node, ancestors) => ancestors.some((ancestor) => {
  if (ancestor.type === 'IfStatement') {
    const condition = literalBoolean(ancestor.test)
    return (condition === false && contains(ancestor.consequent, node)) || (condition === true && contains(ancestor.alternate, node))
  }
  if (ancestor.type === 'ConditionalExpression') {
    const condition = literalBoolean(ancestor.test)
    return (condition === false && contains(ancestor.consequent, node)) || (condition === true && contains(ancestor.alternate, node))
  }
  if (ancestor.type === 'LogicalExpression' && contains(ancestor.right, node)) {
    const left = literalBoolean(ancestor.left)
    return (ancestor.operator === '&&' && left === false) || (ancestor.operator === '||' && left === true)
  }
  if (ancestor.type === 'WhileStatement') return literalBoolean(ancestor.test) === false && contains(ancestor.body, node)
  if (ancestor.type === 'ForStatement') return literalBoolean(ancestor.test) === false && contains(ancestor.body, node)
  return false
})

const analysisCache = new WeakMap()

export const analyzeSurface = (source, module, syntax) => {
  if (!source.includes(`dist/${module}`)) return { imports: [], uses: [] }
  const program = syntax ?? parseModule(source)
  let byModule = analysisCache.get(program)
  if (!byModule) {
    byModule = new Map()
    analysisCache.set(program, byModule)
  }
  if (byModule.has(module)) return byModule.get(module)

  const bindings = []
  const bindingIdentifiers = new WeakSet()
  const bindingByIdentifier = new WeakMap()
  const ancestorsByNode = new WeakMap()
  const declarators = []
  const functions = []
  const imports = []
  const addBinding = (identifier, scope, declarationEnd, surface = false) => {
    if (!identifier || !scope || bindingByIdentifier.has(identifier)) return bindingByIdentifier.get(identifier)
    const binding = {
      name: identifier.name,
      scope: scope.node,
      depth: scope.depth,
      declarationEnd,
      position: identifier.start,
      surface,
      callable: null,
      fastCheck: false,
      propertyInitializer: null,
    }
    bindings.push(binding)
    bindingIdentifiers.add(identifier)
    bindingByIdentifier.set(identifier, binding)
    return binding
  }
  const outerBlock = (ancestors) => nearestScope(ancestors, isBlockScope)
  const functionOrProgram = (ancestors) => nearestScope(ancestors, (node) => isFunction(node) || node.type === 'Program')

  walkSyntax(program, (node, parent, _key, ancestors) => {
    ancestorsByNode.set(node, ancestors)
    if (node.type === 'ImportDeclaration') {
      const surface = moduleSpecifierMatches(node.source.value, module)
      if (surface) imports.push(node)
      const scope = { node: program, depth: 0 }
      for (const specifier of node.specifiers) {
        const binding = addBinding(specifier.local, scope, node.end, surface)
        binding.fastCheck = node.source.value === 'fast-check'
          && (specifier.type === 'ImportDefaultSpecifier' || specifier.type === 'ImportNamespaceSpecifier')
      }
      return
    }
    if (node.type === 'ImportExpression' && moduleSpecifierMatches(node.source?.value, module)) imports.push(node)
    if (node.type === 'VariableDeclarator') {
      const declaration = parent
      const scope = declaration.kind === 'var' ? functionOrProgram(ancestors) : outerBlock(ancestors)
      for (const identifier of patternIdentifiers(node.id)) addBinding(identifier, scope, node.end)
      declarators.push({ node, declaration })
      return
    }
    if (node.type === 'FunctionDeclaration') {
      const binding = addBinding(node.id, outerBlock(ancestors), node.end)
      if (binding) binding.callable = node
      const scope = { node, depth: ancestors.length }
      for (const identifier of node.params.flatMap(patternIdentifiers)) addBinding(identifier, scope, node.end)
      functions.push(node)
      return
    }
    if (node.type === 'FunctionExpression' || node.type === 'ArrowFunctionExpression') {
      const scope = { node, depth: ancestors.length }
      if (node.type === 'FunctionExpression') {
        const binding = addBinding(node.id, scope, node.end)
        if (binding) binding.callable = node
      }
      for (const identifier of node.params.flatMap(patternIdentifiers)) addBinding(identifier, scope, node.end)
      functions.push(node)
      return
    }
    if (node.type === 'ClassDeclaration') addBinding(node.id, outerBlock(ancestors), node.end)
    else if (node.type === 'ClassExpression') addBinding(node.id, { node, depth: ancestors.length }, node.end)
    else if (node.type === 'CatchClause') {
      const scope = { node, depth: ancestors.length }
      for (const identifier of patternIdentifiers(node.param)) addBinding(identifier, scope, node.end)
    }
  })

  const resolveBinding = (identifier) => {
    const ancestors = ancestorsByNode.get(identifier) ?? []
    const scopes = new Set(ancestors)
    return bindings
      .filter((binding) => binding.name === identifier.name && scopes.has(binding.scope))
      .sort((left, right) => right.depth - left.depth)[0] ?? null
  }
  const targetsOf = (pattern) => {
    const identifiers = pattern.type === 'Identifier' ? [pattern] : strictObjectTargets(pattern)
    if (identifiers === null) return null
    const targets = identifiers.map((identifier) => bindingByIdentifier.get(identifier)).filter(Boolean)
    return targets.length === identifiers.length ? targets : null
  }

  for (const { node } of declarators) {
    const targets = targetsOf(node.id) ?? []
    if (isFunction(node.init)) {
      for (const target of targets) target.callable = node.init
    }
  }

  for (const { node, declaration } of declarators) {
    if (declaration.kind !== 'const') continue
    const imported = unwrapAwait(node.init)
    if (imported?.type !== 'ImportExpression' || !moduleSpecifierMatches(imported.source?.value, module)) continue
    for (const target of targetsOf(node.id) ?? []) target.surface = true
  }

  const aliasSources = new WeakSet()
  let changed = true
  while (changed) {
    changed = false
    for (const { node, declaration } of declarators) {
      if (declaration.kind !== 'const' || !node.init) continue
      const targets = targetsOf(node.id)
      if (targets === null) continue
      let identifier = null
      if (node.init.type === 'Identifier') identifier = node.init
      else if (node.init.type === 'MemberExpression' && !node.init.computed && !node.init.optional && node.init.object.type === 'Identifier' && node.init.property.type === 'Identifier') identifier = node.init.object
      if (identifier === null) continue
      const sourceBinding = resolveBinding(identifier)
      if (!sourceBinding?.surface || sourceBinding.declarationEnd > node.init.start) continue
      aliasSources.add(identifier)
      for (const target of targets) {
        if (target.surface) continue
        target.surface = true
        changed = true
      }
    }
  }

  const uses = []
  walkSyntax(program, (node, parent, _key, ancestors) => {
    if (!referenceIdentifier(node, parent) || bindingIdentifiers.has(node) || aliasSources.has(node)) return
    const binding = resolveBinding(node)
    if (!binding?.surface || assignmentTarget(node, ancestors) || inNonterminalInitializer(node, ancestors) || voidRead(node, ancestors)) return
    if (staticallyUnreachable(node, ancestors)) return
    const enclosingFunctions = ancestors.filter(isFunction)
    uses.push({
      position: node.start,
      functions: enclosingFunctions,
      owner: enclosingFunctions.at(-1) ?? null,
    })
  })

  const callsByFunction = new Map(functions.map((fn) => [fn, []]))
  walkSyntax(program, (node, _parent, _key, ancestors) => {
    if (node.type !== 'CallExpression' || staticallyUnreachable(node, ancestors)) return
    const owner = ancestors.filter(isFunction).at(-1)
    if (owner) callsByFunction.get(owner)?.push({ node, ancestors })
  })
  for (const calls of callsByFunction.values()) calls.sort((left, right) => left.node.start - right.node.start)

  const functionParameters = new Map(functions.map((fn) => [
    fn,
    fn.params.map((parameter) =>
      patternIdentifiers(parameter).map((identifier) => bindingByIdentifier.get(identifier)).filter(Boolean)),
  ]))
  const functionByBodyStart = new Map(functions.map((fn) => [fn.body.start, fn]))
  const unwrapChain = (node) => node?.type === 'ChainExpression' ? node.expression : node
  const fastCheckMember = (call, names) => {
    const callee = unwrapChain(call?.callee)
    if (callee?.type !== 'MemberExpression' || callee.computed || callee.object.type !== 'Identifier') return null
    if (callee.property.type !== 'Identifier' || !names.has(callee.property.name)) return null
    return resolveBinding(callee.object)?.fastCheck ? callee.property.name : null
  }
  const propertyConstructors = new Set(['property', 'asyncProperty'])
  const propertyRunners = new Set(['assert', 'check'])
  for (const { node, declaration } of declarators) {
    if (declaration.kind !== 'const' || !fastCheckMember(node.init, propertyConstructors)) continue
    for (const target of targetsOf(node.id) ?? []) target.propertyInitializer = node.init
  }
  const returnedOrAwaited = (call, ancestors, owner) => {
    if (owner?.body?.type !== 'BlockStatement' && contains(owner?.body, call)) return true
    return ancestors.some((ancestor) =>
      (ancestor.type === 'AwaitExpression' && contains(ancestor.argument, call))
      || (ancestor.type === 'ReturnStatement' && contains(ancestor.argument, call)))
  }
  const emptyValue = () => ({ callables: [], properties: [] })
  const mergeValue = (left, right) => ({
    callables: [...left.callables, ...right.callables],
    properties: [...left.properties, ...right.properties],
  })
  const valueOf = (expression, environment, resolving = new Set()) => {
    const node = unwrapChain(expression)
    if (!node) return emptyValue()
    if (isFunction(node)) return { callables: [{ fn: node, environment: new Map(environment) }], properties: [] }
    if (node.type === 'Identifier') {
      const binding = resolveBinding(node)
      if (!binding) return emptyValue()
      if (environment.has(binding)) return environment.get(binding)
      if (binding.callable) {
        return { callables: [{ fn: binding.callable, environment: new Map(environment) }], properties: [] }
      }
      if (binding.propertyInitializer && binding.declarationEnd <= node.start && !resolving.has(binding)) {
        const next = new Set(resolving)
        next.add(binding)
        return valueOf(binding.propertyInitializer, environment, next)
      }
      return emptyValue()
    }
    if (node.type === 'ConditionalExpression') {
      const condition = literalBoolean(node.test)
      if (condition === true) return valueOf(node.consequent, environment, resolving)
      if (condition === false) return valueOf(node.alternate, environment, resolving)
      return mergeValue(
        valueOf(node.consequent, environment, resolving),
        valueOf(node.alternate, environment, resolving),
      )
    }
    if (node.type !== 'CallExpression') return emptyValue()
    const propertyKind = fastCheckMember(node, propertyConstructors)
    if (!propertyKind) return emptyValue()
    const callback = valueOf(node.arguments.at(-1), environment, resolving).callables
    return {
      callables: [],
      properties: callback.length === 0 ? [] : [{ async: propertyKind === 'asyncProperty', callback }],
    }
  }
  const environmentKey = (environment) => [...environment.entries()]
    .sort(([left], [right]) => left.position - right.position)
    .map(([binding, value]) => {
      const callables = value.callables.map(({ fn }) => fn.start).sort((left, right) => left - right).join(',')
      const properties = value.properties
        .flatMap(({ async, callback }) => callback.map(({ fn }) => `${async ? 'a' : 's'}${fn.start}`))
        .sort()
        .join(',')
      return `${binding.position}:${callables}:${properties}`
    })
    .join('|')
  const bindCall = (target, arguments_, callerEnvironment) => {
    const environment = new Map(target.environment)
    const parameters = functionParameters.get(target.fn) ?? []
    for (let index = 0; index < parameters.length; index++) {
      const argument = valueOf(arguments_[index], callerEnvironment)
      for (const binding of parameters[index]) environment.set(binding, argument)
    }
    return { fn: target.fn, environment }
  }
  const closureHasUse = (declaration) => {
    const root = functionByBodyStart.get(declaration.bodyStart)
    if (!root) return false
    const queue = [{ fn: root, environment: new Map() }]
    const visited = new Set()
    const functionsWithUses = new Set(uses.map(({ owner }) => owner).filter(Boolean))

    for (let index = 0; index < queue.length; index++) {
      const state = queue[index]
      const key = `${state.fn.start}|${environmentKey(state.environment)}`
      if (visited.has(key)) continue
      visited.add(key)
      if (functionsWithUses.has(state.fn)) return true

      for (const call of callsByFunction.get(state.fn) ?? []) {
        const runner = fastCheckMember(call.node, propertyRunners)
        if (runner) {
          for (const property of valueOf(call.node.arguments[0], state.environment).properties) {
            if (property.async && !returnedOrAwaited(call.node, call.ancestors, state.fn)) continue
            queue.push(...property.callback)
          }
          continue
        }
        for (const target of valueOf(call.node.callee, state.environment).callables) {
          queue.push(bindCall(target, call.node.arguments, state.environment))
        }
      }
    }
    return false
  }

  const analysis = {
    imports: [...new Map(imports.map((node) => [node.start, node])).values()],
    uses: [...new Map(uses.map((use) => [use.position, use])).values()].sort((left, right) => left.position - right.position),
    closureHasUse,
  }
  byModule.set(module, analysis)
  return analysis
}

const sourceCompileStem = (source) => {
  const prefix = 'src/Wanxiangshu/'
  if (!source.startsWith(prefix) || !source.endsWith('.fs')) return undefined
  return source.slice(prefix.length, -'.fs'.length)
}

export const validateSurfaceManifest = (manifest = SURFACE_MANIFEST, root = process.cwd()) => {
  const failures = []
  const fail = (message) => failures.push(message)
  const requirements = join(root, 'requirements')
  const fsprojPath = join(root, 'src/Wanxiangshu/Wanxiangshu.fsproj')
  const fsproj = existsSync(fsprojPath) ? readFileSync(fsprojPath, 'utf8') : ''
  const executableFiles = walk(requirements, ['.test.mjs', '.mjs', '.js']).map(normalize)
  const executableSources = executableFiles.map((file) => ({ file, source: readFileSync(file, 'utf8') }))
  const testSources = executableSources
    .filter(({ file }) => file.endsWith('.test.mjs'))
    .map(({ file, source }) => {
      const syntax = parseModule(source, file)
      return { file, source, syntax, declarations: scanTestSource(file, source, syntax) }
    })
  const seenModules = new Set()

  if (!Array.isArray(manifest)) {
    return ['surface manifest must be an array']
  }

  // Reject consumer metadata for modules no longer in the manifest. A stale
  // SURFACE_CONSUMERS entry grants phantom import authority to a surface that
  // no longer exists.
  if (manifest === SURFACE_MANIFEST) {
    const manifestModules = new Set(manifest.map((entry) => entry?.module).filter(Boolean))
    for (const consumerModule of Object.keys(SURFACE_CONSUMERS)) {
      if (!manifestModules.has(consumerModule)) {
        fail(`${consumerModule}: stale SURFACE_CONSUMERS entry for unregistered module`)
      }
    }
  }

  for (const entry of manifest) {
    if (entry === null || typeof entry !== 'object' || Array.isArray(entry)) {
      fail('manifest entry must be an object')
      continue
    }
    const label = typeof entry.module === 'string' ? entry.module : '<missing module>'
    if (seenModules.has(label)) fail(`${label}: duplicate manifest module`)
    seenModules.add(label)

    if (!/^[A-Za-z0-9_-]+(?:\/[A-Za-z0-9_-]+)*\.js$/.test(entry.module ?? '')) {
      fail(`${label}: module must be a relative emitted .js path`)
    }
    if (!/^[a-z0-9-]+$/.test(entry.owner ?? '')) fail(`${label}: owner must be a package slug`)
    if (!Array.isArray(entry.laws) || entry.laws.length === 0 || entry.laws.some((law) => typeof law !== 'string')) {
      fail(`${label}: laws must be a non-empty list of law ids`)
    }
    if (!['json', 'opaque-capability'].includes(entry.representation)) {
      fail(`${label}: invalid representation ${entry.representation}`)
    }
    if (!['pure', 'resource'].includes(entry.kind)) fail(`${label}: invalid kind ${entry.kind}`)

    const ownerWhatPath = `requirements/${entry.owner}/WHAT.md`
    if (!existsSync(join(root, ownerWhatPath))) {
      fail(`${label}: missing owner WHAT.md (${ownerWhatPath})`)
      continue
    }

    const laws = Array.isArray(entry.laws) ? entry.laws : []
    const lawOwners = entry.lawOwners && typeof entry.lawOwners === 'object' ? entry.lawOwners : {}
    for (const law of laws) {
      const lawOwner = typeof lawOwners[law] === 'string' ? lawOwners[law] : entry.owner
      const lawWhatPath = `requirements/${lawOwner}/WHAT.md`
      if (!existsSync(join(root, lawWhatPath))) {
        fail(`${label}: law ${law} owner WHAT is missing (${lawWhatPath})`)
        continue
      }
      const lawIds = new Set(whatHeadings(read(root, lawWhatPath)).map(({ id }) => id))
      if (!lawIds.has(law)) fail(`${label}: law ${law} is absent from ${lawWhatPath}`)
    }

    if (typeof entry.source !== 'string' || !existsSync(join(root, entry.source))) {
      fail(`${label}: missing production source ${entry.source}`)
    }

    if (typeof entry.module === 'string' && !existsSync(join(root, 'dist', entry.module))) {
      fail(`${label}: missing emitted surface dist/${entry.module}`)
    }
    const compileStem = sourceCompileStem(entry.source ?? '')
    if (!compileStem) {
      fail(`${label}: source must be a src/Wanxiangshu .fs path`)
    } else if (!fsproj.includes(`<Compile Include="${compileStem}.fs"/>`)) {
      fail(`${label}: ${compileStem}.fs is not compiled by Wanxiangshu.fsproj`)
    }

    const importedBy = typeof entry.module === 'string'
      ? testSources.filter(({ source, syntax }) => analyzeSurface(source, entry.module, syntax).imports.length > 0)
      : []
    const consumerPackages = new Set(
      typeof entry.module === 'string' && Array.isArray(SURFACE_CONSUMERS[entry.module]) ? SURFACE_CONSUMERS[entry.module] : [],
    )

    const attributedUses = []
    if (typeof entry.module === 'string') {
      for (const testSource of importedBy) {
        const analysis = analyzeSurface(testSource.source, entry.module, testSource.syntax)
        for (const declaration of testSource.declarations) {
          if (analysis.closureHasUse(declaration)) attributedUses.push({ file: testSource.file, declaration })
        }
      }
    }
    const uniqueAttributedUses = [...new Map(attributedUses.map((use) => [`${use.file}:${use.declaration.start}`, use])).values()]
    const activeUses = uniqueAttributedUses.filter(({ declaration }) => declaration.state === 'active')
    const isLawProof = ({ file, declaration }) => {
      if (declaration.whatIds.length !== 1) return false
      const law = declaration.whatIds[0]
      if (!laws.includes(law)) return false
      const lawOwner = typeof lawOwners[law] === 'string' ? lawOwners[law] : entry.owner
      return packageOfTestFile(file, requirements) === lawOwner
    }
    const lawProofs = activeUses.filter(isLawProof)
    const ownerPackages = new Set([
      entry.owner,
      ...laws.map((law) => typeof lawOwners[law] === 'string' ? lawOwners[law] : entry.owner),
    ])
    const isAllowedUse = (use) => {
      if (isLawProof(use)) return true
      const pkg = packageOfTestFile(use.file, requirements)
      return pkg !== null && (ownerPackages.has(pkg) || consumerPackages.has(pkg))
    }

    if (typeof entry.module === 'string' && importedBy.length === 0) {
      fail(`${label}: no .test.mjs imports the registered surface`)
    } else if (typeof entry.module === 'string' && activeUses.length === 0) {
      fail(`${label}: surface import has no active executable use in a .test.mjs`)
    }
    if (typeof entry.module === 'string' && lawProofs.length === 0) {
      fail(`${label}: no active owner-law declaration has a production-bound surface use`)
    }

    // Per-consumer rejection: every active import must be law-authorized or
    // declared as an explicit cross-owner consumer. An unrelated test that
    // merely imports the surface is a false green, not proof.
    if (typeof entry.module === 'string') {
      for (const use of activeUses) {
        if (!isAllowedUse(use)) {
          const pkg = packageOfTestFile(use.file, requirements) ?? '?'
          fail(`${label}: unauthorized active import use from ${relativePath(use.file, root)}:${use.declaration.line} (package ${pkg} has no law or declared consumer edge)`)
        }
      }
    }
  }
  return failures
}

export const run = ({ root = process.cwd(), manifest = SURFACE_MANIFEST } = {}) => {
  const failures = validateSurfaceManifest(manifest, root)
  if (failures.length > 0) {
    console.error(`js-surface-manifest: ${failures.length} violation(s)`)
    for (const failure of failures) console.error(`  ${failure}`)
    return 1
  }
  console.log(`js-surface-manifest: OK — ${manifest.length} registered surfaces, laws and contract imports closed`)
  return 0
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href
if (isMain) process.exit(run())
