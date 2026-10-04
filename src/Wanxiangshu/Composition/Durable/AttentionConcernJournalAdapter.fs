namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Concern
open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module AttentionConcernJournalAdapter =
    let forAttention (journal: AgentJournal) : AttentionJournalPort =
        { Read = fun () -> (AgentJournal.snapshot journal).AgentProjections.Attention
          Append =
            fun sessionId providerRun fact ->
                task {
                    let! appended =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            providerRun
                            (AgentFact.Attention fact)
                            journal

                    return
                        appended
                        |> Result.map ignore
                        |> Result.mapError (fun _ -> AttentionAppendFailure.DurabilityUnavailable)
                } }

    let forConcern (journal: AgentJournal) : ConcernJournalPort =
        { ReadState = fun sessionId -> (AgentJournal.snapshot journal).AgentProjections.Concern
          Append =
            fun sessionId providerRun fact ->
                task {
                    let! appended =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            providerRun
                            (AgentFact.Concern fact)
                            journal

                    return
                        appended
                        |> Result.map ignore
                        |> Result.mapError (fun _ -> ConcernAppendFailure.DurabilityUnavailable)
                } }

    /// concern-routing-006: a terminating participant retires every active
    /// mailbox it owns, so no later publish reaches a terminated owner. The
    /// durable append goes through the same ConcernJournalPort the tools use;
    /// per-generation retirement is idempotent (a replayed retirement finds no
    /// active mailbox and appends nothing), and mailboxes of other owners are
    /// never touched.
    let retireMailboxesOf
        (journal: AgentJournal)
        (owner: SessionId)
        (providerRun: ProviderRunIdentity option)
        : Task<Result<unit, string>> =
        task {
            let state = (AgentJournal.snapshot journal).AgentProjections.Concern
            let port = forConcern journal

            let owned =
                state.Mailboxes
                |> Map.values
                |> Seq.filter (fun mailbox -> mailbox.Active && mailbox.OwnerSessionId = owner)
                |> Seq.toList

            let rec loop (mailboxes: ConcernMailbox list) =
                task {
                    match mailboxes with
                    | [] -> return Ok()
                    | mailbox :: rest ->
                        let fact =
                            ConcernFactCases.MailboxRetired
                                {| Generation = mailbox.Generation
                                   Id = mailbox.Id
                                   OwnerSessionId = owner |}

                        match! port.Append owner providerRun fact with
                        | Ok() -> return! loop rest
                        | Error ConcernAppendFailure.DurabilityUnavailable ->
                            return Error "concern mailbox retirement durability unavailable"
                }

            return! loop owned
        }
