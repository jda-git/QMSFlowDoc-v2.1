using System;
using System.IO;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services.Sync;

namespace QMSFlowDoc.Client.Services.Sync.Tests;

public static class SyncTests
{
    public static async Task RunTests()
    {
        Console.WriteLine("Running Hasher Tests...");
        var testFile = "test_hash.txt";
        await File.WriteAllTextAsync(testFile, "Hello World");
        var hash = await Hasher.CalculateSha256Async(testFile);
        
        // SHA256 of "Hello World"
        var expected = "a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e";
        if (hash == expected) Console.WriteLine("Hasher: PASS");
        else Console.WriteLine($"Hasher: FAIL (Expected {expected}, got {hash})");
        
        File.Delete(testFile);

        Console.WriteLine("Running SnapshotStore Tests...");
        var store = new SnapshotStore(); // Uses default path
        await store.InitializeAsync();
        
        var snap = new FileSnapshot { RelativePath = "test.txt", Status = SyncStatus.Synced, LastModifiedLocalUtc = DateTime.UtcNow };
        await store.UpsertSnapshotAsync(snap);
        
        var retrieved = await store.GetSnapshotAsync("test.txt");
        if (retrieved != null && retrieved.RelativePath == "test.txt") Console.WriteLine("SnapshotStore: PASS");
        else Console.WriteLine("SnapshotStore: FAIL");
    }
}
