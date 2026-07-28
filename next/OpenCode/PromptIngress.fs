namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

[<RequireQualifiedAccess>]
module PromptIngress =

    let private agentOf (source: obj) : string option =
        if isNull source then
            None
        else
            let top =
                if isNull source?agent then
                    None
                else
                    Some(unbox<string> source?agent)

            let nested =
                if isNull source?message || isNull source?message?agent then
                    None
                else
                    Some(unbox<string> source?message?agent)

            top |> Option.orElse nested

    let private metadataOf (source: obj) (key: string) : string option =
        if isNull source || isNull source?metadata then
            None
        else
            let v = source?metadata?(key)
            if isNull v then None else Some(unbox<string> v)

    let private parts (source: obj) : obj array =
        if isNull source || isNull source?parts then
            [||]
        else
            unbox<obj array> source?parts

    let private partType (part: obj) : string option =
        if isNull part then
            None
        else
            let v = part?``type``
            if isNull v then None else Some(unbox<string> v)

    let private isSynthetic (part: obj) : bool =
        not (isNull part) && (unbox<bool> part?synthetic)

    let private isHostCompaction (inputObj: obj) (outputObj: obj) : bool =
        let outputParts = parts outputObj

        outputParts
        |> Array.exists (fun p ->
            match partType p with
            | Some "compaction" -> true
            | _ -> isSynthetic p)
        || (outputParts.Length > 0 && outputParts |> Array.forall isSynthetic)
        || (not (isNull outputObj)
            && not (isNull outputObj?message)
            && (unbox<bool> outputObj?message?summary))
        || (agentOf outputObj
            |> Option.exists (fun a -> a.ToLowerInvariant() = "compaction"))

    let private explicitAgent (inputObj: obj) (outputObj: obj) : string option =
        [ inputObj; outputObj ] |> List.tryPick agentOf

    let private isValidAgent (value: string) =
        match PromptAuthority.parseAgentName value with
        | Ok _ -> true
        | Error _ -> false

    let private extractPromptKey (inputObj: obj) (outputObj: obj) : string option =
        match metadataOf inputObj "wanxiangshu_prompt_key" with
        | Some k when not (String.IsNullOrWhiteSpace k) -> Some k
        | _ ->
            parts outputObj
            |> Array.tryPick (fun p -> metadataOf p "wanxiangshu_prompt_key")
            |> Option.filter (String.IsNullOrWhiteSpace >> not)

    let private extractOrigin (inputObj: obj) (outputObj: obj) : string option =
        match metadataOf inputObj "wanxiangshu_origin" with
        | Some o when not (String.IsNullOrWhiteSpace o) -> Some o
        | _ ->
            parts outputObj
            |> Array.tryPick (fun p -> metadataOf p "wanxiangshu_origin")
            |> Option.filter (String.IsNullOrWhiteSpace >> not)

    /// chat.message handler. Classifies origin and accepts only HumanRoot,
    /// AgentOwnerRoot, or Continuation. Unknown/fail-closed is ignored.
    let createHook
        (journal: AgentJournal option)
        (bindUserMessage: string -> string -> unit)
        (bindContinuationMessage: string -> string -> unit)
        (registerOwned: string -> unit)
        (onAuthorityResolved: SessionId -> PromptAuthority.AuthorityExecutionProfile -> unit)
        =
        fun (inputObj: obj) (outputObj: obj) ->
            let sessionId =
                if isNull inputObj then
                    None
                elif not (isNull inputObj?session) then
                    Some(unbox<string> inputObj?session)
                elif not (isNull inputObj?sessionID) then
                    Some(unbox<string> inputObj?sessionID)
                elif not (isNull inputObj?sessionId) then
                    Some(unbox<string> inputObj?sessionId)
                else
                    None

            let messageId =
                if not (isNull inputObj) && not (isNull inputObj?messageID) then
                    Some(unbox<string> inputObj?messageID)
                elif not (isNull inputObj) && not (isNull inputObj?messageId) then
                    Some(unbox<string> inputObj?messageId)
                elif isNull outputObj then
                    None
                elif not (isNull outputObj?id) then
                    Some(unbox<string> outputObj?id)
                elif not (isNull outputObj?message) && not (isNull outputObj?message?id) then
                    Some(unbox<string> outputObj?message?id)
                elif not (isNull outputObj?info) && not (isNull outputObj?info?id) then
                    Some(unbox<string> outputObj?info?id)
                else
                    None

            match sessionId, messageId with
            | Some sid, Some mid ->
                let sessionIdRef = SessionId.create sid
                let messageIdRef = MessageId.create mid

                let runtime =
                    match journal with
                    | Some j -> PromptDispatcher.forJournal j
                    | None -> PromptDispatcher.ephemeral ()

                let promptKey =
                    extractPromptKey inputObj outputObj |> Option.map PromptKeyRef.create

                let hostCompaction = isHostCompaction inputObj outputObj
                let explicit = explicitAgent inputObj outputObj

                let knownOrigin = runtime.ResolveOrigin messageIdRef promptKey hostCompaction

                let finalOrigin =
                    match knownOrigin with
                    | PromptAuthority.PromptOrigin.UnknownOrigin ->
                        match explicit with
                        | Some agent when isValidAgent agent ->
                            PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot
                        | _ -> PromptAuthority.PromptOrigin.UnknownOrigin
                    | other -> other

                match finalOrigin with
                | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.HumanRoot ->
                    match explicit with
                    | None -> ()
                    | Some agent ->
                        match runtime.AcceptHumanRoot sessionIdRef messageIdRef (Some agent) with
                        | Ok profile ->
                            onAuthorityResolved sessionIdRef profile
                            bindUserMessage sid mid
                            registerOwned sid
                        | Error _ -> ()
                | PromptAuthority.PromptOrigin.AuthorityRoot PromptAuthority.RootAuthorityKind.AgentOwnerRoot ->
                    match promptKey with
                    | None -> ()
                    | Some key ->
                        match runtime.AcceptAgentOwnerRoot (PromptKeyRef.value key) sessionIdRef messageIdRef with
                        | Ok profile ->
                            onAuthorityResolved sessionIdRef profile
                            bindUserMessage sid mid
                            registerOwned sid
                        | Error _ -> ()
                | PromptAuthority.PromptOrigin.Continuation _ ->
                    match promptKey with
                    | None -> ()
                    | Some key ->
                        match runtime.AcceptContinuation (PromptKeyRef.value key) sessionIdRef messageIdRef with
                        | Ok _ ->
                            bindContinuationMessage sid mid
                            registerOwned sid
                        | Error _ -> ()
                | PromptAuthority.PromptOrigin.HostInternal ->
                    // Host-owned compaction/continuation: no authority change.
                    ()
                | PromptAuthority.PromptOrigin.UnknownOrigin -> ()
            | _ -> ()
