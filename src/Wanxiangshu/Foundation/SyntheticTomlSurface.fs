namespace Wanxiangshu.Foundation

/// JS-native semantic surface for the canonical synthetic-TOML writer
/// (ARCH-010, P6 wave). Every function takes and returns JS-native data:
/// strings, numbers, string arrays. F# list parameters become JS arrays;
/// translation happens here at the owner boundary, the writer itself stays
/// untouched (JS-SEMANTIC-SURFACE-003/005).
module SyntheticTomlSurface =

    let normalizeNewlines (text: string) : string = SyntheticToml.normalizeNewlines text

    let renderString (text: string) : string = SyntheticToml.renderString text

    let comment (text: string) : string = SyntheticToml.comment text

    let field (name: string) (renderedValue: string) : string = SyntheticToml.field name renderedValue

    let tableEntry (name: string) (fields: string array) : string =
        SyntheticToml.tableEntry name (List.ofArray fields)

    let tableArrayEntry (name: string) (fields: string array) : string =
        SyntheticToml.tableArrayEntry name (List.ofArray fields)

    let renderBool (value: bool) : string = SyntheticToml.renderBool value

    let renderInt (value: int64) : string = SyntheticToml.renderInt value

    let renderKey (name: string) : string = SyntheticToml.renderKey name

    /// `document` collides with the DOM global in JS, so Fable escapes it as
    /// `document$`; the surface names it `renderDocument` instead — surface
    /// naming is the owner's, not the compiler's.
    let renderDocument (instructions: string array) (body: string array) : string =
        SyntheticToml.document (List.ofArray instructions) (List.ofArray body)

    let byteCount (text: string) : int = SyntheticToml.byteCount text
