
import { tryFind } from "../fable_modules/fable-library-js.5.6.0/Map.js";
import { ReviewView, TodoView } from "./ScriptTypes.js";
import { isEmpty } from "../fable_modules/fable-library-js.5.6.0/List.js";

export function getTodo(gateway, sessionId, unitVar) {
    const matchValue = tryFind(sessionId, gateway.ProjectionSet.SessionProjections);
    if (matchValue == null) {
        return new TodoView(false, 0n);
    }
    else {
        const proj = matchValue;
        const matchValue_1 = proj.Todos;
        if (matchValue_1 == null) {
            return new TodoView(false, proj.Version);
        }
        else {
            return new TodoView(!isEmpty(matchValue_1.Items), proj.Version);
        }
    }
}

export function getReview(gateway, sessionId, config, unitVar) {
    let matchValue_1;
    const matchValue = tryFind(sessionId, gateway.ProjectionSet.SessionProjections);
    if (matchValue == null) {
        return new ReviewView(true, 0, config.MaxInvalidRetries, undefined);
    }
    else {
        const proj = matchValue;
        return new ReviewView((matchValue_1 = proj.LastReview, (matchValue_1 != null) ? (!(matchValue_1.tag === 0)) : true), 0, config.MaxInvalidRetries, proj.LastReview);
    }
}

export function getProgressStamp(gateway, sessionId, unitVar) {
    const matchValue = tryFind(sessionId, gateway.ProjectionSet.SessionProjections);
    if (matchValue == null) {
        return 0n;
    }
    else {
        return matchValue.Version;
    }
}

