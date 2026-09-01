namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode

module TurnBinding =
    val fromProjection:
        sessionId: SessionId ->
        projection: PromptAuthority.PromptAuthorityProjection ->
        userBindings: Dictionary<string, PhysicalUserMessageId> ->
        continuationIds: Set<string> ->
            ActiveRunBinding option

    type Store =
        new: unit -> Store

        member UserMessageBindings: Dictionary<string, PhysicalUserMessageId>

        member BindUserMessage: sessionId: SessionId * physical: PhysicalUserMessageId * ?agentRole: Role -> unit

        member BindContinuationUserMessage: sessionId: SessionId * physical: PhysicalUserMessageId -> unit

        member BindPhysicalUserMaterial: sessionId: SessionId * physical: PhysicalUserMessageId -> unit

        member BindActiveRun: binding: ActiveRunBinding -> unit

        member ActiveRunBinding: sessionId: SessionId * ?projection: AgentProjectionSet -> ActiveRunBinding option

        member TryPhysicalUserMessage: sessionId: SessionId -> PhysicalUserMessageId option

        member ClearSession: sessionId: SessionId -> unit
