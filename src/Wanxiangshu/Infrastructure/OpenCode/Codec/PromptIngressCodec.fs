namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Decodes the raw chat.message hook payload before authority policy sees it.
module PromptIngressCodec =

    type DecodedMessage =
        {
            SessionId: SessionId option
            /// chat.message delivers a real physical user message. Typing it as such
            /// is what stops it being used where a transport receipt would also fit.
            PhysicalUserMessageId: PhysicalUserMessageId option
            ExplicitAgent: string option
            PromptKey: PromptKey option
            IsHostCompaction: bool
            /// COMPANION-003/014: the message's text, for OpeningMaterial capture
            /// at the physical acceptance point. User text parts only.
            Text: string option
        }

    let private agentOf (source: obj) : string option =
        if isNull source then
            None
        elif not (isNull source?agent) then
            Some(unbox<string> source?agent)
        elif not (isNull source?message) && not (isNull source?message?agent) then
            Some(unbox<string> source?message?agent)
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

    let private readString (source: obj) (name: string) : string option =
        if isNull source || isNull (source?(name)) then
            None
        else
            let value = unbox<string> (source?(name))
            if String.IsNullOrWhiteSpace value then None else Some value

    let private isTrue (value: obj) =
        if isNull value then
            false
        else
            try
                unbox<bool> value = true
            with _ ->
                false

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

    let private sessionIdOf (input: obj) =
        if isNull input then
            None
        elif not (isNull input?session) then
            Some(SessionId.create (unbox<string> input?session))
        elif not (isNull input?sessionID) then
            Some(SessionId.create (unbox<string> input?sessionID))
        elif not (isNull input?sessionId) then
            Some(SessionId.create (unbox<string> input?sessionId))
        else
            None

    let private messageIdOf (input: obj) (output: obj) =
        let physical (value: string) =
            Some(PhysicalUserMessageId.create value)

        if not (isNull input) && not (isNull input?messageID) then
            physical (unbox<string> input?messageID)
        elif not (isNull input) && not (isNull input?messageId) then
            physical (unbox<string> input?messageId)
        elif not (isNull output) && not (isNull output?id) then
            physical (unbox<string> output?id)
        elif
            not (isNull output)
            && not (isNull output?message)
            && not (isNull output?message?id)
        then
            physical (unbox<string> output?message?id)
        elif not (isNull output) && not (isNull output?info) && not (isNull output?info?id) then
            physical (unbox<string> output?info?id)
        else
            None

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

    /// COMPANION-003: the user message's text, for the opening capture. Only the
    /// physical user message's own text parts count — the opening is the first
    /// task prompt, and a synthetic part (tool result, metadata, guidance) is not it.
    let private textOf (output: obj) : string option =
        parts output
        |> Array.choose (fun part ->
            if isTrue (if isNull part then null else part?synthetic) then
                None
            else
                match readString part "type" with
                | Some kind when kind.Equals("text", StringComparison.OrdinalIgnoreCase) -> readString part "text"
                | _ -> None)
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.toList
        |> function
            | [] -> None
            | texts -> Some(String.concat "\n" texts)

    let decode (input: obj) (output: obj) : DecodedMessage =
        { SessionId = sessionIdOf input
          PhysicalUserMessageId = messageIdOf input output
          ExplicitAgent = [ input; output ] |> List.tryPick agentOf
          PromptKey = promptKeyOf input output
          IsHostCompaction = isHostCompaction output
          Text = textOf output }
