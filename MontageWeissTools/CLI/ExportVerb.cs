﻿using CommandLine;
using Lamar;
using Montage.Weiss.Tools.API;
using Montage.Weiss.Tools.Entities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Montage.Weiss.Tools.CLI
{
    [Verb("export", HelpText = "Exports a file from one format to another, typically into files for Tabletop Simulator, for example.")]
    public class ExportVerb : IVerbCommand, IExportInfo
    {
        [Value(0, HelpText = "Indicates the source file/url.")]
        public string Source { get; set;  }

        [Value(1, HelpText = "Indicates the destination; usually a folder.", Default = "./Export/")]
        public string Destination { get; set; } = "./Export/";

        [Option("parser", HelpText = "Manually sets the deck parser to use. Possible values: encoredecks", Default = "encoredecks")]
        public string Parser { get; set; } = "encoredecks";

        [Option("exporter", HelpText = "Manually sets the deck exporter to use. Possible values: tabletopsim", Default = "tabletopsim")]
        public string Exporter { get; set; } = "tabletopsim";

        [Option("out", HelpText = "For some exporters, gives an out command to execute after exporting.", Default = "")]
        public string OutCommand { get; set; } = "";

        [Option("with", HelpText = "For some exporters, enables various flags. See each exporter for details.", Separator = ',', Default = new string[] { })]
        public IEnumerable<string> Flags { get; set; } = new string[] { };

        [Option("noninteractive", HelpText = "When set to true, there will be no prompts. Default options will be used.", Default = false)]
        public bool NonInteractive { get; set; } = false;

        private readonly ILogger Log = Serilog.Log.ForContext<ExportVerb>();

        private static readonly IEnumerable<string> Empty = new string[] { };

        /// <summary>
        /// For the IOC
        /// </summary>
        public ExportVerb()
        { 
        }

        public async Task Run(IContainer ioc)
        {
            Log.Information("Running...");

            var parser = ioc.GetAllInstances<IDeckParser>()
                .Where(parser => parser.IsCompatible(Source))
                .OrderByDescending(parser => parser.Priority)
                //                .Where(parser => parser.Alias.Contains(Parser) && parser.IsCompatible(Source))
                .First();

            var deck = await parser.Parse(Source);

            deck = await ioc.GetAllInstances<IExportedDeckInspector>()
                .OrderByDescending(inspector => inspector.Priority)
                .ToAsyncEnumerable()
                .AggregateAwaitAsync(deck, async (d, inspector) => await inspector.Inspect(d, NonInteractive));

            if (deck != WeissSchwarzDeck.Empty)
            {
                var exporter = ioc.GetAllInstances<IDeckExporter>()
                    .Where(exporter => exporter.Alias.Contains(Exporter))
                    .First();

                await exporter.Export(deck, this);
            }
        }
    }
}
