namespace Wanxiangshu.Execution.Delegation.Fork

open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

/// JS-native semantic surface for fork child payload (P3 pilot).
///
/// Input is JSON-shaped: the anonymous record compiles to a plain JS object
/// (Assignment string, optional strings, RootRequirements string array), so a
/// JS test constructs ordinary data and never touches Fable representation.
/// Translation JS representation → F# types happens here, at the owner
/// boundary (JS-SEMANTIC-SURFACE-003/005); the F# core keeps its records.
module ForkChildPayloadSurface =

    let private languageOf (lang: string) : ProviderLanguage =
        ProviderLanguage.tryParse lang
        |> Option.defaultValue ProviderLanguage.English

    let private proseOf (lang: string) : ForkChildInstructions =
        let l = languageOf lang

        { Base = ProviderProse.instructionLines l ForkChildPayload.BasePath Map.empty
          CommissionerRecord = ProviderProse.render l ForkChildPayload.CommissionerRecordPath Map.empty
          Attachment = ProviderProse.render l ForkChildPayload.AttachmentPath Map.empty
          Requirements = ProviderProse.render l ForkChildPayload.RequirementsPath Map.empty }

    /// The four localized instruction fragments (JSON-shaped). Base keeps
    /// blank lines as "" so SyntheticToml.document preserves paragraph breaks.
    let instructions (lang: string) : {| Base: string array
                                         CommissionerRecord: string
                                         Attachment: string
                                         Requirements: string |} =
        let p = proseOf lang

        {| Base = List.toArray p.Base
           CommissionerRecord = p.CommissionerRecord
           Attachment = p.Attachment
           Requirements = p.Requirements |}

    /// Render one fork child payload document from JSON-shaped input.
    /// Absent fields are `undefined` in JS and decode as F# `None` / `[]`.
    let render
        (lang: string)
        (input: {| Assignment: string
                   CommissionerRecord: string option
                   Attachment: string option
                   RootRequirements: string array
                   Payload: string option |})
        : string =
        let prose = proseOf lang
        let assignment = if isNull input.Assignment then "" else input.Assignment

        let requirements =
            if isNull input.RootRequirements then
                []
            else
                List.ofArray input.RootRequirements

        ForkChildPayload.render
            prose
            { Assignment = assignment
              CommissionerRecord = input.CommissionerRecord
              Attachment = input.Attachment
              RootRequirements = requirements
              Payload = input.Payload }
