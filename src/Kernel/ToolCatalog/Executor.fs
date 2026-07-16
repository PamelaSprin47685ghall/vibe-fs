module Wanxiangshu.Kernel.ToolCatalog.Executor

open Wanxiangshu.Kernel.ToolCatalog.ToolSpec
open Wanxiangshu.Kernel

let internal executorSpec: ToolSpec =
    { name = "executor"
      description =
        "Executes a shell command, Python code, or JavaScript/TypeScript program synchronously with a strict timeout budget. On completion (or timeout) the captured output is either returned directly or summarized when it exceeds max_bytes. If executing Python or JavaScript, specify dependencies in the \"dependencies\" argument."
      paramDocs =
        map
            [ "language", "Execution language: shell, python, or javascript"
              "command", "The program to execute."
              "dependencies", "Dependencies to install (for python or javascript)."
              "timeout_type", "Execution timeout budget: 'short' (10s) or 'long' (100s)."
              "mode", "Execution mode: 'ro' (concurrent/unordered), 'rw' (sequential/ordered)."
              "what_to_summarize",
              "What the summary should focus on. Becomes the executor subagent's task description, so phrase it as a directive (e.g. 'only keep stack traces and exit codes')."
              "max_bytes",
              "Exceeding this value will trigger summarization instead of obtaining the raw output. It is not recommended to exceed 8192 unless there is a special reason." ]
      requiredFields = [ "command"; "timeout_type"; "mode"; "what_to_summarize"; "max_bytes" ] }

let internal ptySpawnSpec: ToolSpec =
    { name = "pty_spawn"
      description = "Spawn a pseudo-terminal (PTY) subprocess interactively."
      paramDocs =
        map
            [ "command", "The command to run in the PTY."
              "cwd", "Working directory for the spawned process." ]
      requiredFields = [ "command" ] }

let internal ptyWriteSpec: ToolSpec =
    { name = "pty_write"
      description = "Write text to a running PTY process."
      paramDocs =
        map
            [ "session_id", "PTY session ID returned by pty_spawn."
              "text", "Text to write to the PTY stdin." ]
      requiredFields = [ "session_id"; "text" ] }

let internal ptyReadSpec: ToolSpec =
    { name = "pty_read"
      description = "Read output from a running PTY process."
      paramDocs =
        map
            [ "session_id", "PTY session ID returned by pty_spawn."
              "timeout_ms", "Optional read timeout in milliseconds." ]
      requiredFields = [ "session_id" ] }

let internal ptyListSpec: ToolSpec =
    { name = "pty_list"
      description = "List all active PTY sessions."
      paramDocs = Map.empty
      requiredFields = [] }

let internal ptyKillSpec: ToolSpec =
    { name = "pty_kill"
      description = "Kill a PTY session by its session ID."
      paramDocs = map [ "session_id", "PTY session ID returned by pty_spawn." ]
      requiredFields = [ "session_id" ] }
