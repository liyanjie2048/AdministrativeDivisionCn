using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace ConsoleApp
{
    class Program
    {
        static readonly string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\json");
        static readonly JsonSerializerOptions jsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
        };

        static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Console.Title = "获取行政区划数据";

            var context = new MongoDBContext("mongodb://192.168.6.6:27017");
            var collector = new Collector(context);
            await collector.RunAsync();

            await WriteLevelJsonAsync(context);
            await WriteLevel12JsonAsync(context);
            await WriteLevel123JsonAsync(context);
            await WriteLevel345JsonAsync(context);

            Console.WriteLine("Done!");
        }

        static async Task WriteLevelJsonAsync(MongoDBContext context)
        {
            for (int i = 1; i <= 5; i++)
            {
                var list = await context.DataSet.AsQueryable()
                    .Where(_ => _.Level == i)
                    .ToListAsync();
                var data = list
                    .OrderBy(_ => _.Code)
                    .ToDictionary(_ => _.Code, _ => _.Display);
                await File.WriteAllTextAsync(Path.Combine(baseDir, $"{i}.json"), JsonSerializer.Serialize(data, jsonOptions));
            }
        }
        static async Task WriteLevel12JsonAsync(MongoDBContext context)
        {
            var data = new Dictionary<string, object>();

            await FillDataAsync(context, data, 2, await context.DataSet.AsQueryable()
                .Where(_ => _.Level == 0)
                .OrderBy(_ => _.Code)
                .ToListAsync());

            await File.WriteAllTextAsync(Path.Combine(baseDir, $"12.json"), JsonSerializer.Serialize(data, jsonOptions));
        }
        static async Task WriteLevel123JsonAsync(MongoDBContext context)
        {
            var data = new Dictionary<string, object>();

            await FillDataAsync(context, data, 3, await context.DataSet.AsQueryable()
                .Where(_ => _.Level == 0)
                .OrderBy(_ => _.Code)
                .ToListAsync());

            await File.WriteAllTextAsync(Path.Combine(baseDir, $"123.json"), JsonSerializer.Serialize(data, jsonOptions));
        }
        static async Task WriteLevel345JsonAsync(MongoDBContext context)
        {
            var cities = await context.DataSet.AsQueryable()
                .Where(_ => _.Level == 2)
                .OrderBy(_ => _.Code)
                .ToListAsync();
            foreach (var item in cities)
            {
                var data = new Dictionary<string, object>();

                await FillDataAsync(context, data, 5, new[] { item });

                await File.WriteAllTextAsync(Path.Combine(baseDir, $"345-{item.Code}.json"), JsonSerializer.Serialize(data, jsonOptions));
            }
        }
        static async Task FillDataAsync(
            MongoDBContext context,
            Dictionary<string, object> data,
            int level,
            IEnumerable<AdministrativeDivisionCn> parents)
        {
            foreach (var item in parents)
            {
                var list = await context.DataSet.AsQueryable()
                    .Where(_ => _.Level == item.Level + 1)
                    .Where(_ => item.Level == 0 || _.Code.StartsWith(item.Code))
                    .OrderBy(_ => _.Code)
                    .ToListAsync();
                data.Add(item.Code, list.ToDictionary(_ => _.Code, _ => _.Display));

                if (item.Level + 1 < level)
                    await FillDataAsync(context, data, level, list);
            }
        }
    }
}
