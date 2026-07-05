using Royale.Content;
using Royale.Protocol;
using Royale.Server;
using Royale.Simulation;

var descriptor = ServerDescriptor.Create();

Console.WriteLine(
    $"Royale server skeleton ready. Protocol {ProtocolConstants.Version}, map {ContentCatalog.DefaultMapId}, tick {SimulationSettings.TickRateHz} Hz, headless {descriptor.IsHeadless}.");
