function xResultsBrowser() {
    
}

/**
* Use this method to register to events of the browser. You can define arbitrary number
* of event handlers for any event. You can remove handler by calling {@link xBrowser#onRemove onRemove()} method.
*
* @function xResultsBrowser#on
* @param {String} eventName - Name of the event you would like to listen to.
* @param {Object} callback - Callback handler of the event which will consume arguments and perform any custom action.
*/
xResultsBrowser.prototype.on = function (eventName, callback) {
    if (typeof (this._events) === "undefined") this._events = [];
    var events = this._events;
    if (!events[eventName]) {
        events[eventName] = [];
    }
    events[eventName].push(callback);
};

/**
* Use this method to unregisted handlers from events. You can add event handlers by call to {@link xBrowser#on on()} method.
*
* @function xResultsBrowser#onRemove
* @param {String} eventName - Name of the event
* @param {Object} callback - Handler to be removed
*/
xResultsBrowser.prototype.onRemove = function (eventName, callback) {
    if (typeof (this._events) === "undefined") this._events = [];
    var events = this._events;
    var callbacks = events[eventName];
    if (!callbacks) {
        return;
    }
    var index = callbacks.indexOf(callback);
    if (index >= 0) {
        callbacks.splice(index, 1);
    }
};

//executes all handlers bound to event name
xResultsBrowser.prototype._fire = function (eventName, args) {
    if (typeof (this._events) === "undefined") this._events = [];
    var handlers = this._events[eventName];
    if (!handlers) {
        return;
    }
    //call the callbacks
    for (var i in handlers) {
        if (handlers.hasOwnProperty(i)) {
            handlers[i](args);
        }
    }
};