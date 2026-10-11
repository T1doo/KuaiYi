using KuaiYi.MockProvider;

await using var server = await MockProviderHost.StartAsync();
Console.WriteLine("KUAIYI_MOCK_URL=" + server.BaseUri.AbsoluteUri);
await server.WaitForShutdownAsync();
