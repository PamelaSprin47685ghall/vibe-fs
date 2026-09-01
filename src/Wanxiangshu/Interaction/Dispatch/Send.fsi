namespace Wanxiangshu.Interaction.Dispatch

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode

[<AutoOpen>]
module PromptDispatcherSend =
    type PromptDispatcher.Runtime with
        member SendAgentOwnerRoot:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            identitySeed: PromptAuthority.IdentitySeed ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            onAccepted: (PhysicalUserMessageId -> unit) option ->
                Task<Result<PromptKey, string>>

        member SendAgentOwnerRootDetachedObserved:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            identitySeed: PromptAuthority.IdentitySeed ->
            directory: string option ->
            onFailure: (string -> Task) ->
                Task<Result<PromptKey, string>>

        member SendAgentOwnerRootWithTools:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            identitySeed: PromptAuthority.IdentitySeed ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            onAccepted: (PhysicalUserMessageId -> unit) option ->
            tools: Map<string, bool> ->
            model: OpencodeModel option ->
                Task<Result<PromptKey, string>>

        member SendContinuation:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            continuation: PromptAuthority.ContinuationKind ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            onAccepted: (PhysicalUserMessageId -> unit) option ->
                Task<Result<PromptKey, string>>

        member SendGateNudge:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            continuation: PromptAuthority.ContinuationKind ->
            gateKind: string ->
            terminalProviderRun: ProviderRunIdentity ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            onAccepted: (PhysicalUserMessageId -> unit) option ->
                Task<Result<PromptKey, string>>

        member SendContinuationWithTools:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            continuation: PromptAuthority.ContinuationKind ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            onAccepted: (PhysicalUserMessageId -> unit) option ->
            tools: Map<string, bool> ->
                Task<Result<PromptKey, string>>

        member SendInteractionRepair:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            requestId: BloggerRequestId ->
            terminalProviderRun: ProviderRunIdentity ->
            repairKind: string ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            onAccepted: (PhysicalUserMessageId -> unit) option ->
                Task<Result<PromptKey, string>>

        member SendManagerIdleEncouragement:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            lifeId: ManagerLifeId ->
            conditionKey: string ->
            terminalProviderRun: ProviderRunIdentity ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            onAccepted: (PhysicalUserMessageId -> unit) option ->
                Task<Result<PromptKey, string>>

        member internal SendIdleContinuation:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            continuation: PromptAuthority.ContinuationKind ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            onAccepted: (PhysicalUserMessageId -> unit) option ->
            physicalAdmission: (unit -> Result<unit, QuiescencePermitFailure>) ->
                Task<PromptDispatcher.SendAttemptOutcome>

        member internal SendIdleGateNudge:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            continuation: PromptAuthority.ContinuationKind ->
            gateKind: string ->
            terminalProviderRun: ProviderRunIdentity ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            physicalAdmission: (unit -> Result<unit, QuiescencePermitFailure>) ->
                Task<PromptDispatcher.SendAttemptOutcome>

        member internal SendIdleInteractionRepair:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            requestId: BloggerRequestId ->
            terminalProviderRun: ProviderRunIdentity ->
            repairKind: string ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            physicalAdmission: (unit -> Result<unit, QuiescencePermitFailure>) ->
                Task<PromptDispatcher.SendAttemptOutcome>

        member internal SendIdleManagerIdleEncouragement:
            port: ISessionHostPort ->
            sessionId: SessionId ->
            text: string ->
            lifeId: ManagerLifeId ->
            conditionKey: string ->
            terminalProviderRun: ProviderRunIdentity ->
            profile: PromptAuthority.AuthorityExecutionProfile ->
            effectiveAgent: string ->
            directory: string option ->
            awaitMode: PromptDispatcher.AwaitMode ->
            physicalAdmission: (unit -> Result<unit, QuiescencePermitFailure>) ->
                Task<PromptDispatcher.SendAttemptOutcome>
