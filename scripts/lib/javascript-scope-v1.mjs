import { isFunction, walkSyntax } from './js-syntax.mjs'

const patternIdentifiers = (pattern) => {
  if (!pattern) return []
  if (pattern.type === 'Identifier') return [pattern]
  if (pattern.type === 'AssignmentPattern') return patternIdentifiers(pattern.left)
  if (pattern.type === 'RestElement') return patternIdentifiers(pattern.argument)
  if (pattern.type === 'ArrayPattern') return pattern.elements.flatMap(patternIdentifiers)
  if (pattern.type === 'ObjectPattern') return pattern.properties.flatMap((property) =>
    property.type === 'RestElement' ? patternIdentifiers(property.argument) : patternIdentifiers(property.value))
  return []
}

const blockScope = (node) => node?.type === 'Program'
  || node?.type === 'BlockStatement'
  || node?.type === 'CatchClause'
  || node?.type === 'ForStatement'
  || node?.type === 'ForInStatement'
  || node?.type === 'ForOfStatement'
  || node?.type === 'SwitchStatement'
  || node?.type === 'StaticBlock'

const nearestScope = (ancestors, accepts) => {
  for (let index = ancestors.length - 1; index >= 0; index -= 1) {
    if (accepts(ancestors[index])) return ancestors[index]
  }
  return null
}

const rootIdentifier = (node) => {
  if (node?.type === 'Identifier') return node
  if (node?.type === 'ChainExpression') return rootIdentifier(node.expression)
  if (node?.type === 'MemberExpression') return rootIdentifier(node.object)
  if (node?.type === 'CallExpression' || node?.type === 'NewExpression') return rootIdentifier(node.callee)
  if (node?.type === 'AssignmentExpression') return rootIdentifier(node.left)
  if (node?.type === 'UpdateExpression') return rootIdentifier(node.argument)
  if (node?.type === 'TaggedTemplateExpression') return rootIdentifier(node.tag)
  return null
}

const staticImportInitializer = (node, resolveBinding) => {
  const value = node?.type === 'AwaitExpression' ? node.argument : node
  if (value?.type === 'ImportExpression' && typeof value.source?.value === 'string') return true
  if (value?.type === 'CallExpression'
    && value.callee?.type === 'Identifier'
    && value.callee.name === 'require'
    && value.arguments?.length === 1
    && typeof value.arguments[0]?.value === 'string') return true
  const root = rootIdentifier(value)
  return root !== null && resolveBinding(root)?.provenance === 'imported'
}

