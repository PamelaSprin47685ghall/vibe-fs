namespace Wanxiangshu.Repository.Programming.Js

module JsAnchorFs =
    type JsGrepHit =
        { Path: string
          Line: int
          Column: int
          Text: string }

    type JsGrepListing =
        { Matches: JsGrepHit list
          ReadSnapshots: JsReadSnapshot list }

    val grep:
        root: string -> spec: AnchorSpec -> pattern: string -> Result<JsGrepListing, JsFailure>
    val findAnchor:
        text: string -> spec: AnchorSpec -> occurrence: int -> Result<(int * int), JsFailure>
    val requireUnique:
        text: string -> spec: AnchorSpec -> Result<(int * int), JsFailure>
