const Default = REST.Default;
const End = REST.End;
const Restart = REST.Restart;

class EventContext {
  /** @type {number} */ nextSkipId = 0;
  /** @type {{[id: number | string]: number[]}} */ skips = {};
  /** @type {number[]} */ labels = [];
  /** @type {EVENT} */ event;
  /** @type {number} */ orIndex = 1;
  /** @type {number} */ andIndex = 1;

  /** @param {EVENT} event  */
  constructor(event) {
    this.event = event;
  }
}
/** @type {EventContext[]} */
let _eventStack = [];
function _Event() {
  let _event = _eventStack.at(-1);
  if (!_event) throw new Error("Not in event");
  return _event;
}

/** @type {(id: number, restBehavior: number, instructions: (...args: any[]) => void) => EVENT} */
function CreateEvent(id, restBehavior, instructions) {
  let evt = new EVENT();
  evt.ID = id;
  evt.RestBehavior = restBehavior;
  _eventStack.push(new EventContext(evt));

  instructions();

  let skips = _Event().skips;
  if (Object.keys(skips).length > 0) {
    let unfilledSkips = JSON.stringify(skips);
    throw new Error(
      `Reserved skips in Event ${id} have not been filled. Unfilled skips: ${unfilledSkips}`
    );
  }
  _eventStack.pop();

  EVD.Events.Add(evt);
  return evt;
}

/** @type {(id: number, restBehavior: number, instructions: (...args: any[]) => void) => EVENT} */
function Event(id, restBehavior, body) {
  var bodyWithArgs = () => body(..._GetArgs(body));
  return CreateEvent(id, restBehavior, bodyWithArgs);
}

/** @type {(func: (...any: any[]) => any) => string[]} */
function _GetArgs(func) {
  var start = func.toString().indexOf("(");
  var end = func.toString().indexOf(")");
  var args = func.toString().substring(start + 1, end);
  return args.split(/\s*,\s*/).map((arg) => arg);
}

/** @type {(bank: number, index: number, args: any[]) => unknown} */
function _Instruction(bank, index, args) {
  if (_Event()) {
    let layer = undefined;
    if (args.length) {
      var lastArg = args.pop();
      if (lastArg.layerValue) {
        layer = lastArg.layerValue;
      } else {
        args.push(lastArg);
      }
    }

    if (layer) {
      return Scripter.MakeInstruction(
        _Event().event,
        bank,
        index,
        layer,
        hostArray(args)
      );
    } else {
      return Scripter.MakeInstruction(
        _Event().event,
        bank,
        index,
        hostArray(args)
      );
    }
  }
}

/** @type {() => number} */
function _ReserveNewSkip() {
  let id = _Event().nextSkipId++;
  _ReserveSkip(id);
  return id;
}
/** @type {(id: string | number) => void} */
function _ReserveSkip(id) {
  id = id ?? _Event().nextSkipId++;
  if (!_Event().skips[id]) _Event().skips[id] = [];
  _Event().skips[id].push(_Event().event.Instructions.Count);
}

/** @type {(id: string | number) => void} */
function _FillSkip(id) {
  var skips = _Event().skips;
  for (let index of skips[id] ?? []) {
    Scripter.FillSkipPlaceholder(_Event().event, index);
  }

  delete _Event().skips[id];
}

/** @type {(args: any[]) => any[]} */
function hostArray(args) {
  var argOut = $$$_host.newArr(args.length);
  for (var i = 0; i < args.length; i++) {
    argOut[i] = args[i];
  }
  return argOut;
}

// function $LAYERS(...args) {
//   var layer = 0;
//   for (var i = 0; i < args.length; i++) layer |= 1 << args[i];
//   return { layerValue: layer };
// }

/** @type {(num: number) => number} */
function floatArg(num) {
  return Scripter.ConvertFloatToIntBytes(num);
}

/** @type {(...nums: number[]) => number} */
function bytesArg(...nums) {
  return nums[0] + (nums[1] << 8) + (nums[2] << 16) + (nums[3] << 24);
}

/** @type {EventC[]} */
var eventCs = [];

class EventC {
  /**
   * @param {number} id
   * @param {number} restBehavior
   * @param {(...args: number[]) => void} body
   */
  constructor(id, restBehavior, body) {
    this.id = id;
    this.behavior = restBehavior;
    this.body = body;
    eventCs.push(this);
  }

  Event() {
    CreateEvent(this.id, this.behavior, this.body);
  }

  /**
   * @param {number} slot
   * @param {number[]} args;
   */
  Initialize(slot, ...args) {
    InitializeEvent(slot, this.id, ...args);
  }
}

function LoadAllEvents() {
  for (let eventC of eventCs) {
    eventC.Event();
  }
}
