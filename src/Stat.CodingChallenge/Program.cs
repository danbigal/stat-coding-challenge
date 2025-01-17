﻿using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stat.CodingChallenge.Application;
using Stat.CodingChallenge.Domain.Handlers;
using Stat.CodingChallenge.Domain.Wrappers;
using Stat.CodingChallenge.Infrastructure.Handlers;
using Stat.CodingChallenge.Infrastructure.Wrappers;
using System.Diagnostics;

namespace Stat.CodingChallenge
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var serviceProvider = serviceCollection.BuildServiceProvider();

            var processor = serviceProvider.GetService<Processor>();

            Stopwatch stopwatch = Stopwatch.StartNew();

            await processor.ProcessAsync();

            Console.WriteLine($"Process finished: {stopwatch.Elapsed}. Press any key to finish.");
            Console.ReadKey();
        }

        public static void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton<IAmazonS3, AmazonS3Client>()
                .AddSingleton<ICsvHandler, CsvHandler>()
                .AddSingleton<IS3Handler, S3Handler>()
                .AddSingleton<IFileWrapper, FileWrapper>();

            services.AddSingleton<Processor>();

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            services.AddSingleton<IConfiguration>(provider => config);

            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
        }
    }
}
