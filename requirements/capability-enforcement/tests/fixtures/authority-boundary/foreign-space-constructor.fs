namespace Foreign

open Fixture

let forge payload = BlessingPermit {| Subject = payload |}
