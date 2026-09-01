/** Replace F# comments and literals with spaces while preserving offsets and newlines. */
export function maskFSharpTrivia(source) {
  const masked = source.split('')
  const blank = (index) => {
    if (masked[index] !== '\n' && masked[index] !== '\r') masked[index] = ' '
  }
  const blankPair = (index) => {
    blank(index)
    blank(index + 1)
  }

  let index = 0
  while (index < source.length) {
    if (source.startsWith('//', index)) {
      while (index < source.length && source[index] !== '\n') blank(index++)
      continue
    }

    if (source.startsWith('(*', index)) {
      let depth = 1
      blankPair(index)
      index += 2
      while (index < source.length && depth > 0) {
        if (source.startsWith('(*', index)) {
          depth++
          blankPair(index)
          index += 2
        } else if (source.startsWith('*)', index)) {
          depth--
          blankPair(index)
          index += 2
        } else {
          blank(index++)
        }
      }
      continue
    }

    if (source.startsWith('"""', index)) {
      for (let offset = 0; offset < 3; offset++) blank(index + offset)
      index += 3
      while (index < source.length && !source.startsWith('"""', index)) blank(index++)
      if (index < source.length) {
        for (let offset = 0; offset < 3; offset++) blank(index + offset)
        index += 3
      }
      continue
    }

    if (source.startsWith('@"', index)) {
      blankPair(index)
      index += 2
      while (index < source.length) {
        if (source.startsWith('""', index)) {
          blankPair(index)
          index += 2
        } else if (source[index] === '"') {
          blank(index++)
          break
        } else {
          blank(index++)
        }
      }
      continue
    }

    if (source[index] === '"') {
      blank(index++)
      while (index < source.length) {
        if (source[index] === '\\') {
          blank(index++)
          if (index < source.length) blank(index++)
        } else if (source[index] === '"') {
          blank(index++)
          break
        } else {
          blank(index++)
        }
      }
      continue
    }

    const characterLiteralLength =
      source[index] === "'" && source[index + 1] === '\\' && source[index + 3] === "'"
        ? 4
        : source[index] === "'" && source[index + 2] === "'"
          ? 3
          : 0
    if (characterLiteralLength > 0) {
      for (let offset = 0; offset < characterLiteralLength; offset++) blank(index + offset)
      index += characterLiteralLength
      continue
    }

    index++
  }

  return masked.join('')
}
