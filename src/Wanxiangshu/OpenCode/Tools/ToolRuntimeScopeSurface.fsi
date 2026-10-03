namespace Wanxiangshu.OpenCode.Tools

open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Persistence.Journal

[<AbstractClass; Sealed; AttachMembers>]
type ToolRuntimeScopeSurface =
    static member evaluateRetirementBlockers: journal: JournalHandle * scenario: obj -> Task<string array>
    static member verifyDevOpsReturnDrain: journal: JournalHandle * scenario: obj -> Task<obj>
