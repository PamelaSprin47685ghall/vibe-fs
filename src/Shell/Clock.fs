module Wanxiangshu.Shell.Clock

let getTimestampMs () : int64 =
    System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

let nowUtc () : System.DateTime = System.DateTime.UtcNow
