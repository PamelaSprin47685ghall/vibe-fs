namespace Wanxiangshu.OpenCode.Tools

open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Persistence.Journal

[<AbstractClass; Sealed; AttachMembers>]
type ToolRuntimeScopeSurface =
    static member evaluateRetirementBlockers: journal: JournalHandle * scenario: obj -> Task<string array>

    static member coldRetirementBlockers:
        journal: JournalHandle * workspace: string * managerSessionId: string -> Task<string array>

    static member verifyDevOpsReturnDrain: journal: JournalHandle * scenario: obj -> Task<obj>
    static member coldDevOpsReturn: journal: JournalHandle * workspace: string -> Task<obj>
    static member undrainedDevOpsReturn: journal: JournalHandle -> Task<obj>

    static member ptyAdmissionProbe:
        journal: JournalHandle * workspace: string * child: string * realBackend: bool * stateDirectory: string -> obj

    static member startPtyExecution:
        journal: JournalHandle * child: string * physical: string * provider: string -> Task<unit>

    static member verifyOwnershipRetirementContract: scenario: obj -> Task<obj>
    static member verifyUnknownProcessDrain: unit -> Task<obj>
    static member verifyProcessRetirementContract: unit -> Task<obj>
