namespace Wanxiangshu.Context.Companion

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

/// Context-compression projection owner. Prompt wrappers, synthetic identities
/// and the provider-visible Blogger message plan cross as plain JSON only.
[<RequireQualifiedAccess>]
module CompanionProjectionSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private text (value: obj) : string =
        if isNullish value then "" else string value

    let private intValue (value: obj) : int = int (text value)

    let private shaOf (value: obj) : string -> string =
        if isNullish value then
            id
        else
            unbox<string -> string> value

    let private normalLines =
        ProviderProse.instructionLines ProviderLanguage.English CompanionPrompt.Normal Map.empty

    let private squashLines =
        ProviderProse.instructionLines ProviderLanguage.English CompanionPrompt.Squash Map.empty

    let private memoryPreambleValue =
        ProviderProse.render ProviderLanguage.English CompanionPrompt.MemoryPreamble Map.empty

    let normalInstructionLines: string array = normalLines |> List.toArray
    let squashInstructionLines: string array = squashLines |> List.toArray
    let normalInstruction: string = CompanionPrompt.asCommentedInstruction normalLines
    let squashInstruction: string = CompanionPrompt.asCommentedInstruction squashLines
    let memoryPreambleText: string = memoryPreambleValue
    let memoryPreamble: string = memoryPreambleValue
    let normal: string = "normal"

    let squash (count: int) : obj =
        box {| kind = "squash"; count = count |}

    let workingRecord (body: string) : string =
        CompanionPrompt.workingRecordMessage body

    let previousTip (fieldName: string) (cycleId: string) : string =
        CompanionPrompt.previousTipMessage fieldName cycleId

    let newWork (toml: string) : string =
        CompanionPrompt.newWorkMessage normalLines toml

    let memoryBlock (body: string) : string =
        CompanionPrompt.companionMemoryBlock memoryPreambleValue body

    let sealRoot (sha256: obj) (value: obj) : string =
        CompanionIdentity.sealRoot
            (shaOf sha256)
            (SessionId.create (text value?session))
            (PrefixEpochId.create (int64 (text value?epoch)))
            (intValue value?cutoff)
            (text value?prefixDigest)
            (BlobDigest.create (text value?frozenDigest))

    let companionMemoryMessageId (sha256: obj) (seal: string) : string =
        CompanionIdentity.companionMemoryMessageId (shaOf sha256) seal

    let frameMessageId (sha256: obj) (value: obj) : string =
        CompanionIdentity.frameMessageId
            (shaOf sha256)
            (SessionId.create (text value?blogger))
            (FrameEpochId.create (int64 (text value?epoch)))
            (intValue value?ordinal)
            (BlobDigest.create (text value?digest))

    let instructionMessageId (sha256: obj) (value: obj) : string =
        CompanionIdentity.instructionMessageId
            (shaOf sha256)
            (SessionId.create (text value?blogger))
            (FrameEpochId.create (int64 (text value?epoch)))
            (text value?kind)

    let private outputMessage (message: CompanionProjectedMessage) : obj =
        box
            {| id = message.MessageId
               role = message.Role
               text = message.Text
               physical = message.IsPhysical |}

    let private planKind (value: obj) : CompanionRequestKind =
        let kindValue = value?kind

        let kind =
            if isNullish kindValue?kind then
                text kindValue
            else
                text kindValue?kind

        let count =
            if kind = "squash" then
                let countValue =
                    if isNullish kindValue?count then
                        value?count
                    else
                        kindValue?count

                if isNullish countValue then 0 else intValue countValue
            else
                0

        match kind with
        | "squash" -> CompanionRequestKind.Squash count
        | _ -> CompanionRequestKind.Normal

    /// Build one normal or squash request from durable frame bodies and the
    /// physical delta. Squash intentionally ignores `delta` in production.
    let build (sha256: obj) (value: obj) : obj =
        let frameValues =
            if isNullish value?frames then
                [||]
            else
                unbox<obj array> value?frames

        let frameBodies =
            frameValues
            |> Array.toList
            |> List.map (fun frame -> BlobDigest.create (text frame?digest), text frame?body)

        let physicalDelta =
            if isNullish value?delta then
                None
            else
                Some(text value?delta?messageId, text value?delta?toml)

        let tipValues =
            if isNullish value?previousTips then
                [||]
            else
                unbox<obj array> value?previousTips

        let previousTips =
            tipValues
            |> Array.toList
            |> List.map (fun tip -> text tip?field, text tip?cycleId)

        let plan =
            CompanionProjectionBuilder.build
                (shaOf sha256)
                (SessionId.create (text value?blogger))
                (FrameEpochId.create (int64 (text value?epoch)))
                (planKind value)
                frameBodies
                physicalDelta
                previousTips
                normalLines
                squashLines

        let messages = plan.Messages |> List.map outputMessage |> List.toArray

        box
            {| messages = messages
               texts = plan.Messages |> List.map (fun message -> message.Text) |> List.toArray
               roles = plan.Messages |> List.map (fun message -> message.Role) |> List.toArray
               physicalFlags = plan.Messages |> List.map (fun message -> message.IsPhysical) |> List.toArray
               isFirstTurnShape = CompanionProjectionBuilder.isFirstTurnShape plan |}
