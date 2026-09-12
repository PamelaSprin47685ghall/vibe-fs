namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Host
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module RequirementGroundingJournalAdapter =
    let forRequirementGrounding (journal: AgentJournal) : RequirementGroundingPort =
        { RequirementGroundingPort.ReadState =
            fun sessionId ->
                AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
                |> Option.bind _.RequirementGrounding
                |> Option.defaultValue RequirementGroundingProjection.empty
          RequirementGroundingPort.AppendRequested =
            fun sessionId snapshot ->
                task {
                    let! res =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            None
                            (HostFact.RequirementGroundingRequested
                                {| SessionId = sessionId
                                   Snapshot = snapshot |})
                            journal

                    return res |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                }
          RequirementGroundingPort.AppendMaterialObserved =
            fun sessionId observation ->
                task {
                    let! res =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            None
                            (HostFact.RequirementGroundingMaterialObserved
                                {| SessionId = sessionId
                                   Observation = observation |})
                            journal

                    return res |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                }
          RequirementGroundingPort.AppendAnchored =
            fun sessionId occurrence ->
                task {
                    let! res =
                        AgentJournal.appendAgent
                            (StreamId.Session sessionId)
                            None
                            (HostFact.RequirementGroundingAnchored
                                {| SessionId = sessionId
                                   Occurrence = occurrence |})
                            journal

                    return res |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
                } }
