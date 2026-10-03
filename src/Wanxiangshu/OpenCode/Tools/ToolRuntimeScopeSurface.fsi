namespace Wanxiangshu.OpenCode.Tools

open System.Threading.Tasks
open Fable.Core

[<AbstractClass; Sealed; AttachMembers>]
type ToolRuntimeScopeSurface =
    static member evaluateRetirementBlockers: scenario: obj -> Task<string array>
    static member verifyDevOpsReturnDrain: scenario: obj -> Task<obj>
