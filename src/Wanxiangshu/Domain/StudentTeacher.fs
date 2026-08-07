namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel

/// Pure Student–Teacher rules. Natural-language knowledge never enters these
/// values; it belongs exclusively to the QA byte stream (PERSIST-011).
module StudentTeacher =

    let toolsFor =
        function
        | Role.Student, ProviderRequestKind.StudentLearn -> set [ ToolPermission.Teacher ]
        | Role.Student, ProviderRequestKind.StudentCompile ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Return ]
        | Role.Teacher, _ -> Roles.permissions Role.Teacher
        | _ -> Set.empty

    let teacherTierFor = id

    let teacherAgentFor tier =
        ManagedAgentCatalog.nameOf (teacherTierFor tier) Role.Teacher

    /// Framework-owned separators are limited to the minimum natural newline
    /// needed to prevent adjacent verbatim inputs from sticking together.
    let appendEntry (existing: string) (entry: string) =
        if String.IsNullOrEmpty existing then
            entry
        else
            existing + "\n\n" + entry

    /// Crash reconciliation may replay an append. Only a complete byte-for-byte
    /// tail match proves duplication; uncertainty preserves both inputs.
    let appendIdempotentTail (existing: string) (entry: string) =
        if existing.EndsWith(entry, StringComparison.Ordinal) then
            existing
        else
            appendEntry existing entry

    /// A replayed HumanRoot is not a new QA entry even after later exchanges
    /// moved it away from the tail. Identity comes from Prompt Authority; this
    /// check only proves that the byte stream still begins with that exact root.
    let hasOpening (existing: string) (opening: string) =
        existing = opening
        || existing.StartsWith(opening + "\n\n", StringComparison.Ordinal)

    type RunState =
        | LearnReady
        | TeacherWaiting
        | CompileDispatching
        | CompileReady
        | Closed

    let mayInvokeTeacher =
        function
        | RunState.LearnReady -> true
        | _ -> false

    let mayCompile =
        function
        | RunState.LearnReady -> true
        | _ -> false

    let mayReturn =
        function
        | RunState.CompileReady -> true
        | _ -> false
