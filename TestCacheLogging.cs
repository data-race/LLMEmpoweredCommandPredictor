using System;
using System.IO;
using System.Threading.Tasks;
using LLMEmpoweredCommandPredictor.PredictorCache;

namespace CacheLoggingTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Testing InMemoryCache logging functionality...");
            var logFilePath = @"C:\Users\cashen\AppData\Local\Temp\LLMCommandPredictor_Cache.log";
            Console.WriteLine($"Expected log file location: {logFilePath}");
            Console.WriteLine();

            // Check if log file exists before testing
            if (File.Exists(logFilePath))
            {
                Console.WriteLine("Log file already exists. Current content:");
                Console.WriteLine(new string('=', 50));
                try
                {
                    var existingContent = File.ReadAllText(logFilePath);
                    Console.WriteLine(existingContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading existing log file: {ex.Message}");
                }
                Console.WriteLine(new string('=', 50));
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("Log file does not exist yet.");
            }

            Console.WriteLine("Creating InMemoryCache instance...");
            
            // Create a new cache instance (this will trigger initialization logging)
            var cache = new InMemoryCache();

            // Wait a moment for initialization to complete
            Console.WriteLine("Waiting for initialization to complete...");
            await Task.Delay(2000);

            Console.WriteLine("Testing cache operations...");

            // Test SetAsync
            await cache.SetAsync("test-command", """{"Suggestions":["test-command","test-command --help"],"Source":"test","IsFromCache":false,"GenerationTimeMs":1.0}""");
            Console.WriteLine("✓ SetAsync called");

            // Test GetAsync with cache hit
            var result = await cache.GetAsync("test-command");
            Console.WriteLine($"✓ GetAsync called - result: {(result != null ? "Found" : "Not found")}");

            // Test GetAsync with cache miss
            var missResult = await cache.GetAsync("nonexistent-command");
            Console.WriteLine($"✓ GetAsync called for miss - result: {(missResult != null ? "Found" : "Not found")}");

            // Test RemoveAsync
            await cache.RemoveAsync("test-command");
            Console.WriteLine("✓ RemoveAsync called");

            // Test ClearAsync
            await cache.ClearAsync();
            Console.WriteLine("✓ ClearAsync called");

            // Wait a moment for all logging to complete
            await Task.Delay(1000);

            Console.WriteLine();
            Console.WriteLine("Checking log file after operations...");
            
            if (File.Exists(logFilePath))
            {
                Console.WriteLine("Log file content after testing:");
                Console.WriteLine(new string('=', 50));
                try
                {
                    var content = File.ReadAllText(logFilePath);
                    Console.WriteLine(content);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading log file: {ex.Message}");
                }
                Console.WriteLine(new string('=', 50));
            }
            else
            {
                Console.WriteLine("ERROR: Log file was not created!");
            }

            // Dispose the cache
            cache.Dispose();
            Console.WriteLine("Cache disposed.");
        }
    }
}
