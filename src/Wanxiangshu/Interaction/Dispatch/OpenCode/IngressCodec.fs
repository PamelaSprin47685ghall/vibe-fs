namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Enforcer
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

open System
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Decodes the raw chat.message hook payload before authority policy sees it.
module PromptIngressCodec =

    type DecodedMessage = ChatAdmissionIntent.DecodedMessage

    let private readString (source: obj) (name: string) : string option =
        if isNull source || isNull (source?(name)) then
            None
        else
            let value = unbox<string> (source?(name))
            if String.IsNullOrWhiteSpace value then None else Some value

    let private agentOf (source: obj) : string option =
        if isNull source then
            None
        elif not (isNull source?agent) then
            readString source "agent"
        elif not (isNull source?info) && not (isNull source?info?agent) then
            readString source?info "agent"
        elif not (isNull source?message) then
            if not (isNull source?message?agent) then
                readString source?message "agent"
            elif not (isNull source?message?info) && not (isNull source?message?info?agent) then
                readString source?message?info "agent"
            else
                None
        elif not (isNull source?properties) then
            if not (isNull source?properties?agent) then
                readString source?properties "agent"
            elif not (isNull source?properties?info) && not (isNull source?properties?info?agent) then
                readString source?properties?info "agent"
            else
                None
        else
            None

    let private metadataOf (source: obj) (key: string) : string option =
        if isNull source || isNull source?metadata then
            None
        else
            let value = source?metadata?(key)
            if isNull value then None else Some(unbox<string> value)

    let private parts (source: obj) : obj array =
        if isNull source || isNull source?parts then
            [||]
        else
            unbox<obj array> source?parts

    let private tryBoolean (value: obj) =
        try
            Some(unbox<bool> value)
        with _ ->
            None

    let private isTrue (value: obj) =
        match tryBoolean value with
        | Some true -> true
        | _ -> false

    let private isHostCompaction (output: obj) =
        let outputParts = parts output
        let message = if isNull output then null else output?message

        outputParts
        |> Array.exists (fun part ->
            readString part "type"
            |> Option.exists (fun kind -> kind.Equals("compaction", StringComparison.OrdinalIgnoreCase)))
        || isTrue (if isNull message then null else message?summary)
        || (agentOf output
            |> Option.exists (fun agent -> agent.Equals("compaction", StringComparison.OrdinalIgnoreCase)))
        || (readString message "mode"
            |> Option.exists (fun mode -> mode.Equals("compaction", StringComparison.OrdinalIgnoreCase)))

    let private isHostSynthetic (output: obj) =
        parts output
        |> Array.exists (fun part -> isTrue (if isNull part then null else part?synthetic))

    let private sessionIdOf (input: obj) (output: obj) =
        let fromSource (source: obj) =
            if isNull source then
                None
            elif not (isNull source?sessionID) then
                Some(unbox<string> source?sessionID)
            elif not (isNull source?sessionId) then
                Some(unbox<string> source?sessionId)
            elif not (isNull source?session) then
                Some(unbox<string> source?session)
            else
                None

        let message = if isNull output then null else output?message
        let info = if isNull output then null else output?info

        [ fromSource input; fromSource output; fromSource message; fromSource info ]
        |> List.tryPick id
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.map (fun value -> SessionId.create (value.Trim()))

    let private messageIdOf (input: obj) (output: obj) =
        let message = if isNull output then null else output?message

        [ readString input "messageID"; readString message "id" ]
        |> List.choose id
        |> List.distinct
        |> function
            | [ physical ] -> Some(PhysicalUserMessageId.create physical)
            | _ -> None

    /// PROMPT-011: read the anchor back from the field PromptMetadataCodec wrote.
    ///
    /// The field name comes from that module rather than being spelled again here.
    /// Two independent literals for one wire field is how a rename silently breaks
    /// recovery: the write side moves, the read side keeps matching nothing, and
    /// every plugin prompt starts looking like UnknownOrigin.
    let private promptKeyOf (input: obj) (output: obj) =
        match metadataOf input PromptMetadataCodec.PromptKeyField with
        | Some key when not (String.IsNullOrWhiteSpace key) -> Some(PromptKey.create key)
        | _ ->
            parts output
            |> Array.tryPick (fun part -> metadataOf part PromptMetadataCodec.PromptKeyField)
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.map PromptKey.create

    /// Classify one physical message part; synthetic and non-text parts are not
    /// opening content.
    let private textOfPart (part: obj) : string option =
        match isTrue (if isNull part then null else part?synthetic), readString part "type" with
        | true, _ -> None
        | false, Some kind when kind.Equals("text", StringComparison.OrdinalIgnoreCase) -> readString part "text"
        | _ -> None

    /// COMPANION-003: the user message's text, for the opening capture. Only the
    /// physical user message's own text parts count — the opening is the first
    /// task prompt, and a synthetic part (tool result, metadata, guidance) is not it.
    let private textOf (output: obj) : string option =
        parts output
        |> Array.choose textOfPart
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.toList
        |> function
            | [] -> None
            | texts -> Some(String.concat "\n" texts)

    let decode (input: obj) (output: obj) : DecodedMessage =
        let message = if isNull output then null else output?message
        let info = if isNull output then null else output?info
        let properties = if isNull output then null else output?properties

        let explicitAgent =
            [ agentOf input
              agentOf output
              agentOf message
              agentOf info
              agentOf properties ]
            |> List.tryPick id
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.map (fun v -> v.Trim())
            |> Option.orElseWith (fun () ->
                sessionIdOf input output
                |> Option.bind SessionExecutionBinding.tryAgent)

        { SessionId = sessionIdOf input output
          PhysicalUserMessageId = messageIdOf input output
          ExplicitAgent = explicitAgent
          PromptKey = promptKeyOf input output
          IsHostCompaction = isHostCompaction output
          IsHostSynthetic = isHostSynthetic output
          Text = textOf output }
