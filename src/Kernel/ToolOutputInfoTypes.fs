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
    {
        stdout: string
        stderr: string option
        exitCode: int option
        signal: string option
        /// Command/process outcome label (completed, exit_error, killed_timeout, …).
        /// Wire key: exit_status — not ToolOutputMessage.status (no-change envelope).
        exitStatus: string
        truncated: bool
        summary: string option
    }





type WriteResultInfo =
    { path: string
      success: bool
      syntaxErrors: string list }

type ToolOutputContent =
    | Empty
    | Plain of string
    | Executor of ExecutorOutput
    | WriteResult of WriteResultInfo

let noChangeStatus = "No Change Since Previous Read/Write"

type ToolOutputMessage =
    { content: ToolOutputContent
      hint: string option
      syntax: string option
      iterator: string option
      status: string option }
