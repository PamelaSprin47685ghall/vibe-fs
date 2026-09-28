export function compilerEvidenceReferences(text) {
  const pattern = /FSharp\.Compiler(?:\.Service)?|FSharpChecker|Fable\.(?:Compiler|AST)\.dll|scanCompilerObservationsV1|scanDslCompilerEvidence|runLocalityDependencyScan|scanProductionLocalitySliceReportV1|locality-symbol-uses/
  return text.split('\n').flatMap((line, index) => {
    const match = pattern.exec(line)
    return match ? [{ line: index + 1, token: match[0] }] : []
  })
}
