namespace Wanxiangshu.Next.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

/// Decodes the raw chat.message hook payload before authority policy sees it.
module PromptIngressCodec =

    type DecodedMessage =
        { SessionId: SessionId option
          MessageId: MessageId option
          ExplicitAgent: string option
          PromptKey: PromptKeyRef option
          IsHostCompaction: bool }

    let private agentOf (source: obj) : string option =
        if isNull source then None
        elif not (isNull source?agent) then Some(unbox<string> source?agent)
        elif not (isNull source?message) && not (isNull source?message?agent) then
            Some(unbox<string> source?message?agent)
        else
            None

    let private metadataOf (source: obj) (key: string) : string option =
        if isNull source || isNull source?metadata then None
        else
            let value = source?metadata?(key)
            if isNull value then None else Some(unbox<string> value)

    let private parts (source: obj) : obj array =
        if isNull source || isNull source?parts then [||] else unbox<obj array> source?parts

    let private isSynthetic (part: obj) = not (isNull part) && (unbox<bool> part?synthetic)

    let private isHostCompaction (output: obj) =
        let outputParts = parts output

        outputParts
        |> Array.exists (fun part ->
            (not (isNull part) && not (isNull part?``type``) && unbox<string> part?``type`` = "compaction")
            || isSynthetic part)
        || (outputParts.Length > 0 && outputParts |> Array.forall isSynthetic)
        || (not (isNull output)
            && not (isNull output?message)
            && not (isNull output?message?summary)
            && unbox<bool> output?message?summary)
        || (agentOf output |> Option.exists (fun agent -> agent.Equals("compaction", StringComparison.OrdinalIgnoreCase)))

    let private sessionIdOf (input: obj) =
        if isNull input then None
        elif not (isNull input?session) then Some(SessionId.create (unbox<string> input?session))
        elif not (isNull input?sessionID) then Some(SessionId.create (unbox<string> input?sessionID))
        elif not (isNull input?sessionId) then Some(SessionId.create (unbox<string> input?sessionId))
        else None

    let private messageIdOf (input: obj) (output: obj) =
        if not (isNull input) && not (isNull input?messageID) then Some(MessageId.create (unbox<string> input?messageID))
        elif not (isNull input) && not (isNull input?messageId) then Some(MessageId.create (unbox<string> input?messageId))
        elif not (isNull output) && not (isNull output?id) then Some(MessageId.create (unbox<string> output?id))
        elif not (isNull output) && not (isNull output?message) && not (isNull output?message?id) then
            Some(MessageId.create (unbox<string> output?message?id))
        elif not (isNull output) && not (isNull output?info) && not (isNull output?info?id) then
            Some(MessageId.create (unbox<string> output?info?id))
        else None

    let private promptKeyOf (input: obj) (output: obj) =
        match metadataOf input "wanxiangshu_prompt_key" with
        | Some key when not (String.IsNullOrWhiteSpace key) -> Some(PromptKeyRef.create key)
        | _ ->
            parts output
            |> Array.tryPick (fun part -> metadataOf part "wanxiangshu_prompt_key")
            |> Option.filter (String.IsNullOrWhiteSpace >> not)
            |> Option.map PromptKeyRef.create

    let decode (input: obj) (output: obj) : DecodedMessage =
        { SessionId = sessionIdOf input
          MessageId = messageIdOf input output
          ExplicitAgent = [ input; output ] |> List.tryPick agentOf
          PromptKey = promptKeyOf input output
          IsHostCompaction = isHostCompaction output }
