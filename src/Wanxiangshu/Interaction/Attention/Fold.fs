namespace Wanxiangshu.Interaction.Attention

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Enforcer.InstitutionalLearning

[<RequireQualifiedAccess>]
module AttentionFactFold =

    let fold projection fact =
        match fact with
        | AttentionFactCases.DeferredWorkRecorded payload ->
            Ok
                { projection with
                    Attention =
                        projection.Attention
                        |> AttentionProjection.record payload.SessionId payload.OccurrenceId payload.Text }

    let foldLearning projection fact =
        match fact with
        | InstitutionalLearningFactCases.LearningDispositionCommitted payload ->
            Ok
                { projection with
                    Attention =
                        projection.Attention
                        |> AttentionProjection.resurface
                            payload.SessionId
                            payload.OccurrenceId
                            payload.ResurfacedDeferredWorkIds }

