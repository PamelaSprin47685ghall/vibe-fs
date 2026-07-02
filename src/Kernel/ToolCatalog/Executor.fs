module Wanxiangshu.Kernel.ToolCatalog.Executor

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec
open Wanxiangshu.Kernel

let internal executorSpec: ToolSpec =
    { name = "executor"
      description =
        "Executes a shell command, Python code, or JavaScript/TypeScript program synchronously with a strict timeout budget. On completion (or timeout) the captured output is either returned directly or summarized when it exceeds 8192 bytes. If executing Python or JavaScript, specify dependencies in the \"dependencies\" argument."
      paramDocs =
        map
            [ "language", "Execution language: shell, python, or javascript"
              "program", "The program to execute."
              "dependencies", "Dependencies to install (for python or javascript)."
              "timeout_type",
              "Execution timeout budget: 'short' (1s), 'long' (10s), or 'last-resort' (100s). Use 'last-resort' only when absolutely necessary."
              "mode",
              "Execution mode: 'ro' for read-only/diagnostic/compile/test commands, 'rw' for commands that modify project source files (use ro if modifying no-source files)."
              "what_to_summarize",
              "What the summary should focus on. Becomes the executor subagent's task description, so phrase it as a directive (e.g. 'only keep stack traces and exit codes')." ]
      requiredFields = [ "program"; "timeout_type"; "mode"; "what_to_summarize" ] }

let internal executorWaitSpec: ToolSpec =
    { name = "executor_wait"
      description = "Wait for background executor output."
      paramDocs = map [ "ms", "Wait time in milliseconds." ]
      requiredFields = [] }

let internal executorAbortSpec: ToolSpec =
    { name = "executor_abort"
      description = "Abort background executor task."
      paramDocs = Map.empty
      requiredFields = [] }
