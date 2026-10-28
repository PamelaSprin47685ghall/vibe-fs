import { parse } from 'acorn'

function titleText(node) {
  if (node?.type === 'Literal' && typeof node.value === 'string') return node.value
  if (node?.type === 'TemplateLiteral') return node.quasis.map((part) => part.value.cooked).join('${…}')
  if (node?.type === 'BinaryExpression' && node.operator === '+') return titleText(node.left) + titleText(node.right)
  return ''
}

/** Parse a test file's source and return its test declarations (kind, title, line). */
export function testDeclarations(source) {
  const declarations = []
  const visit = (node) => {
    if (!node || typeof node !== 'object') return
    if (node.type === 'CallExpression') {
      let callee = node.callee
      if (callee?.type === 'MemberExpression' && ['only', 'skip', 'todo'].includes(callee.property.name)) callee = callee.object
      if (callee?.type === 'Identifier' && ['test', 'it', 'integrationTest', 'e2eTest', 'releaseTest'].includes(callee.name)) {
        declarations.push({ kind: callee.name, title: titleText(node.arguments[0]), line: node.loc.start.line })
      }
    }
    for (const value of Object.values(node)) {
      if (Array.isArray(value)) value.forEach(visit)
      else if (value && typeof value === 'object') visit(value)
    }
  }
  visit(parse(source, { ecmaVersion: 'latest', sourceType: 'module', locations: true }))
  return declarations
}