export const createJavaScriptScopeResolverV1 = (program, { preboundNames = [] } = {}) => {
  if (!program || typeof program !== 'object' || typeof program.type !== 'string' || !Array.isArray(preboundNames)) {
    throw new TypeError('JavaScript scope resolver requires an AST and prebound names')
  }
  const ancestorsByNode = new WeakMap()
  const parentByNode = new WeakMap()
  const bindingByIdentifier = new WeakMap()
  const bindingsByScope = new Map()
  const declarators = []
  const programScope = program.type === 'Program' ? program : { type: 'Program' }

  const addBinding = (identifier, scope, provenance = 'local') => {
    if (identifier?.type !== 'Identifier' || scope === null) return null
    const binding = { name: identifier.name, scope, provenance }
    const entries = bindingsByScope.get(scope) ?? []
    entries.push(binding)
    bindingsByScope.set(scope, entries)
    bindingByIdentifier.set(identifier, binding)
    return binding
  }
  for (const name of preboundNames) {
    if (typeof name !== 'string' || !/^\$\d+$/.test(name)) throw new TypeError('prebound JavaScript names must be Fable argument holes')
    const entries = bindingsByScope.get(programScope) ?? []
    entries.push({ name, scope: programScope, provenance: 'local' })
    bindingsByScope.set(programScope, entries)
  }

  walkSyntax(program, (node, parent, _key, ancestors) => {
    ancestorsByNode.set(node, ancestors)
    parentByNode.set(node, parent)
    if (node.type === 'ImportDeclaration') {
      for (const specifier of node.specifiers ?? []) addBinding(specifier.local, programScope, 'imported')
      return
    }
    if (node.type === 'VariableDeclarator') {
      const declaration = parent
      const scope = declaration?.kind === 'var'
        ? nearestScope(ancestors, (candidate) => isFunction(candidate) || candidate.type === 'Program') ?? programScope
        : nearestScope(ancestors, blockScope) ?? programScope
      const bindings = patternIdentifiers(node.id).map((identifier) => addBinding(identifier, scope)).filter(Boolean)
      declarators.push({ bindings, init: node.init })
      return
    }
    if (node.type === 'FunctionDeclaration') {
      addBinding(node.id, nearestScope(ancestors, blockScope) ?? programScope)
      for (const identifier of (node.params ?? []).flatMap(patternIdentifiers)) addBinding(identifier, node)
      return
    }
    if (node.type === 'FunctionExpression' || node.type === 'ArrowFunctionExpression') {
      if (node.type === 'FunctionExpression') addBinding(node.id, node)
      for (const identifier of (node.params ?? []).flatMap(patternIdentifiers)) addBinding(identifier, node)
      return
    }
    if (node.type === 'ClassDeclaration') addBinding(node.id, nearestScope(ancestors, blockScope) ?? programScope)
    if (node.type === 'ClassExpression') addBinding(node.id, node)
    if (node.type === 'CatchClause') {
      for (const identifier of patternIdentifiers(node.param)) addBinding(identifier, node)
    }
  })

  const resolveBinding = (identifier) => {
    if (identifier?.type !== 'Identifier') return null
    const ancestors = ancestorsByNode.get(identifier) ?? []
    for (let index = ancestors.length - 1; index >= 0; index -= 1) {
      const matches = (bindingsByScope.get(ancestors[index]) ?? []).filter(({ name }) => name === identifier.name)
      if (matches.length > 0) return matches.at(-1)
    }
    return (bindingsByScope.get(programScope) ?? []).filter(({ name }) => name === identifier.name).at(-1) ?? null
  }

  let changed = true
  while (changed) {
    changed = false
    for (const { bindings, init } of declarators) {
      if (!staticImportInitializer(init, resolveBinding)) continue
      for (const binding of bindings) {
        if (binding.provenance === 'imported') continue
        binding.provenance = 'imported'
        changed = true
      }
    }
  }

  return ({ node }) => {
    if (node?.type === 'ImportDeclaration' || node?.type === 'ImportExpression') {
      return { binding_provenance: 'imported', program_scope: false }
    }
    if (node?.type === 'VariableDeclaration') {
      const bindings = (node.declarations ?? []).flatMap(({ id }) => patternIdentifiers(id)).map((identifier) => bindingByIdentifier.get(identifier)).filter(Boolean)
      return {
        binding_provenance: 'local',
        program_scope: bindings.length > 0 && bindings.every(({ scope }) => scope === programScope),
      }
    }
    if ((node?.type === 'CallExpression' || node?.type === 'NewExpression')
      && ['ArrowFunctionExpression', 'ClassExpression', 'FunctionExpression'].includes(node.callee?.type)) {
      return { binding_provenance: 'local', program_scope: false }
    }
    if (node?.type === 'Identifier') {
      const parent = parentByNode.get(node)
      if ((parent?.type === 'MemberExpression' && parent.property === node && parent.computed !== true)
        || parent?.type === 'MetaProperty') {
        return { binding_provenance: 'local', program_scope: false }
      }
    }
    const root = rootIdentifier(node)
    if (root === null) {
      return ['AssignmentExpression', 'CallExpression', 'MemberExpression', 'NewExpression', 'UpdateExpression'].includes(node?.type)
        ? { binding_provenance: 'unresolved', program_scope: false }
        : { binding_provenance: 'local', program_scope: false }
    }
    const binding = resolveBinding(root)
    if (!binding) return { binding_provenance: 'free', program_scope: false }
    return { binding_provenance: binding.provenance, program_scope: binding.scope === programScope }
  }
}
