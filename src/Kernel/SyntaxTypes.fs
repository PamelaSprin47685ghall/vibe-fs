module VibeFs.Kernel.SyntaxTypes

/// One syntax problem located in a source file.
type SyntaxDiagnostic =
    { line: int
      column: int
      endLine: int
      endColumn: int
      severity: string
      message: string }

/// The outcome of checking a file's syntax — either an array of diagnostics with
/// the detected language, or a failure reason.
type SyntaxCheckResult =
    | Ok of lang: string * errors: SyntaxDiagnostic array
    | Failed of lang: string * reason: string
