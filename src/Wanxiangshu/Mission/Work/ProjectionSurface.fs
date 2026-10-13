namespace Wanxiangshu.Mission.Work

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Work.OpenCode
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

    let apply (journal: JournalHandle) (sessionId: string) (acceptedRequest: bool) (messages: obj array) : Task<obj> =
        task {
            let interrupted = ResizeArray<string>()
            let output = createObj [ "messages" ==> messages ]

            let interrupt current =
                interrupted.Add(SessionId.value current)
                Task.FromResult()

            let! disposition =
                RelayNarrativeTransform.apply (Some journal.Journal) acceptedRequest interrupt (Some sessionId) None output

            return
                box
                    {| disposition =
                        match disposition with
                        | RelayProjectionDisposition.Unchanged -> "unchanged"
                        | RelayProjectionDisposition.CurrentIteration -> "current-iteration"
                        | RelayProjectionDisposition.RetiredAttemptStopped -> "retired-attempt-stopped"
                       messages = output?messages
                       interrupted = interrupted.ToArray() |}
        }
