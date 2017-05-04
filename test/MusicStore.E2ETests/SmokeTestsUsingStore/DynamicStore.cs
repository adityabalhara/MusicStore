﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.Extensions.Logging;

namespace E2ETests
{
    internal class DynamicStore : IDisposable
    {
        public const string MusicStoreAspNetCoreStore = "MUSICSTORE_ASPNETCORE_DYNAMIC_STORE";
        private readonly string _storeWorkingDir;
        private readonly ILogger _logger;

        public DynamicStore(bool createStoreInDefaultLocation, string storeDirectory, ILoggerFactory loggerFactory)
        {
            StoreDirectory = storeDirectory;
            _logger = loggerFactory.CreateLogger<DynamicStore>();

            var applicationPath = Helpers.GetApplicationPath(ApplicationType.Portable);
            var applicationProjFilePath = Path.Combine(applicationPath, "MusicStore.csproj");
            _storeWorkingDir = Path.Combine(Path.GetTempPath(), $"storeworkingdir-{Guid.NewGuid()}");
            var parameters = $"store "
                + $" --framework netcoreapp2.0"
                + $" --configuration {Helpers.GetCurrentBuildConfiguration()}"
                + $" --runtime {GetRuntimeIdentifier()}"
                + $" --working-dir {_storeWorkingDir}"
                + $" --manifest {applicationProjFilePath}"
                + $" --preserve-working-dir";

            if (!createStoreInDefaultLocation)
            {
                parameters += $" --output {StoreDirectory}";
            }

            _logger.LogInformation($"Executing command: dotnet {parameters}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = parameters,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = applicationPath,
            };

            var hostProcess = new Process() { StartInfo = startInfo };

            hostProcess.StartAndCaptureOutAndErrToLogger("dynamic-store", _logger);

            hostProcess.WaitForExit();

            if (hostProcess.ExitCode != 0)
            {
                var message = $"dotnet store exited with exit code : {hostProcess.ExitCode}";
                throw new Exception(message);
            }
        }

        public string StoreDirectory { get; }

        public static bool IsEnabled()
        {
            var useStore = Environment.GetEnvironmentVariable(MusicStoreAspNetCoreStore);
            if (string.IsNullOrEmpty(useStore)
                || string.Equals(useStore, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (Helpers.PreservePublishedApplicationForDebugging)
            {
                _logger.LogInformation("Skipping deleting the store and working directory as it has been disabled");
            }
            else
            {
                _logger.LogInformation("Deleting the storing and working directory.");

                RetryHelper.RetryOperation(
                        () => Directory.Delete(StoreDirectory, recursive: true),
                        e => _logger.LogError($"Failed to delete directory : {e.Message}"),
                        retryCount: 3,
                        retryDelayMilliseconds: 100);

                RetryHelper.RetryOperation(
                        () => Directory.Delete(_storeWorkingDir, recursive: true),
                        e => _logger.LogError($"Failed to delete directory : {e.Message}"),
                        retryCount: 3,
                        retryDelayMilliseconds: 100);
            }
        }

        private string GetRuntimeIdentifier()
        {
            var architecture = GetArchitecture();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win7-" + architecture;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux-" + architecture;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx-" + architecture;
            }
            else
            {
                throw new InvalidOperationException("Unrecognized operation system platform");
            }
        }

        private string GetArchitecture()
        {
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X86:
                    return "x86";
                case Architecture.X64:
                    return "x64";
                default:
                    throw new NotSupportedException($"Unsupported architecture: {RuntimeInformation.OSArchitecture}");
            }
        }
    }

}