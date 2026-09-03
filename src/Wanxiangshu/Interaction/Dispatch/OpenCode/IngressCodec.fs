namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open Wanxiangshu.OpenCode
open System
open Fable.Core.JsInterop
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

    let private childObject (source: obj) (name: string) : obj =
        if isNull source then null else source?(name)

    let private agentOf (source: obj) : string option =
        let message = childObject source "message"
        let properties = childObject source "properties"
        let session = childObject source "session"
        let body = childObject source "body"
        let options = childObject source "options"

        [ source
          childObject source "info"
          message
          childObject message "info"
          properties
          childObject properties "info"
          session
          childObject session "info"
          body
          childObject body "info"
          options
          childObject options "info" ]
        |> List.tryPick (fun candidate -> readString candidate "agent")

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

    let private sessionIdOfPart (sess: obj) : string option =
        if isNull sess then
            None
        elif not (isNull sess?id) then
            Some(string sess?id)
        elif not (isNull sess?sessionID) then
            Some(string sess?sessionID)
        elif not (isNull sess?sessionId) then
            Some(string sess?sessionId)
        else
            let s = string sess
            if s.StartsWith("ses_") then Some s else None

    let private sessionIdOf (input: obj) (output: obj) =
        let fromSource (source: obj) =
            if isNull source then
                None
            elif not (isNull source?sessionID) then
                Some(string source?sessionID)
            elif not (isNull source?sessionId) then
                Some(string source?sessionId)
            elif not (isNull source?session) then
                sessionIdOfPart source?session
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
            |> Option.orElseWith (fun () -> sessionIdOf input output |> Option.bind SessionExecutionBinding.tryAgent)

        { SessionId = sessionIdOf input output
          PhysicalUserMessageId = messageIdOf input output
          ExplicitAgent = explicitAgent
          PromptKey = promptKeyOf input output
          IsHostCompaction = isHostCompaction output
          IsHostSynthetic = isHostSynthetic output
          Text = textOf output }
