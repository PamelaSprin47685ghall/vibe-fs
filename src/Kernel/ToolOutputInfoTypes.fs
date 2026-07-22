module Wanxiangshu.Kernel.ToolOutputInfoTypes

open Wanxiangshu.Kernel.Errors.DomainError

type FailureReason =
    | Aborted
    | ToolError of DomainError

let failureReasonText =
    function
    | Aborted -> "aborted"
    | ToolError e -> formatDomainError e

type ExecutorOutput =
    { stdout: string
      stderr: string option
      exitCode: int option
      signal: string option
      status: string
      truncated: bool
      summary: string option }

type FuzzyFindMatchItem =
    { path: string
      pattern: string option
      annotation: string option }

type FuzzyFindOutput =
    { pattern: string option
      totalMatched: int option
      totalFiles: int option
      matches: FuzzyFindMatchItem list }

type FuzzyGrepMatchItem =
    { path: string
      line: int
      content: string
      pattern: string option
      contextBefore: string list
      contextAfter: string list
      annotation: string option }

type FuzzyGrepOutput =
    { pattern: string option
      totalMatched: int option
      regexFallbackError: string option
      matches: FuzzyGrepMatchItem list }

type WriteResultInfo =
    { path: string
      success: bool
      syntaxErrors: string list }

type ToolOutputContent =
    | Empty
    | Plain of string
    | Executor of ExecutorOutput
    | FuzzyFind of FuzzyFindOutput
    | FuzzyGrep of FuzzyGrepOutput
    | WriteResult of WriteResultInfo

let noChangeStatus = "No Change Since Previous Read/Write"

type ToolOutputMessage =
    { content: ToolOutputContent
      hint: string option
      syntax: string option
      iterator: string option
      status: string option }
