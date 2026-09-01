import { parse } from 'acorn'

export const parseModule = (source, file = '<source>') => {
  try {
    return parse(source, {
      allowHashBang: true,
      ecmaVersion: 'latest',
      locations: true,
      sourceType: 'module',
    })
  } catch (error) {
    error.message = `${file}: ${error.message}`
    throw error
  }
}

export const walkSyntax = (node, visit, ancestors = [], parent = null, key = null) => {
  if (node === null || typeof node !== 'object' || typeof node.type !== 'string') return
  visit(node, parent, key, ancestors)
  const nextAncestors = [...ancestors, node]
  for (const [childKey, child] of Object.entries(node)) {
    if (Array.isArray(child)) {
      for (const item of child) walkSyntax(item, visit, nextAncestors, node, childKey)
    } else walkSyntax(child, visit, nextAncestors, node, childKey)
  }
}

export const patternNames = (pattern) => {
  if (!pattern) return []
  switch (pattern.type) {
    case 'Identifier': return [pattern.name]
    case 'AssignmentPattern': return patternNames(pattern.left)
    case 'RestElement': return patternNames(pattern.argument)
    case 'ArrayPattern': return pattern.elements.flatMap(patternNames)
    case 'ObjectPattern': return pattern.properties.flatMap((property) =>
      property.type === 'RestElement' ? patternNames(property.argument) : patternNames(property.value))
    default: return []
  }
}

export const isFunction = (node) =>
  node?.type === 'ArrowFunctionExpression'
  || node?.type === 'FunctionExpression'
  || node?.type === 'FunctionDeclaration'
