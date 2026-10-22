namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module PromptRecovery =
    type ClaimOutcome =
        | Proven of PhysicalUserMessageId
        | StillPending of hasReceipt: bool
        | Unreadable of reason: string

    type Reconciled =
        { SessionId: SessionId
          PromptKey: PromptKey
          Outcome: ClaimOutcome }

    val reconcile: journal: AgentJournal option -> transcriptOpt: ITranscriptPort option -> Task<Reconciled list>
