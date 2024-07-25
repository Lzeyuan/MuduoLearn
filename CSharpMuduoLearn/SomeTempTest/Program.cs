using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace scl;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var receiveOption = new Option<bool>(
            aliases: ["--recv", "-r"],
            description: "Receive.");

        var portOption = new Option<int>(
            aliases: ["--port", "-p"],
            getDefaultValue: () => 5001,
            description: "Set port. Default to 5001."
        );

        var transmitOption = new Option<string>(
            aliases: ["--trans", "-t"],
            description: "Transmit.");

        var startCommand = new Command("start", "Start test.")
        {
            receiveOption,
            portOption,
            transmitOption
        };

        var rootCommand = new RootCommand("TTcp command line.");
        rootCommand.AddCommand(startCommand);

        startCommand.SetHandler(StartTest, receiveOption, portOption, transmitOption);
        return await rootCommand.InvokeAsync(args);
    }

    static async Task StartTest(bool recv, int port, string trans)
    {
        if (recv == !string.IsNullOrEmpty(trans))
        {
            Console.WriteLine("Either -t or -r must be specified.\n");
            return;
        }

        Console.WriteLine($"{recv} {port} {trans}");
        await Task.Delay(11);
    }
}