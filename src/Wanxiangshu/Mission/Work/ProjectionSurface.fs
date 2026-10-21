namespace Wanxiangshu.Mission.Work

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.Journal

module ProjectionSurface =
    /// Audit and provider start from the same physical set. Successor
    /// visibility is decided in RelayNarrativeTransform when a lifecycle
    /// tenure is supplied. This entry has no lifecycle fold, so it does not
    /// strip. The retirement cut remains a request-identity test.
    let projectMessages (messages: obj array) =
        box
            {| audit = messages
               provider = messages |}

    let apply
        (_journal: JournalHandle)
        (_sessionId: string)
        (_acceptedRequest: bool)
        (messages: obj array)
        : Task<obj> =
        task {
            let output = createObj [ "messages" ==> messages ]
            let disposition = RelayProjection.disposition None

            return
                box
                    {| disposition =
                        match disposition with
                        | RelayProjectionDisposition.Unchanged -> "unchanged"
                        | RelayProjectionDisposition.CurrentTenure -> "current-tenure"
                        | RelayProjectionDisposition.RetiredAttemptStopped -> "retired-attempt-stopped"
                       messages = output?messages
                       interrupted = (Array.empty: string array) |}
        }
