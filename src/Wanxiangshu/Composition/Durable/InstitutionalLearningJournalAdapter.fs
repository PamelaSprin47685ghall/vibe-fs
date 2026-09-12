namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module InstitutionalLearningJournalAdapter =
    let forInstitutionalLearning (journal: AgentJournal) : InstitutionalLearningJournalPort =
        { ReadState = fun sessionId -> (AgentJournal.snapshot journal).AgentProjections.InstitutionalLearning
          PendingAttentionWorkPairs =
            fun sessionId ->
                AgentProjection.pendingAttentionWorkPairs sessionId (AgentJournal.snapshot journal).AgentProjections
          Append =
            fun sessionId providerRun fact ->
                task {
                    let! appended =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            providerRun
                            (AgentFact.InstitutionalLearning fact)
                            journal

                    return
                        appended
                        |> Result.map ignore
                        |> Result.mapError (fun _ -> InstitutionalLearningAppendFailure.DurabilityUnavailable)
                } }
