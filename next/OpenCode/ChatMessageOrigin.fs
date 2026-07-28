namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

/// Host strips top-level PromptInput.metadata. Recover correlation from part
/// metadata or a unique pending claim for the session.
module ChatMessageOrigin =

    let private fromMetaKey (meta: obj) =
        if isNull meta || isNull meta?wanxiangshu_prompt_key then
            None
        else
            Some(unbox<string> meta?wanxiangshu_prompt_key)

    let private fromMetaOrigin (meta: obj) =
        if isNull meta || isNull meta?wanxiangshu_origin then
            None
        else
            Some(unbox<string> meta?wanxiangshu_origin)

    let private partMetas (outputObj: obj) =
        if isNull outputObj || isNull outputObj?parts then
            []
        else
            try
                unbox<obj array> outputObj?parts
                |> Array.toList
                |> List.choose (fun part ->
                    if isNull part || isNull part?metadata then
                        None
                    else
                        Some(part?metadata))
            with _ ->
                []

    let extractPromptKey
        (inputObj: obj)
        (outputObj: obj)
        (svc: PromptAuthorityService)
        (sessionId: string)
        : string option * string option =
        let topKey =
            if isNull inputObj then
                None
            else
                fromMetaKey inputObj?metadata

        let topOrigin =
            if isNull inputObj then
                None
            else
                fromMetaOrigin inputObj?metadata

        let metas = partMetas outputObj
        let partKey = metas |> List.tryPick fromMetaKey
        let partOrigin = metas |> List.tryPick fromMetaOrigin

        let key =
            match topKey |> Option.orElse partKey with
            | Some k when not (String.IsNullOrWhiteSpace k) -> Some k
            | _ when not (String.IsNullOrWhiteSpace sessionId) ->
                let sid = SessionId.create sessionId

                let pending =
                    svc.Projection.PendingClaims
                    |> Map.toList
                    |> List.filter (fun (_, claim) -> claim.SessionId = sid)

                match pending with
                | [ (keyRef, _) ] -> Some(PromptKeyRef.value keyRef)
                | _ -> None
            | _ -> None

        let origin =
            match topOrigin |> Option.orElse partOrigin with
            | Some o -> Some o
            | None ->
                match key with
                | None -> None
                | Some k ->
                    match Map.tryFind (PromptKeyRef.create k) svc.Projection.PendingClaims with
                    | Some claim -> Some(PromptAuthority.originLabel claim.Origin)
                    | None -> None

        key, origin
