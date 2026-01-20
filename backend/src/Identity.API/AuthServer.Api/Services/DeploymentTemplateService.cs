using System.Text;

namespace AuthServer.Api.Services;

public interface IDeploymentTemplateService
{
    GenerateTemplatesResult GenerateTemplates(GenerateTemplatesInput input);
}

public sealed class GenerateTemplatesInput
{
    public string? AppId { get; set; }
    public string? AppName { get; set; }
    public TechStackInput? TechStack { get; set; }
    public bool IncludeWorkflow { get; set; } = true;
    public bool IncludeDockerfiles { get; set; } = true;
}

public sealed class TechStackInput
{
    public FrameworkInput? Frontend { get; set; }
    public FrameworkInput? Backend { get; set; }
    public DatabaseInput? Database { get; set; }
}

public sealed class FrameworkInput
{
    public string? Framework { get; set; }
    public string? Language { get; set; }
    public string? Version { get; set; }
    public int Port { get; set; } = 3000;
    public string? EntryPoint { get; set; }
    public string? Runtime { get; set; }
    public string? ImportFile { get; set; }
    public string? BuildCommand { get; set; }
    public string? StartCommand { get; set; }
    public List<string>? DockerfileInstructions { get; set; }
    public List<string>? RuntimeInstructions { get; set; }
}

public sealed class DatabaseInput
{
    public string? Type { get; set; }
    public string? Provider { get; set; }
}

public sealed class GenerateTemplatesResult
{
    public bool Success { get; set; }
    public List<GeneratedFile> Files { get; set; } = new();
    public string? Error { get; set; }
}

public sealed class GeneratedFile
{
    public string Path { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string? Description { get; set; }
}

/// <summary>
/// Generates deployment templates (Dockerfiles, GitHub Actions workflows) using Jinja-style templates.
/// This is a simplified in-process implementation; the full logic from project-aid-v2 templates.
/// </summary>
public sealed class DeploymentTemplateService : IDeploymentTemplateService
{
    private readonly ILogger<DeploymentTemplateService> _logger;

    public DeploymentTemplateService(ILogger<DeploymentTemplateService> logger)
    {
        _logger = logger;
    }

