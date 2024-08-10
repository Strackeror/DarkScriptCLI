// See https://aka.ms/new-console-template for more information
using DarkScript3;

var options = new EventCFG.CFGOptions();
var docs = new InstructionDocs("er-common.emedf.json");
var eventScripter = new FancyEventScripter(
    new EventScripter(Environment.GetCommandLineArgs()[1], docs),
    docs,
    options
);

Console.WriteLine(eventScripter.Unpack());
