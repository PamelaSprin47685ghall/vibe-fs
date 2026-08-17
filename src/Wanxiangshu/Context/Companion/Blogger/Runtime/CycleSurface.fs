namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation.Identity

/// Blogger cycle effect surface. Materialization and receipt identity remain
/// projection-owned; JS observes only counts and explicit rejection text.
[<RequireQualifiedAccess>]
module BloggerCycleSurface =
    let private text (value: obj) =
        if isNull value then "" else string value

    let private request requestId bloggerId digest promptKey : OpenBloggerRequest =
        { RequestId = BloggerRequestId.create requestId
          MainSessionId = SessionId.create "ses-main"
          BloggerSessionId = SessionId.create bloggerId
          RequestKind = "main"
          ContextRef = BlobRef.create ("blob-" + digest)
          ContextDigest = BlobDigest.create ("sha-" + digest)
          ObservedPrefixEpochId = PrefixEpochId.create 0L
          PreviousIngestedThroughSequence = 0L
          NextIngestedThroughSequence = 1L
          FrameEpochId = FrameEpochId.create 0L
          SelectedFrameDigests = []
          PromptKey = promptKey |> Option.map PromptKey.create }

    let private receipt kind requestId run : BloggerCycleReceipt =
        { ProviderRun = ProviderRunIdentity.create run
          Kind =
            if kind = "squash" then
                BlogFrameKind.Squash
            else
                BlogFrameKind.Entry
          RequestId = BloggerRequestId.create requestId }

    let private view state =
        box
            {| openRequests = state.OpenByRequestId.Count
               openBloggers = state.OpenByBlogger.Count
               receipts = state.ByProviderRun.Count
               requestBindings = state.ProviderRunByRequestId.Count |}

    let scenario (actions: obj array) : obj =
        // DSL-MUTABLE: algorithm-scratch — scenario projection accumulator
        let mutable state = BloggerCycleProjection.empty
        // DSL-MUTABLE: algorithm-scratch — first scenario rejection
        let mutable error: string option = None

        for action in actions do
            match error with
            | Some _ -> ()
            | None ->
                match text (action?kind) with
                | "materialize" ->
                    let requestId = text (action?requestId)
                    let digest = text (action?digest)
                    let blogger = text (action?blogger)

                    let prompt =
                        if isNull (action?promptKey) then
                            None
                        else
                            Some(text (action?promptKey))

                    match BloggerCycleProjection.materialize (request requestId blogger digest prompt) state with
                    | Ok next -> state <- next
                    | Error reason -> error <- Some reason
                | "abandon" ->
                    state <-
                        BloggerCycleProjection.abandon
                            (BloggerRequestId.create (text (action?requestId)))
                            (SessionId.create (text (action?blogger)))
                            state
                | "entry"
                | "squash" ->
                    match
                        BloggerCycleProjection.recordReceipt
                            (receipt (text (action?kind)) (text (action?requestId)) (text (action?run)))
                            state
                    with
                    | Ok next -> state <- next
                    | Error reason -> error <- Some reason
                | unknown -> error <- Some("unknown action: " + unknown)

        match error with
        | Some reason -> box {| ok = false; error = reason |}
        | None -> box {| ok = true; state = view state |}
