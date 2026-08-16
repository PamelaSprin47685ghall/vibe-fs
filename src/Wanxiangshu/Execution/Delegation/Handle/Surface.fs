namespace Wanxiangshu.Execution.Delegation

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// JS-native projection boundary for durable handle lifecycle. The projection
/// remains the delegation owner's typed implementation; JS receives snapshots,
/// never the union or map representation.
module HandleSurface =
    let private handle = HandleId.Agent(AgentHandleId.create "h1")
    let private completion kind : HandleCompletion =
        { Kind = kind
          CompletionRef = None
          CompletionDigest = None }

    let private lifecycleName lifecycle =
        match lifecycle with
        | HandleLifecycle.Active -> "Active"
        | HandleLifecycle.CompletedAwaitingJoin _ -> "CompletedAwaitingJoin"
        | HandleLifecycle.Abandoned _ -> "Abandoned"
        | HandleLifecycle.Retired -> "Retired"

    let private snapshot projection =
        match Map.tryFind handle projection.Handles with
        | None -> null
        | Some record ->
            box
                {| handle = HandleId.describe record.Handle
                   child = SessionId.value record.ChildSessionId
                   targetAgent = record.TargetAgent
                   role = record.CanonicalRole.ToString()
                   lifecycle = lifecycleName record.Lifecycle
                   creationOrder = record.CreationOrder |}

    let scenario (action: string) : obj =
        let linked =
            HandleProjection.link
                handle
                (SessionId.create "ses_child")
                "fast-coder"
                Role.Coder
                HandleOwnership.DurableParentHandle
                HandleProjection.empty

        match linked with
        | Error _ -> null
        | Ok projection ->
            let finalProjection =
                match action with
                | "complete" ->
                    match HandleProjection.complete handle (completion HandleCompletionKind.Terminal) projection with
                    | Ok value -> value
                    | Error _ -> projection
                | "abandon" ->
                    match HandleProjection.abandon handle HandleAbandonReason.ParentCancelled projection with
                    | Ok value -> value
                    | Error _ -> projection
                | "retire" ->
                    match HandleProjection.complete handle (completion HandleCompletionKind.Terminal) projection with
                    | Ok completed ->
                        match HandleProjection.retire handle completed with
                        | Ok value -> value
                        | Error _ -> projection
                    | Error _ -> projection
                | _ -> projection

            box
                {| ok = true
                   action = action
                   record = snapshot finalProjection
                   listable = HandleProjection.listable finalProjection |> List.length
                   joinable = HandleProjection.joinable finalProjection |> List.length |}

    /// Crash-reconciliation matrix at the handle owner. Duplicate completion
    /// and retirement are replayed through the same projection transitions.
    let crashScenario (action: string) : obj =
        let linked =
            HandleProjection.link
                handle
                (SessionId.create "ses_child")
                "fast-coder"
                Role.Coder
                HandleOwnership.DurableParentHandle
                HandleProjection.empty

        let projection =
            match linked with
            | Ok value -> value
            | Error _ -> HandleProjection.empty
        let completeProjection value =
            match HandleProjection.complete handle (completion HandleCompletionKind.Terminal) value with
            | Ok next -> next
            | Error _ -> value
        let retireProjection value =
            match HandleProjection.retire handle value with
            | Ok next -> next
            | Error _ -> value
        let completed =
            match action with
            | "completed"
            | "replayed-completed" -> completeProjection projection
            | "retired"
            | "replayed-retired" -> projection |> completeProjection |> retireProjection
            | _ -> projection

        let replayed =
            match action with
            | "replayed-completed" ->
                match HandleProjection.complete handle (completion HandleCompletionKind.Terminal) completed with
                | Ok value -> value
                | Error _ -> completed
            | "replayed-retired" ->
                let retired =
                    match HandleProjection.retire handle completed with
                    | Ok value -> value
                    | Error _ -> completed
                match HandleProjection.complete handle (completion HandleCompletionKind.Terminal) retired with
                | Ok value -> value
                | Error _ -> retired
            | _ -> completed

        match Map.tryFind handle replayed.Handles with
        | None -> null
        | Some record ->
            box
                {| lifecycle = lifecycleName record.Lifecycle
                   completion =
                       match record.LastCompletion with
                       | Some completion -> box {| kind = completion.Kind.ToString() |}
                       | None -> null
                   abandonReason = null
                   joinable = HandleProjection.joinable replayed |> List.length
                   retired = record.Lifecycle = HandleLifecycle.Retired |}

