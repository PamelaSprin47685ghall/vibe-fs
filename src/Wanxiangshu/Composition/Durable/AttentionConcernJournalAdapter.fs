namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Concern
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
