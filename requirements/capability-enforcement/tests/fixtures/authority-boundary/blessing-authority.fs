namespace Fixture

type BlessingPermit =
    private | BlessingPermit of {| Subject: string |}

module BlessingAdmission =
    let grant payload = BlessingPermit payload
