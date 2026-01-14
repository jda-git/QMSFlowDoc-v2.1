using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace QMSFlowDoc.Launcher;

class Program
{
    static async Task Main(string[] args)
    {
        Console.Title = "QMS FlowDoc - Launcher & Setup";
        Console.WriteLine("========================================");
        Console.WriteLine("    QMS FlowDoc - Sistema de Gestión    ");
        Console.WriteLine("========================================");

        if (!CheckRequirements())
        {
            Console.WriteLine("\n[!] Faltan requisitos críticos.");
            Console.Write("¿Deseas intentar instalarlos automáticamente con winget? (s/n): ");
            if (Console.ReadLine()?.ToLower() == "s")
            {
                InstallRequirements();
            }
            else
            {
                Console.WriteLine("Por favor, instala los requisitos manualmente y reinicia.");
                return;
            }
        }

        Console.WriteLine("\n[*] Verificando base de datos...");
        await SetupDatabase();

        Console.WriteLine("\n[*] Configurando dependencias y backend...");
        SetupBackendConfig();

        Console.WriteLine("\n[*] Compilando aplicación...");
        if (!CompileApplication())
        {
            Console.WriteLine("\n[!] Error en la compilación. Verifica los logs.");
            return;
        }

        Console.WriteLine("\n[*] Iniciando aplicación...");
        StartApplication();
    }

    static bool CheckRequirements()
    {
        bool dotnetOk = CheckCommand("dotnet --version", "9.");
        bool postgresOk = CheckCommand("psql --version", "16") || CheckCommand("pg_isready", "");

        Console.WriteLine($"- .NET 9 SDK: {(dotnetOk ? "OK" : "No detectado")}");
        Console.WriteLine($"- PostgreSQL 16: {(postgresOk ? "OK" : "No detectado")}");

        return dotnetOk && postgresOk;
    }

    static bool CheckCommand(string command, string expectedPart)
    {
        try
        {
            // Improved argument handling
            var parts = command.Split(' ', 2);
            var fileName = parts[0];
            var arguments = parts.Length > 1 ? parts[1] : "";

            // Try executing command from PATH
            if (RunProcessByType(fileName, arguments, expectedPart)) return true;

            // Fallback: Check common specific paths for psql if that's what we are looking for
            if (fileName == "psql")
            {
                string[] commonPaths = {
                    @"C:\Program Files\PostgreSQL\16\bin\psql.exe",
                    @"C:\Program Files\PostgreSQL\17\bin\psql.exe",
                    @"C:\Program Files\PostgreSQL\15\bin\psql.exe"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path)) return true;
                }
            }

