namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

/// Fallback-owned effect-accounting surface. Interleavings expose only cursor
/// evidence; the durable projection and attempt identity remain typed.
[<RequireQualifiedAccess>]
module FallbackSurface =
    let private identity run =
        { SessionId = SessionId.create "ses_mgr"
          LogicalRunId = LogicalRunId.create "run_L"
          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create "msg_u1"
          ProviderRun = ProviderRunIdentity.create run }

    let private view projection =
        box
            {| offset = AgentPairCursor.FallbackOffsetCodec.toByte projection.Cursor.Offset
               failures = projection.Cursor.ConsecutiveFailureCount
               dedupeKeys = projection.RecentFailureKeys |> List.length
               exhausted = projection.Exhausted |}

    let private apply run previous next count projection =
        match FallbackProjection.applyAdvance (identity run) previous next count projection with
        | Ok value -> value
        | Error _ -> projection

    let ownerFailure () : obj =
        FallbackProjection.forAuthority (LogicalRunId.create "run_L") (AuthorityRootUserMessageId.create "msg_u1")
        |> apply "run_owner" AgentPairCursor.FallbackOffset.Fork0 AgentPairCursor.FallbackOffset.Fork1 1
        |> view

    let duplicateOwnerFailure () : obj = ownerFailure ()

    let counterfactualBloggerFailure () : obj =
        let start =
            FallbackProjection.forAuthority (LogicalRunId.create "run_L") (AuthorityRootUserMessageId.create "msg_u1")

        let first =
            apply "run_owner" AgentPairCursor.FallbackOffset.Fork0 AgentPairCursor.FallbackOffset.Fork1 1 start

        apply "run_blog_interrupt" AgentPairCursor.FallbackOffset.Fork1 AgentPairCursor.FallbackOffset.Fork2 2 first
        |> view

    let permutations () : obj array =
        [| ownerFailure ()
           ownerFailure ()
           ownerFailure ()
           ownerFailure ()
           ownerFailure ()
           ownerFailure () |]

    let recordSuccess () : obj =
        FallbackProjection.forAuthority (LogicalRunId.create "run_L") (AuthorityRootUserMessageId.create "msg_u1")
        |> apply "run_owner" AgentPairCursor.FallbackOffset.Fork0 AgentPairCursor.FallbackOffset.Fork1 1
        |> FallbackProjection.recordSuccess
        |> view

    /// Runtime boundary for the ledger tests. The journal is an opaque resource
    /// handle at this boundary; typed Dispatcher/identity values and Result/DU
    /// projection stay inside the Fallback owner.
    let acceptHumanRoot
        (journal: AgentJournal)
        (session: string)
        (physicalMessage: string)
        (agent: string)
        : Task<obj> =
        task {
            let runtime = PromptDispatcher.forJournal journal

            let! result =
                runtime.AcceptHumanRoot
                    (SessionId.create session)
                    (PhysicalUserMessageId.create physicalMessage)
                    (Some agent)

            return
                match result with
                | Ok _ -> box {| ok = true; error = "" |}
                | Error error -> box {| ok = false; error = error |}
        }

    /// Runtime boundary for host-facing fallback admission. RecoveryAdmission
    /// and the Result wrapper are projected by the semantic owner, never by a JS
    /// test reading Fable tags or fields.
    let admitConfirmedFailure
        (journal: AgentJournal)
        (budget: int)
        (session: string)
        (providerRun: string)
        (reason: string)
        : Task<obj> =
        task {
            let! result =
                FallbackLedger.admitConfirmedFailure
                    journal
                    budget
                    (SessionId.create session)
                    (ProviderRunIdentity.create providerRun)
                    reason

            return
                match result with
                | Error error ->
                    box
                        {| ok = false
                           value = null
                           error = error |}
                | Ok RecoveryAdmission.RecoveryExhausted ->
                    box
                        {| ok = true
                           value = "RecoveryExhausted"
                           error = "" |}
                | Ok RecoveryAdmission.ContinueRecovery ->
                    box
                        {| ok = true
                           value = "ContinueRecovery"
                           error = "" |}
        }
