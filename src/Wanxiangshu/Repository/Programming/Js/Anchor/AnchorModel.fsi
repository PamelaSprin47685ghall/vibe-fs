namespace Wanxiangshu.Repository.Programming.Js.Anchor

open Wanxiangshu.Repository.Programming.Js

[<RequireQualifiedAccess>]
type AnchorCarryItem =
    | Reference of int
    | Literal of string

type AnchorViewDeclaration =
    { PrefixCount: int
      Carry: AnchorCarryItem list }

type AnchorMessage =
    { Id: string option
      Text: string
      Raw: obj }

type AnchorSegment =
    { Anchor: int
      Messages: AnchorMessage list }

type AnchorViewReport =
    { Total: int
      Used: int
      Cached: int
      Kept: int
      Inserted: int
      Dropped: int
      PrefixCount: int }

[<RequireQualifiedAccess>]
module AnchorCarry =
    val toValue: items: AnchorCarryItem list -> obj

[<RequireQualifiedAccess>]
module AnchorDeclarationRule =
    val parseCarry: value: obj -> Result<AnchorCarryItem list, JsFailure>
    val parse: value: obj -> Result<AnchorViewDeclaration, JsFailure>

[<RequireQualifiedAccess>]
module AnchorHistory =
    val append:
        numberOf: (string -> int) -> segments: AnchorSegment list -> messages: AnchorMessage list -> AnchorSegment list

    val select:
        segments: AnchorSegment list -> items: AnchorCarryItem list -> Result<(int option * string) list, JsFailure>

    val validate: segments: AnchorSegment list -> declaration: AnchorViewDeclaration -> Result<unit, JsFailure>
