namespace Fixture

// DSL-AUTHORITY: Capability
type OneShotRecordCapability = private {
    Owner: obj
    Subject: string
    Version: int64
}

let issueRecord owner subject version =
    { Owner = owner; Subject = subject; Version = version }

let foreignMint owner subject version : OneShotRecordCapability =
    { Owner = owner; Subject = subject; Version = version }
