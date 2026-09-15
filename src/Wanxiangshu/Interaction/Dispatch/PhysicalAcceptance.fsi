namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type PromptPhysicalOutcome =
    | Accepted of PhysicalUserMessageId
    | Rejected of string

module PromptPhysicalAcceptance =
    val register: promptKey: PromptKey -> callback: (PhysicalUserMessageId -> unit) -> unit
    val cancel: promptKey: PromptKey -> unit
    val accepted: promptKey: PromptKey -> physicalUserMessageId: PhysicalUserMessageId -> unit
    val rejected: promptKey: PromptKey -> reason: string -> unit
    val awaitConfirmation: promptKey: PromptKey -> timeoutMs: int option -> Task<PromptPhysicalOutcome option>
