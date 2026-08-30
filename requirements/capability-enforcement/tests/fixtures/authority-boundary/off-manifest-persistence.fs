namespace Unrelated.Persistence

module Snapshot =
    let encodeCapability (permit: OneShotCapability) = Json.serialize permit