            return false;
        }
        catch { return false; }
    }

    static bool RunProcessByType(string fileName, string arguments, string expectedPart)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            string output = process?.StandardOutput.ReadToEnd() ?? "";
            process?.WaitForExit();
            // Case insensitive check
            return output.Contains(expectedPart, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void InstallRequirements()
    {
        Console.WriteLine("\n[*] Iniciando instalación de requisitos...");
        RunCommand("winget install Microsoft.DotNet.SDK.9");
        RunCommand("winget install PostgreSQL.PostgreSQL.16");
        Console.WriteLine("\n[!] Instalación finalizada. Es posible que necesites reiniciar la terminal.");
    }

    static async Task SetupDatabase()
    {
        // Check if DB exists or create it
        Console.Write("Introduce la clave del usuario 'postgres' para configurar DB: ");
        string? pass = Console.ReadLine();
        Environment.SetEnvironmentVariable("PGPASSWORD", pass);

        try {
            RunCommand("psql -U postgres -c \"CREATE DATABASE qmsflowdoc;\"");
            Console.WriteLine("[+] Base de datos creada o existente.");
            Console.WriteLine("[!] IMPORTANTE: Si ves errores de 'relación no existe', borra la DB manualmente y reinicia.");
            
            /* schema.sql is now deprecated - EF Core handles schema creation
            string schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/schema.sql"));
            if (File.Exists(schemaPath))
            {
                RunCommand($"psql -U postgres -d qmsflowdoc -f \"{schemaPath}\"");
                Console.WriteLine("[+] Esquema cargado.");
            }
            */
        } catch (Exception ex) {
            Console.WriteLine($"[!] Error configurando DB: {ex.Message}");
        }
        Environment.SetEnvironmentVariable("PGPASSWORD", null);
    }

    static void SetupBackendConfig()
    {
        string configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/QMSFlowDoc.Api/appsettings.json"));
        if (!File.Exists(configPath)) return;

        try
        {
            string json = File.ReadAllText(configPath);
            var root = JsonNode.Parse(json);
            
            // Set JWT Key if missing or simple
            var jwt = root!["Jwt"]!;
            if (jwt["Key"]?.ToString().Length < 32)
            {
                byte[] key = new byte[32];
                RandomNumberGenerator.Fill(key);
                jwt["Key"] = Convert.ToBase64String(key);
                Console.WriteLine("[+] Generada nueva clave JWT segura.");
            }

            File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] Error configurando backend: {ex.Message}");
        }
    }

    static bool CompileApplication()
    {
        // Compilation is now handled in StartApplication for API only
        // Client must be pre-compiled in Visual Studio
        Console.WriteLine("\n[*] El cliente debe estar compilado en Visual Studio (Platform: x64, Configuration: Debug)");
        return true;
    }

    static void StartApplication()
    {
        Console.WriteLine("\n[+] Compilando API...");
        string apiPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/QMSFlowDoc.Api"));
        
        if (!RunCommand($"dotnet build \"{apiPath}\" -c Debug"))
        {
            Console.WriteLine("[!] Error compilando API. Presiona cualquier tecla para salir.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("\n[+] Iniciando servicios. Mantén esta ventana abierta.");

        // Start API
        Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{apiPath}\" -c Debug --no-build",
            UseShellExecute = true
        });

        // Wait for API to start
        Task.Delay(3000).Wait();

        // Start Client - ensure we use the win-x64 version (which has runtime)
        string basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, 
            "../../../../../src/QMSFlowDoc.Client/bin/x64/Debug/net8.0-windows10.0.19041.0"));
        
        string clientExeDirect = Path.Combine(basePath, "QMSFlowDoc.Client.exe");
        string clientExeWin64 = Path.Combine(basePath, "win-x64", "QMSFlowDoc.Client.exe");
        
        // If direct build is newer, update the win-x64 folder with NEW CODE
        if (File.Exists(clientExeDirect) && File.Exists(clientExeWin64))
        {
            var timeDirect = File.GetLastWriteTime(clientExeDirect);
            var timeWin64 = File.GetLastWriteTime(clientExeWin64);
            
            // Allow a small grace period or just check if direct is newer
            if (timeDirect > timeWin64)
            {
                Console.WriteLine($"[+] Detectado nuevo código compilado ({timeDirect:HH:mm:ss}). Actualizando entorno win-x64...");
                
                string[] filesToUpdate = {
                    "QMSFlowDoc.Client.dll",
                    "QMSFlowDoc.Client.pdb",
                    "QMSFlowDoc.Shared.dll",
                    "QMSFlowDoc.Shared.pdb"
                };

                foreach (var file in filesToUpdate)
                {
                    string source = Path.Combine(basePath, file);
                    string dest = Path.Combine(basePath, "win-x64", file);

                    if (File.Exists(source))
                    {
                        try
                        {
                            File.Copy(source, dest, overwrite: true);
                            // console.WriteLine($"    -> Actualizado: {file}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[!] Error actualizando {file}: {ex.Message}");
                        }
                    }
                }
                Console.WriteLine($"[+] Actualización completada.");
            }
        }
        else if (File.Exists(clientExeDirect) && !File.Exists(clientExeWin64))
        {
             Console.WriteLine("[!] Error: No existe la carpeta win-x64. Por favor, realiza un 'Deploy' inicial desde Visual Studio.");
             // Fallback to try direct, though it often fails without runtime
             clientExeWin64 = clientExeDirect;
        }
        
        // Always use win-x64 version which has the reliable runtime environment
        string clientExe = clientExeWin64;
        
        if (!File.Exists(clientExe))
        {
            Console.WriteLine($"[!] Error: No se encuentra el cliente compilado.");
            Console.WriteLine($"[!] Por favor, compila QMSFlowDoc.Client en Visual Studio primero (Platform: x64, Configuration: Debug).");
            Console.WriteLine($"[!] Ruta esperada: {clientExe}");
            Console.WriteLine("\n[*] Presiona cualquier tecla para salir.");
            Console.ReadKey();
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = clientExe,
            UseShellExecute = true
        });
        
        Console.WriteLine("\n[*] Aplicación en ejecución.");
    }

    static bool RunCommand(string command)
    {
        try
        {
            var parts = command.Split(' ', 2);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = parts[0],
                Arguments = parts.Length > 1 ? parts[1] : "",
                UseShellExecute = false
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch { return false; }
    }
}
