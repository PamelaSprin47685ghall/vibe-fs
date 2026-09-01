namespace Wanxiangshu.Repository.Knowledge.Casebook

/// CASE-003: typed observation capture from the final execution layer.
///
/// Captures happen at the Host tool-execution boundary (tool.execute.after:
/// args + rendered output) — never from transcript text. Capture is
/// best-effort: an unparseable execution yields None, which only means one
/// fewer change-detection opportunity, never a failed Inspector call.
module CasebookCapture =

    /// Stable content fingerprint for FileRead observations (CASE-003).
    val contentHash: text: string -> string

    /// read: args.path + rendered output → FileRead (hash of the observed text).
    val ofReadExecution: args: obj -> output: string -> Observation option

    /// glob: output lines are the matched relative paths (rendered one per
    /// line); pattern comes from args (pattern / glob / query, best-effort).
    val ofGlobExecution: args: obj -> output: string -> Observation option

    /// grep: pattern from args; matches rendered as "path:line:index:text"
    /// lines — parse best-effort, keep the raw text for the match payload.
    val ofGrepExecution: args: obj -> output: string -> Observation option

    /// Dispatch by tool name (CASE-003).
    val capture: toolName: string -> args: obj -> output: string -> Observation option

    /// §63: parse a typed shell command and, if it denotes a single-file read,
    /// return a FileRead observation (content hash empty because output is not
    /// available from the command text).
    val ofExecCommand: command: string -> Observation option