    public GenerateTemplatesResult GenerateTemplates(GenerateTemplatesInput input)
    {
        var result = new GenerateTemplatesResult { Success = true };

        try
        {
            if (input.IncludeDockerfiles)
            {
                if (input.TechStack?.Backend != null)
                {
                    var backendDockerfile = GenerateBackendDockerfile(input.TechStack.Backend, input.AppName);
                    result.Files.Add(new GeneratedFile
                    {
                        Path = "backend/Dockerfile",
                        Content = Encoding.UTF8.GetBytes(backendDockerfile),
                        Description = "Backend Dockerfile"
                    });
                }

                if (input.TechStack?.Frontend != null)
                {
                    var frontendDockerfile = GenerateFrontendDockerfile(input.TechStack.Frontend, input.AppName);
                    result.Files.Add(new GeneratedFile
                    {
                        Path = "frontend/Dockerfile",
                        Content = Encoding.UTF8.GetBytes(frontendDockerfile),
                        Description = "Frontend Dockerfile"
                    });
                }
            }

            if (input.IncludeWorkflow)
            {
                var workflow = GenerateGitHubActionsWorkflow(input);
                result.Files.Add(new GeneratedFile
                {
                    Path = ".github/workflows/deploy.yml",
                    Content = Encoding.UTF8.GetBytes(workflow),
                    Description = "GitHub Actions deployment workflow"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate deployment templates");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private static string GenerateBackendDockerfile(FrameworkInput backend, string? appName)
    {
        var framework = (backend.Framework ?? "fastapi").ToLowerInvariant();
        var runtime = backend.Runtime ?? GetDefaultRuntime(framework);
        var port = backend.Port > 0 ? backend.Port : 8000;
        var entryPoint = backend.EntryPoint ?? GetDefaultEntryPoint(framework, "backend");
        var importFile = backend.ImportFile ?? GetDefaultImportFile(framework);
        var buildCommand = backend.BuildCommand ?? GetDefaultBuildCommand(framework);
        var startCommand = backend.StartCommand ?? GetDefaultStartCommand(framework, entryPoint, port);

        var sb = new StringBuilder();
        sb.AppendLine($"# Stage: Backend build ({framework})");
        sb.AppendLine($"FROM {runtime} AS builder");
        sb.AppendLine("WORKDIR /backend");
        sb.AppendLine();
        sb.AppendLine("# Copy dependency file first for better caching");
        sb.AppendLine($"COPY {importFile} ./");
        sb.AppendLine($"RUN {buildCommand}");
        sb.AppendLine();
        sb.AppendLine("# Copy all application code");
        sb.AppendLine("COPY . .");
        sb.AppendLine();

        // Framework-specific instructions
        if (backend.DockerfileInstructions?.Any() == true)
        {
            foreach (var instruction in backend.DockerfileInstructions)
            {
                sb.AppendLine(instruction);
            }
            sb.AppendLine();
        }

        sb.AppendLine($"# Verify entry point exists");
        sb.AppendLine($"RUN test -f {entryPoint} || (echo \"Entry point {entryPoint} not found\" && exit 1)");
        sb.AppendLine();
        sb.AppendLine("# Final stage");
        sb.AppendLine($"FROM {runtime}");
        sb.AppendLine("WORKDIR /backend");
        sb.AppendLine();
        sb.AppendLine("# Copy everything from builder");
        sb.AppendLine("COPY --from=builder /backend ./");
        sb.AppendLine();
        sb.AppendLine("# Set environment variables");
        sb.AppendLine("ENV PYTHONPATH=/");
        sb.AppendLine($"ENV PORT={port}");
        sb.AppendLine();

        // Runtime instructions
        if (backend.RuntimeInstructions?.Any() == true)
        {
            foreach (var instruction in backend.RuntimeInstructions)
            {
                sb.AppendLine($"RUN {instruction}");
            }
            sb.AppendLine();
        }

        sb.AppendLine($"EXPOSE {port}");
        sb.AppendLine();

        if (framework == "fastapi")
        {
            var moduleName = entryPoint.Replace(".py", "", StringComparison.OrdinalIgnoreCase);
            sb.AppendLine("# Start FastAPI application");
            sb.AppendLine($"CMD [\"uvicorn\", \"{moduleName}:app\", \"--host\", \"0.0.0.0\", \"--port\", \"{port}\"]");
        }
        else if (framework == "node.js" || framework == "express")
        {
            sb.AppendLine("# Start Node.js application");
            sb.AppendLine($"CMD [\"node\", \"{entryPoint}\"]");
        }
        else
        {
            sb.AppendLine("# Default start command");
            sb.AppendLine($"CMD {startCommand}");
        }

        return sb.ToString();
    }

    private static string GenerateFrontendDockerfile(FrameworkInput frontend, string? appName)
    {
        var framework = (frontend.Framework ?? "react").ToLowerInvariant();
        var runtime = frontend.Runtime ?? GetDefaultRuntime(framework);
        var port = frontend.Port > 0 ? frontend.Port : 3000;
        var entryPoint = frontend.EntryPoint ?? GetDefaultEntryPoint(framework, "frontend");
        var importFile = frontend.ImportFile ?? GetDefaultImportFile(framework);
        var buildCommand = frontend.BuildCommand ?? GetDefaultBuildCommand(framework);

        var sb = new StringBuilder();
        sb.AppendLine($"# Stage 1: Build ({framework})");
        sb.AppendLine($"FROM {runtime} AS builder");
        sb.AppendLine("WORKDIR /frontend");
        sb.AppendLine();

        if (framework == "streamlit")
        {
            sb.AppendLine("ENV PYTHONPATH=/frontend");
        }

        sb.AppendLine("# Copy dependency file first for better caching");
        sb.AppendLine($"COPY {importFile} ./");
        sb.AppendLine($"RUN {buildCommand}");
        sb.AppendLine();
        sb.AppendLine("# Copy all application code");
        sb.AppendLine("COPY . .");
        sb.AppendLine();

        // Framework-specific instructions
        if (frontend.DockerfileInstructions?.Any() == true)
        {
            foreach (var instruction in frontend.DockerfileInstructions)
            {
                sb.AppendLine(instruction);
            }
            sb.AppendLine();
        }

        if (framework == "react")
        {
            sb.AppendLine("# Ensure entry point exists");
            sb.AppendLine($"RUN test -f public/{entryPoint} || (echo \"Entry point public/{entryPoint} not found\" && exit 1)");
            sb.AppendLine();
            sb.AppendLine("# Build the application");
            sb.AppendLine("RUN npm run build");
            sb.AppendLine();
            sb.AppendLine("# Stage 2: Nginx");
            sb.AppendLine("FROM nginx:alpine");
            sb.AppendLine("WORKDIR /usr/share/nginx/html");
            sb.AppendLine();
            sb.AppendLine("# Copy build output");
            sb.AppendLine("COPY --from=builder /frontend/build /usr/share/nginx/html");
            sb.AppendLine();
            sb.AppendLine("# Copy custom nginx.conf from public (optional)");
            sb.AppendLine("COPY public/nginx.conf /etc/nginx/conf.d/default.conf");
            sb.AppendLine();
            sb.AppendLine("# Copy env.js and entrypoint.sh");
            sb.AppendLine("COPY public/env.js /usr/share/nginx/html/env.js");
            sb.AppendLine("COPY public/entrypoint.sh /entrypoint.sh");
            sb.AppendLine("RUN chmod +x /entrypoint.sh");
            sb.AppendLine();
            sb.AppendLine($"EXPOSE {port}");
            sb.AppendLine("ENTRYPOINT [\"/entrypoint.sh\"]");
        }
        else if (framework == "streamlit")
        {
            sb.AppendLine("# Final stage for Streamlit");
            sb.AppendLine($"FROM {runtime}");
            sb.AppendLine("WORKDIR /frontend");
            sb.AppendLine();
            sb.AppendLine("ENV PYTHONPATH=/frontend");
            sb.AppendLine();
            sb.AppendLine("# Copy everything from builder");
            sb.AppendLine("COPY --from=builder /frontend ./");
            sb.AppendLine();
            sb.AppendLine($"EXPOSE {port}");
            sb.AppendLine($"CMD [\"streamlit\", \"run\", \"{entryPoint}\"]");
        }
        else
        {
            sb.AppendLine($"# Ensure entry point exists");
            sb.AppendLine($"RUN test -f public/{entryPoint} || (echo \"Entry point public/{entryPoint} not found\" && exit 1)");
            sb.AppendLine("RUN npm run build");
            sb.AppendLine();
            sb.AppendLine($"FROM {runtime}");
            sb.AppendLine("WORKDIR /frontend");
            sb.AppendLine();
            sb.AppendLine("COPY --from=builder /frontend/build ./build");
            sb.AppendLine($"EXPOSE {port}");
            sb.AppendLine("CMD [\"echo\", \"Frontend build complete—static files are ready for serving.\"]");
        }

        return sb.ToString();
    }

    private static string GenerateGitHubActionsWorkflow(GenerateTemplatesInput input)
    {
        var appName = input.AppName ?? "mozaiks-app";
        var sanitizedName = appName.ToLowerInvariant().Replace(" ", "-");

        var sb = new StringBuilder();
        sb.AppendLine("name: Deploy to Azure Container Apps");
        sb.AppendLine();
        sb.AppendLine("on:");
        sb.AppendLine("  push:");
        sb.AppendLine("    branches: [main]");
        sb.AppendLine("  workflow_dispatch:");
        sb.AppendLine();
        sb.AppendLine("env:");
        sb.AppendLine("  DOCKERHUB_USERNAME: ${{ secrets.DOCKERHUB_USERNAME }}");
        sb.AppendLine("  CONTAINER_APP_NAME: ${{ secrets.CONTAINER_APP_NAME }}");
        sb.AppendLine();
        sb.AppendLine("jobs:");
        sb.AppendLine("  build-and-push:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    steps:");
        sb.AppendLine("      - uses: actions/checkout@v3");
        sb.AppendLine();
        sb.AppendLine("      - name: Set up Docker Buildx");
        sb.AppendLine("        uses: docker/setup-buildx-action@v2");
        sb.AppendLine();
        sb.AppendLine("      - name: Login to DockerHub");
        sb.AppendLine("        uses: docker/login-action@v3");
        sb.AppendLine("        with:");
        sb.AppendLine("          username: ${{ env.DOCKERHUB_USERNAME }}");
        sb.AppendLine("          password: ${{ secrets.DOCKERHUB_TOKEN }}");
        sb.AppendLine();

        if (input.TechStack?.Frontend != null)
        {
            sb.AppendLine("      - name: Build and push Frontend image");
            sb.AppendLine("        uses: docker/build-push-action@v4");
            sb.AppendLine("        with:");
            sb.AppendLine("          context: ./frontend");
            sb.AppendLine("          push: true");
            sb.AppendLine("          tags: |");
            sb.AppendLine("            ${{ env.DOCKERHUB_USERNAME }}/${{ secrets.CONTAINER_APP_NAME }}-f-${{ github.run_id }}:${{ github.sha }}");
            sb.AppendLine("            ${{ env.DOCKERHUB_USERNAME }}/${{ secrets.CONTAINER_APP_NAME }}-f-${{ github.run_id }}:latest");
            sb.AppendLine();
        }

        if (input.TechStack?.Backend != null)
        {
            sb.AppendLine("      - name: Build and push Backend image");
            sb.AppendLine("        uses: docker/build-push-action@v4");
            sb.AppendLine("        with:");
            sb.AppendLine("          context: ./backend");
            sb.AppendLine("          push: true");
            sb.AppendLine("          tags: |");
            sb.AppendLine("            ${{ env.DOCKERHUB_USERNAME }}/${{ secrets.CONTAINER_APP_NAME }}-b-${{ github.run_id }}:${{ github.sha }}");
            sb.AppendLine("            ${{ env.DOCKERHUB_USERNAME }}/${{ secrets.CONTAINER_APP_NAME }}-b-${{ github.run_id }}:latest");
            sb.AppendLine();
        }

        sb.AppendLine("  deploy:");
        sb.AppendLine("    runs-on: ubuntu-latest");
        sb.AppendLine("    needs: build-and-push");
        sb.AppendLine("    steps:");
        sb.AppendLine("      - name: Azure Login");
        sb.AppendLine("        uses: azure/login@v1");
        sb.AppendLine("        with:");
        sb.AppendLine("          creds: ${{ secrets.AZURE_CREDENTIALS }}");
        sb.AppendLine();
        sb.AppendLine("      - name: Deploy to Azure Container Apps");
        sb.AppendLine("        run: |");
        sb.AppendLine("          echo \"Deploying to Azure Container Apps...\"");
        sb.AppendLine("          # Add deployment commands here");

        return sb.ToString();
    }

    private static string GetDefaultRuntime(string framework) => framework.ToLowerInvariant() switch
    {
        "fastapi" or "flask" or "django" or "streamlit" => "python:3.11-slim",
        "node.js" or "express" or "react" or "vue" or "angular" => "node:18-alpine",
        "dotnet" or "aspnetcore" => "mcr.microsoft.com/dotnet/aspnet:8.0",
        _ => "python:3.11-slim"
    };

    private static string GetDefaultEntryPoint(string framework, string type) => framework.ToLowerInvariant() switch
    {
        "fastapi" => "main.py",
        "flask" => "app.py",
        "django" => "manage.py",
        "streamlit" => "app.py",
        "node.js" or "express" => "index.js",
        "react" => "index.html",
        "vue" => "index.html",
        _ => type == "frontend" ? "index.html" : "main.py"
    };

    private static string GetDefaultImportFile(string framework) => framework.ToLowerInvariant() switch
    {
        "fastapi" or "flask" or "django" or "streamlit" => "requirements.txt",
        "node.js" or "express" or "react" or "vue" or "angular" => "package.json",
        _ => "requirements.txt"
    };

    private static string GetDefaultBuildCommand(string framework) => framework.ToLowerInvariant() switch
    {
        "fastapi" or "flask" or "django" or "streamlit" => "pip install --no-cache-dir -r requirements.txt",
        "node.js" or "express" or "react" or "vue" or "angular" => "npm ci",
        _ => "pip install --no-cache-dir -r requirements.txt"
    };

    private static string GetDefaultStartCommand(string framework, string entryPoint, int port) => framework.ToLowerInvariant() switch
    {
        "fastapi" => $"[\"uvicorn\", \"{entryPoint.Replace(".py", "")}:app\", \"--host\", \"0.0.0.0\", \"--port\", \"{port}\"]",
        "flask" => $"[\"flask\", \"run\", \"--host=0.0.0.0\", \"--port={port}\"]",
        "node.js" or "express" => $"[\"node\", \"{entryPoint}\"]",
        _ => $"[\"python\", \"{entryPoint}\"]"
    };
}
