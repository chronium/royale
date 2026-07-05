using Royale.Client.Platform;

SdlApplicationOptions options = SdlApplicationOptions.Parse(args);

using var application = new SdlApplication(options);
application.Run();
