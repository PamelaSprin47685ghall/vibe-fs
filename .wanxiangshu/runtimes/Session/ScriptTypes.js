
import { Record } from "../fable_modules/fable-library-js.5.6.0/Types.js";
import { list_type, string_type, option_type, int32_type, record_type, int64_type, bool_type } from "../fable_modules/fable-library-js.5.6.0/Reflection.js";
import { ReviewVerdict_$reflection } from "../Kernel/Fact.js";

export class TodoView extends Record {
    constructor(Unfinished, ProgressStamp) {
        super();
        this.Unfinished = Unfinished;
        this.ProgressStamp = ProgressStamp;
    }
}

export function TodoView_$reflection() {
    return record_type("Wanxiangshu.Next.Session.TodoView", [], TodoView, () => [["Unfinished", bool_type], ["ProgressStamp", int64_type]]);
}

export class ReviewView extends Record {
    constructor(Required, Round, MaxRound, Verdict) {
        super();
        this.Required = Required;
        this.Round = (Round | 0);
        this.MaxRound = (MaxRound | 0);
        this.Verdict = Verdict;
    }
}

export function ReviewView_$reflection() {
    return record_type("Wanxiangshu.Next.Session.ReviewView", [], ReviewView, () => [["Required", bool_type], ["Round", int32_type], ["MaxRound", int32_type], ["Verdict", option_type(ReviewVerdict_$reflection())]]);
}

export class SessionScriptConfig extends Record {
    constructor(FallbackModels, MaxRetriesPerModel, MaxInvalidRetries) {
        super();
        this.FallbackModels = FallbackModels;
        this.MaxRetriesPerModel = (MaxRetriesPerModel | 0);
        this.MaxInvalidRetries = (MaxInvalidRetries | 0);
    }
}

export function SessionScriptConfig_$reflection() {
    return record_type("Wanxiangshu.Next.Session.SessionScriptConfig", [], SessionScriptConfig, () => [["FallbackModels", list_type(string_type)], ["MaxRetriesPerModel", int32_type], ["MaxInvalidRetries", int32_type]]);
}

