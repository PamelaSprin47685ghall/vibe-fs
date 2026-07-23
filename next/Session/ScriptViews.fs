namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal

module SessionScriptViews =

    let getTodo (gateway: IGateway) (sessionId: SessionId) () : TodoView =
        match Map.tryFind sessionId gateway.ProjectionSet.SessionProjections with
        | Some proj ->
            match proj.Todos with
            | Some snap ->
                { Unfinished = not (List.isEmpty snap.Items)
                  ProgressStamp = proj.Version }
            | None ->
                { Unfinished = false
                  ProgressStamp = proj.Version }
        | None ->
            { Unfinished = false
              ProgressStamp = 0L }

    let getReview (gateway: IGateway) (sessionId: SessionId) (config: SessionScriptConfig) () : ReviewView =
        match Map.tryFind sessionId gateway.ProjectionSet.SessionProjections with
        | Some proj ->
            let req =
                match proj.LastReview with
                | Some Fact.ReviewVerdict.Passed -> false
                | _ -> true
            { Required = req
              Round = 0
              MaxRound = config.MaxInvalidRetries
              Verdict = proj.LastReview }
        | None ->
            { Required = true
              Round = 0
              MaxRound = config.MaxInvalidRetries
              Verdict = None }

    let getProgressStamp (gateway: IGateway) (sessionId: SessionId) () : int64 =
        match Map.tryFind sessionId gateway.ProjectionSet.SessionProjections with
        | Some proj -> proj.Version
        | None -> 0L
